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
    private ShaderMaterial paper = null!, portrait = null!;
    private Node2D hero = null!;
    private Tween? tween;
    private Action? dispose;
    private NMerchantInventory inventory = null!;
    private float elapsed, endRadius;
    private int warmFrames;
    private bool warming;
    private static Texture2D Png(string path)
    {
        using var im = new Image();
        if(im.LoadPngFromBuffer(FileAccess.GetFileAsBytes("res://p3makoto/"+path)) != Error.Ok) throw new InvalidOperationException(path);
        return ImageTexture.CreateFromImage(im);
    }
    internal void Setup(NMerchantInventory inv, Control back)
    {
        inventory = inv;
        ProcessMode = ProcessModeEnum.Always;
        SetProcess(false);
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
        StopOpening();
        tween?.Kill(); water.SetShaderParameter("reveal",1f); Show(); dispose?.Invoke(); dispose=null;
        // The merchant button encloses Igor's visible torso in normalized room coordinates.
        Vector2 center=new(.5f,.465f);
        if(inventory.GetParent() is MegaCrit.Sts2.Core.Nodes.Rooms.NMerchantRoom room)
            center=GetGlobalTransform().AffineInverse()*room.MerchantButton.GetGlobalRect().GetCenter()/Size;
        float aspect=Size.X/Math.Max(Size.Y,1);
        water.SetShaderParameter("center",center); water.SetShaderParameter("aspect",aspect);
        endRadius=new Vector2(Math.Max(center.X,1-center.X)*aspect,Math.Max(center.Y,1-center.Y)).Length()+.03f;
        hero.Modulate=Colors.White;
        dispose=HeroStage.ShowShopHero(hero,Size,out paper,out portrait);
        elapsed=0f;
        ApplyOpening(0f);
        // Render the hidden stage before starting; cap the first step after a cold load.
        warmFrames=0;
        warming=true;
        RenderingServer.FramePostDraw+=WarmDrawn;
    }
    private void WarmDrawn()
    {
        if (++warmFrames<2) return;
        RenderingServer.FramePostDraw-=WarmDrawn;
        warming=false;
        SetProcess(true);
    }
    private void StopOpening()
    {
        SetProcess(false);
        if (warming) RenderingServer.FramePostDraw-=WarmDrawn;
        warming=false;
    }
    public override void _Process(double delta)
    {
        elapsed+=Math.Min((float)delta,1f/30f);
        ApplyOpening(elapsed);
        if (elapsed>=1f) SetProcess(false);
    }
    private static float Progress(float time,float start,float duration) => Mathf.Clamp((time-start)/duration,0f,1f);

    // Solve Bezier X for elapsed time, then read Y for displacement. Both curves end at zero velocity.
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
    private void ApplyOpening(float time)
    {
        Vector2 first=new(.20f,.60f), second=new(.18f,1f);
        float waterProgress=Progress(time,0f,.55f);
        water.SetShaderParameter("radius",Mathf.Lerp(.012f,endRadius,1f-Mathf.Pow(1f-waterProgress,3f)));
        paper.SetShaderParameter("paper_y",-.30f*(1f-CutEase(Progress(time,.30f,.44f),first,second)));
        paper.SetShaderParameter("paper_alpha",Progress(time,.30f,.07f));
        // EQUIP reaches the bottom margin, so use the proposal's later word start.
        paper.SetShaderParameter("word_y",.13f*(1f-CutEase(Progress(time,.44f,.40f),new Vector2(.22f,.70f),new Vector2(.20f,1f))));
        paper.SetShaderParameter("word_alpha",Progress(time,.44f,.06f));
        float x=.22f*(1f-CutEase(Progress(time,.50f,.50f),first,second));
        float alpha=Progress(time,.50f,.08f);
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
        tween.Chain().TweenCallback(Callable.From(()=>{Hide();dispose?.Invoke();dispose=null;water.SetShaderParameter("reveal",1f);}));
    }
    public override void _ExitTree(){StopOpening();if(GodotObject.IsInstanceValid(inventory)) inventory.InventoryClosed-=Leave;dispose?.Invoke();dispose=null;}
}

