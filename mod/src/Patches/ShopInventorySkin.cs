using System;
using MegaCrit.Sts2.Core.Commands;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using P3Makoto.Vfx;

namespace P3Makoto.Patches;

[HarmonyPatch(typeof(NMerchantInventory), nameof(NMerchantInventory.Open))]
internal static class ShopInventorySkin
{
    [ThreadStatic] internal static int OpeningVelvet;
    internal static void Prefix(NMerchantInventory __instance, out bool __state)
    {
        __state=__instance.GetParent()?.GetNodeOrNull("P3VelvetShop")!=null;
        if(__state) OpeningVelvet++;
    }
    internal static void Finalizer(bool __state) { if(__state) OpeningVelvet--; }
    internal static void Postfix(NMerchantInventory __instance) => ModEntry.Safe("shop inventory", () =>
    {
        GetOrCreateLayer(__instance)?.Enter();
    });

    // Prepare is a reserved Harmony lifecycle method name inside a patch class.
    internal static ShopWater? GetOrCreateLayer(NMerchantInventory inv)
    {
        if (inv.GetParent()?.GetNodeOrNull("P3VelvetShop") == null) return null;
        var layer = inv.GetNodeOrNull<ShopWater>("P3ShopWater");
        if (layer == null)
        {
            var rug = inv.GetNodeOrNull<TextureRect>("SlotsContainer");
            var back = inv.GetNodeOrNull<Control>("BackButton");
            if (rug == null || back == null) return null;
            layer = new ShopWater {Name="P3ShopWater", MouseFilter=Control.MouseFilterEnum.Ignore};
            inv.AddChild(layer);
            inv.MoveChild(layer,rug.GetIndex());
            layer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            try { layer.Setup(inv,back); }
            catch { layer.Name="FailedShopWater"; layer.QueueFree(); throw; }
            if (inv.GetNodeOrNull<CanvasItem>("MerchantHandContainer") is {} hand)
                hand.Modulate = Colors.Transparent;
            rug.SelfModulate = Colors.Transparent; // only the rug's draw call; children keep their native transforms
        }
        return layer;
    }
}

internal partial class ShopWater : Control
{
    private ShaderMaterial water = null!;
    private ShaderMaterial paper = null!, portrait = null!;
    private Node2D hero = null!;
    private Tween? tween;
    private Action? dispose;
    private Action<bool>? setActive;
    private CanvasLayer? warmCanvas;
    private bool prewarming;
    private const float OpenEnd=.28f;
    private NMerchantInventory inventory = null!;
    private SceneTree tree = null!;
    private ulong lastTick;
    private float elapsed, endRadius;
    private int warmFrames;
    private bool ticking;
    private static Texture2D Png(string path)
    {
        using var im = new Image();
        if(im.LoadPngFromBuffer(FileAccess.GetFileAsBytes("res://p3makoto/"+path)) != Error.Ok) throw new InvalidOperationException(path);
        return ImageTexture.CreateFromImage(im);
    }
    internal void Setup(NMerchantInventory inv, Control back)
    {
        inventory = inv;
        tree = GetTree();
        TreeExiting += Cleanup;
        var idle=Png("shop/back_idle.png"); var focus=Png("shop/back_focus.png");
        water = new ShaderMaterial { Shader=Shaders.CommuBg };
        water.SetShaderParameter("shop_opaque", true);
        using var black = Image.CreateEmpty(1,1,false,Image.Format.Rgba8); black.Fill(Colors.Black);
        var emptyMask = ImageTexture.CreateFromImage(black); water.SetShaderParameter("mask_tex",emptyMask); water.SetShaderParameter("mask_next",emptyMask);
        water.SetShaderParameter("caustics",Png("hero3d/tex/T_UI_Camp_Caustics_00.png"));
        var bg=new ColorRect {Material=water,MouseFilter=MouseFilterEnum.Ignore};
        AddChild(bg); bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        hero=new Node2D(); AddChild(hero);
        var icon=new TextureRect {Texture=idle,ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.Scale,MouseFilter=MouseFilterEnum.Ignore};
        foreach(var child in back.GetChildren()) if(child is TextureRect rect) {rect.SelfModulate=Colors.Transparent;
            if(rect.GetNodeOrNull<CanvasItem>("Icon") is {} nativeIcon) nativeIcon.Hide();}
        back.AddChild(icon); icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); icon.OffsetLeft=36; icon.OffsetRight=6; icon.OffsetTop=21; icon.OffsetBottom=-21;
        bool hover=false, selected=false;
        void Refresh()=>icon.Texture=hover || selected ? focus : idle;
        back.MouseEntered+=()=>{hover=true; Refresh();}; back.MouseExited+=()=>{hover=false; Refresh();};
        back.FocusEntered+=()=>{selected=true; Refresh();}; back.FocusExited+=()=>{selected=false; Refresh();};
        inv.InventoryClosed+=Leave;
        Hide();
    }
    internal void Preload()
    {
        if (dispose != null) return;
        EnsureStage();
        // Draw the actual materials behind the opaque room, not through a hidden parent.
        warmCanvas=new CanvasLayer {Layer=-100};
        tree.Root.AddChild(warmCanvas);
        warmCanvas.AddChild(new ColorRect {Material=water,Size=Size,MouseFilter=MouseFilterEnum.Ignore});
        var display=hero.GetChild<TextureRect>(0);
        paper.SetShaderParameter("paper_alpha",1f);
        portrait.SetShaderParameter("shop_alpha",1f);
        warmCanvas.AddChild(new ColorRect {Material=paper,Size=Size,MouseFilter=MouseFilterEnum.Ignore});
        warmCanvas.AddChild(new TextureRect {Texture=display.Texture,Material=portrait,Size=Size,ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,MouseFilter=MouseFilterEnum.Ignore});
        warmFrames=0; prewarming=true;
        StartTick();
    }
    private void EnsureStage()
    {
        if(dispose==null)
        {
            dispose=HeroStage.ShowShopHero(hero,Size,out paper,out portrait,out setActive);
            ApplyOpening(0f);
        }
    }
    internal void Enter()
    {
        StopOpening();
        tween?.Kill(); water.SetShaderParameter("reveal",1f); Show();
        // The merchant button encloses Igor's visible torso in normalized room coordinates.
        Vector2 center=new(.5f,.465f);
        if(inventory.GetParent() is MegaCrit.Sts2.Core.Nodes.Rooms.NMerchantRoom room)
            center=GetGlobalTransform().AffineInverse()*room.MerchantButton.GetGlobalRect().GetCenter()/Size;
        float aspect=Size.X/Math.Max(Size.Y,1);
        water.SetShaderParameter("center",center); water.SetShaderParameter("aspect",aspect);
        endRadius=new Vector2(Math.Max(center.X,1-center.X)*aspect,Math.Max(center.Y,1-center.Y)).Length()+.03f;
        hero.Modulate=Colors.White;
        EnsureStage();
        setActive?.Invoke(true);
        // Finish only the native visual slide; retain Open's transactions and input setup.
        if(AccessTools.Field(typeof(NMerchantInventory), "_inventoryTween").GetValue(inventory) is Tween nativeTween) nativeTween.Kill();
        if(inventory.GetNodeOrNull<Control>("SlotsContainer") is {} slots) slots.Position=new Vector2(slots.Position.X,80f);
        if(AccessTools.Field(typeof(NMerchantInventory), "_backstop").GetValue(inventory) is CanvasItem backstop) backstop.Modulate=new Color(1,1,1,.8f);
        elapsed=0f;
        ApplyOpening(0f);
        // Preloaded stages are already rendered. Never gate opening on a render callback.
        StartTick();
        Audio.Sfx.Clip("igor_shop_cue107", Audio.Sfx.IgorVoice);
        GD.Print($"{ModEntry.Tag} shop opening started; size={Size}");
    }
    private void StartTick()
    {
        lastTick=Time.GetTicksUsec();
        if(ticking) return;
        ticking=true;
        tree.ProcessFrame+=Tick;
    }
    private void StopOpening()
    {
        if (ticking) tree.ProcessFrame-=Tick;
        ticking=false;
        prewarming=false;
        warmCanvas?.QueueFree(); warmCanvas=null;
    }
    private void Tick()
    {
        if(prewarming)
        {
            if(++warmFrames<3) return;
            StopOpening();
            ApplyOpening(0f);
            setActive?.Invoke(false);
            GD.Print($"{ModEntry.Tag} shop preload ready");
            return;
        }
        ulong now=Time.GetTicksUsec();
        float delta=(now-lastTick)/1000000f;
        lastTick=now;
        // Do not discard long frames: a 20 FPS opening must not run at 2/3 speed.
        // Paused/unfocused time is excluded, rather than capped on every frame.
        if (tree.Paused || !DisplayServer.WindowIsFocused()) return;
        elapsed+=delta;
        ApplyOpening(elapsed);
        if (elapsed>=OpenEnd) { ApplyOpening(OpenEnd); StopOpening(); GD.Print($"{ModEntry.Tag} shop opening completed"); }
    }
    private void ApplyOpening(float time)
    {
        water.SetShaderParameter("radius",endRadius);
        paper.SetShaderParameter("paper_y",0f);
        paper.SetShaderParameter("word_y",0f);
        paper.SetShaderParameter("hero_x",0f);
        paper.SetShaderParameter("paper_alpha",1f);
        paper.SetShaderParameter("word_alpha",1f);
        paper.SetShaderParameter("hero_alpha",1f);
        portrait.SetShaderParameter("shop_slide_x",0f);
        portrait.SetShaderParameter("shop_alpha",1f);
        float p=Mathf.Clamp(time/OpenEnd,0f,1f);
        // Fade the complete inventory, including goods and the return button, together.
        inventory.Modulate=new Color(1,1,1,p*p*(3f-2f*p));
    }
    private void Leave()
    {
        Audio.Sfx.Clip("igor_shop_back", Audio.Sfx.IgorVoice);
        StopOpening();
        inventory.Modulate=Colors.White;
        tween?.Kill(); tween=CreateTween().SetParallel();
        tween.TweenMethod(Callable.From<float>(v=>water.SetShaderParameter("reveal",v)),1f,0f,.35);
        tween.TweenProperty(hero,"modulate:a",0f,.3);
        tween.Chain().TweenCallback(Callable.From(()=>{Hide();setActive?.Invoke(false);water.SetShaderParameter("reveal",1f);}));
    }
    private void Cleanup()
    {
        StopOpening();
        tween?.Kill();
        if (GodotObject.IsInstanceValid(inventory)) inventory.InventoryClosed-=Leave;
        dispose?.Invoke();
        dispose=null;
    }
}


/// <summary>While the Velvet Room is open, the native merchant's voice/sound events stay silent.</summary>
[HarmonyPatch(typeof(SfxCmd), nameof(SfxCmd.Play), new Type[]{typeof(string),typeof(float)})]
internal static class VelvetShopMerchantSilence
{
    internal static bool Muted(string sfx) =>
        (VelvetShopPatch.ActiveRooms>0 || ShopInventorySkin.OpeningVelvet>0) && sfx.StartsWith("event:/sfx/npcs/merchant/", StringComparison.Ordinal);
    internal static bool Prefix(string sfx) => !Muted(sfx);
}

[HarmonyPatch(typeof(SfxCmd), nameof(SfxCmd.Play), new Type[]{typeof(string),typeof(string),typeof(float),typeof(float)})]
internal static class VelvetShopMerchantSilenceParam
{
    internal static bool Prefix(string sfx) => !VelvetShopMerchantSilence.Muted(sfx);
}

/// <summary>Igor answers each successful purchase with a random line.</summary>
[HarmonyPatch(typeof(NMerchantInventory), "OnPurchaseCompleted")]
internal static class VelvetShopPurchaseVoice
{
    internal static void Postfix(NMerchantInventory __instance, MegaCrit.Sts2.Core.Entities.Merchant.PurchaseStatus status) => ModEntry.Safe("igor purchase voice", () =>
    {
        if (status != MegaCrit.Sts2.Core.Entities.Merchant.PurchaseStatus.Success) return;
        if (__instance.GetParent()?.GetNodeOrNull("P3VelvetShop") == null) return;
        Audio.Sfx.Clip($"igor_buy_{GD.RandRange(1, 3)}", Audio.Sfx.IgorVoice);
    });
}
