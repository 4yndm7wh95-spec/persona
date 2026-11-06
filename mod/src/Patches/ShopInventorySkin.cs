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
    private const float WaterDur=.70f;
    // Overlap the entrances, then settle paper / word / portrait 80 ms apart.
    private const float PaperStart=.06f, PaperDur=.64f, PaperFade=.06f;
    private const float WordStart=.16f, WordDur=.62f, WordFade=.07f;
    private const float HeroStart=.26f, HeroDur=.60f, HeroFade=.08f;
    private const float EntryVisibleFrac=.25f;
    // Offline bounds: original EQUIP alpha and the union of equipment poses
    // at 0/.3/.5/.7/.9/1.1/1.3/1.5s, using the current shop camera.
    private const float WordContentTop=.69f, WordContentBottom=1.005f, HeroContentLeft=.35208333f;
    // The complete slanted sheet has its bottom corner at y=1.9234.
    // Start with only that corner in view (~11% of the final visible area),
    // rather than cutting the sheet horizontally to shorten its travel.
    private const float PaperFrom=-1.53f;
    private static readonly float WordFrom=Math.Clamp((1f-WordContentTop)-EntryVisibleFrac*(Math.Min(WordContentBottom,1f)-WordContentTop),.12f,.32f);
    // Keep the measured offscreen start; only the entrance timing changes.
    private static readonly float HeroFrom=Math.Clamp((1f-EntryVisibleFrac)*(1f-HeroContentLeft),.26f,.42f);
    private static readonly float OpenEnd=Math.Max(WaterDur,Math.Max(PaperStart+PaperDur,Math.Max(WordStart+WordDur,HeroStart+HeroDur)));
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
        elapsed=0f;
        ApplyOpening(0f);
        // Preloaded stages are already rendered. Never gate opening on a render callback.
        StartTick();
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
    private static float Progress(float time,float start,float duration) => Mathf.Clamp((time-start)/duration,0f,1f);

    // Solve Bezier X for elapsed time, then read Y for displacement; end at zero velocity.
    private static float CutEase(float progress,Vector2 first,Vector2 second)
    {
        if (progress<=0f || progress>=1f) return progress;
        float low=0f, high=1f;
        for (int i=0;i<18;i++)
        {
            float middle=(low+high)*.5f;
            Vector2 point=Vector2.Zero.BezierInterpolate(first,second,Vector2.One,middle);
            if (point.X<progress) low=middle;
            else high=middle;
        }
        return Vector2.Zero.BezierInterpolate(first,second,Vector2.One,(low+high)*.5f).Y;
    }
    private static float Fade(float time,float start,float duration)
    {
        float p=Progress(time,start,duration);
        return 1f-(1f-p)*(1f-p);
    }
    private void ApplyOpening(float time)
    {
        Vector2 first=new(.35f,0f), second=new(.65f,1f);
        float w=Progress(time,0f,WaterDur);
        water.SetShaderParameter("radius",Mathf.Lerp(.012f,endRadius,1f-Mathf.Pow(1f-w,3f)));
        paper.SetShaderParameter("paper_y",PaperFrom*(1f-CutEase(Progress(time,PaperStart,PaperDur),first,second)));
        paper.SetShaderParameter("paper_alpha",Fade(time,PaperStart,PaperFade));
        paper.SetShaderParameter("word_y",WordFrom*(1f-CutEase(Progress(time,WordStart,WordDur),first,second)));
        paper.SetShaderParameter("word_alpha",Fade(time,WordStart,WordFade));
        float x=HeroFrom*(1f-CutEase(Progress(time,HeroStart,HeroDur),first,second));
        float alpha=Fade(time,HeroStart,HeroFade);
        paper.SetShaderParameter("hero_x",x);
        paper.SetShaderParameter("hero_alpha",alpha);
        portrait.SetShaderParameter("shop_slide_x",x);
        portrait.SetShaderParameter("shop_alpha",alpha);
    }
    private void Leave()
    {
        StopOpening();
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

