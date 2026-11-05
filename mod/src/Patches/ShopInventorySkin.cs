using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using P3Makoto.Vfx;

namespace P3Makoto.Patches;

[HarmonyPatch(typeof(NMerchantInventory), nameof(NMerchantInventory.Open))]
internal static class ShopInventorySkin
{
    internal static void Postfix(NMerchantInventory __instance) => ModEntry.Safe("shop inventory", () =>
    {
        var inv = __instance;
        if (inv.GetParent()?.GetNodeOrNull("P3VelvetShop") == null) return;
        var layer = inv.GetNodeOrNull<ShopWater>("P3ShopWater");
        if (layer == null)
        {
            var rug = inv.GetNodeOrNull<TextureRect>("SlotsContainer");
            var back = inv.GetNodeOrNull<Control>("BackButton");
            if (rug == null || back == null) return;
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
        layer.Enter();
    });
}

internal partial class ShopWater : Control
{
    private ShaderMaterial water = null!;
    private Node2D hero = null!;
    private Tween? tween;
    private Action? dispose;
    private NMerchantInventory inventory = null!;
    private static Texture2D Png(string path)
    {
        using var im = new Image();
        if(im.LoadPngFromBuffer(FileAccess.GetFileAsBytes("res://p3makoto/"+path)) != Error.Ok) throw new InvalidOperationException(path);
        return ImageTexture.CreateFromImage(im);
    }
    internal void Setup(NMerchantInventory inv, Control back)
    {
        inventory = inv;
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
    }
    internal void Enter()
    {
        tween?.Kill(); water.SetShaderParameter("reveal",1f); Show(); dispose?.Invoke(); dispose=null;
        // The merchant button encloses Igor's visible torso in normalized room coordinates.
        Vector2 center=new(.5f,.465f);
        if(inventory.GetParent() is MegaCrit.Sts2.Core.Nodes.Rooms.NMerchantRoom room)
            center=GetGlobalTransform().AffineInverse()*room.MerchantButton.GetGlobalRect().GetCenter()/Size;
        float aspect=Size.X/Math.Max(Size.Y,1);
        water.SetShaderParameter("center",center); water.SetShaderParameter("aspect",aspect);
        float end=new Vector2(Math.Max(center.X,1-center.X)*aspect,Math.Max(center.Y,1-center.Y)).Length()+.03f;
        const float seed = .012f;
        water.SetShaderParameter("radius",seed); hero.Modulate=Colors.Transparent;
        dispose=HeroStage.ShowShopHero(hero,Size);
        // Start only after the seed has actually been presented, even on a cold model load.
        var opening = tween = CreateTween().SetParallel();
        opening.Pause();
        RenderingServer.FramePostDraw += Begin;
        void Begin()
        {
            RenderingServer.FramePostDraw -= Begin;
            if (GodotObject.IsInstanceValid(this) && IsInsideTree() && opening == tween && opening.IsValid()) opening.Play();
        }
        tween.TweenMethod(Callable.From<float>(v=>water.SetShaderParameter("radius",v)),seed,end,.55).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(hero,"modulate:a",1f,.22).SetDelay(.32);
    }
    private void Leave()
    {
        tween?.Kill(); tween=CreateTween().SetParallel();
        tween.TweenMethod(Callable.From<float>(v=>water.SetShaderParameter("reveal",v)),1f,0f,.35);
        tween.TweenProperty(hero,"modulate:a",0f,.3);
        tween.Chain().TweenCallback(Callable.From(()=>{Hide();dispose?.Invoke();dispose=null;water.SetShaderParameter("reveal",1f);}));
    }
    public override void _ExitTree(){if(GodotObject.IsInstanceValid(inventory)) inventory.InventoryClosed-=Leave;dispose?.Invoke();dispose=null;}
}

