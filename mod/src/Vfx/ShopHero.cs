using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
namespace P3Makoto.Vfx;

internal static partial class HeroStage
{
    // 0.15.30: the goods-page stage used to decode its textures, parse three GLB models and
    // five motion clips on the main thread right as the room opened. Textures and clips are now
    // decoded once on a worker thread and kept for the session; the model scenes (which become
    // part of each visit's viewport) are prepared in the background for the next visit.
    private sealed class ShopPrepared
    {
        internal readonly Dictionary<string, Image> Images = new(StringComparer.Ordinal);
        internal readonly List<(string Model, Node Scene)> Scenes = new();
        internal readonly Dictionary<string, Clip> Clips = new(StringComparer.Ordinal);
    }
    private static readonly string[] ShopClipNames = { "Campequip", "CampequipLoop", "CampequipUni01", "CampequipUni02" };
    private static readonly Dictionary<string, Texture2D> shopTextures = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Clip> shopClips = new(StringComparer.Ordinal);
    private static Task<ShopPrepared>? shopPrep;
    private static bool shopPrepWarned;

    /// <summary>Starts (once) the background preparation of the next goods-page stage.</summary>
    internal static void ShopPrefetch()
    {
        if (shopPrep != null) return;
        bool needImages = shopTextures.Count < 3, needClips = shopClips.Count < ShopClipNames.Length;
        shopPrep = Task.Run(() =>
        {
            ShopPrepared p = new();
            if (needImages)
                foreach (string file in Images[..3])
                {
                    Image image = new();
                    if (image.LoadPngFromBuffer(Read("tex/" + file)) != Error.Ok || image.IsEmpty())
                        throw new InvalidOperationException("invalid hero texture: " + file);
                    p.Images[file] = image;
                }
            foreach (string model in Models[..3])
            {
                using GltfDocument doc = new();
                using GltfState state = new();
                if (doc.AppendFromBuffer(Read(model), Path, state) != Error.Ok)
                    throw new InvalidOperationException("invalid hero model: " + model);
                p.Scenes.Add((model, doc.GenerateScene(state) ?? throw new InvalidOperationException("empty hero scene: " + model)));
            }
            if (needClips)
                foreach (string name in ShopClipNames) p.Clips[name] = new Clip(name, true);
            return p;
        });
    }

    internal static bool ShopPrefetchDone => shopPrep is { IsCompleted: true };

    /// <summary>Main thread, map idle: uploads one prepared hero texture per call; true when nothing is left.</summary>
    internal static bool WarmShopTextures()
    {
        if (shopPrep is not { IsCompleted: true }) return false;
        if (!shopPrep.IsCompletedSuccessfully) { WarnShopPrep(); return true; }
        ShopPrepared p = shopPrep.Result;
        foreach ((string name, Clip clip) in p.Clips) shopClips.TryAdd(name, clip);
        p.Clips.Clear();
        foreach ((string file, Image image) in p.Images)
        {
            if (!shopTextures.ContainsKey(file)) shopTextures[file] = ImageTexture.CreateFromImage(image);
            image.Dispose();
            p.Images.Remove(file);
            return false;
        }
        return true;
    }

    private static void WarnShopPrep()
    {
        if (shopPrepWarned) return;
        shopPrepWarned = true;
        GD.PushWarning($"[P3Makoto] shop stage prefetch failed, loading on demand: {shopPrep?.Exception?.GetBaseException().Message}");
    }

    /// <summary>The finished background preparation, if any; it is consumed (its scenes join this visit).</summary>
    private static ShopPrepared? TakeShopPrep()
    {
        if (shopPrep is not { IsCompleted: true }) return null;   // still running: keep it for the next visit
        if (!shopPrep.IsCompletedSuccessfully) { WarnShopPrep(); shopPrep = null; return null; }
        ShopPrepared p = shopPrep.Result;
        shopPrep = null;
        return p;
    }

    private static Texture2D ShopTexture(string file, ShopPrepared? p)
    {
        if (shopTextures.TryGetValue(file, out Texture2D? tex)) return tex;
        if (p != null && p.Images.Remove(file, out Image? ready))
        {
            tex = ImageTexture.CreateFromImage(ready);
            ready.Dispose();
        }
        else
        {
            using Image image = new();
            if (image.LoadPngFromBuffer(Read("tex/" + file)) != Error.Ok) throw new InvalidOperationException(file);
            tex = ImageTexture.CreateFromImage(image);
        }
        return shopTextures[file] = tex;
    }

    private static Clip ShopClip(string name, ShopPrepared? p)
    {
        if (shopClips.TryGetValue(name, out Clip? clip)) return clip;
        if (p == null || !p.Clips.TryGetValue(name, out clip)) clip = new Clip(name, true);
        return shopClips[name] = clip;
    }
    // A private viewport per merchant room lets the pile menu keep its own active stage.
    internal static Action ShowShopHero(Node2D host, Vector2 size, out ShaderMaterial paper, out ShaderMaterial portrait, out Action<bool> setActive)
    {
        Stage stage = new(host.GetTree());
        try { stage.LoadShop(host, size, out paper, out portrait); }
        catch { stage.DisposeShop(); throw; }
        setActive=stage.SetShopActive;
        return stage.DisposeShop;
    }

    private sealed partial class Stage
    {
        private void StopShop() => SetShopActive(false);
        internal void DisposeShop()
        {
            StopShop();
            if(owner!=null && GodotObject.IsInstanceValid(owner)) owner.TreeExiting-=StopShop;
            owner=null;
            // TreeExiting may run while the parent is removing children.
            Display.QueueFree();
            viewport.QueueFree();
        }
        internal void SetShopActive(bool active)
        {
            if (ticking) tree.ProcessFrame-=Tick;
            ticking=false; Playing=active;
            if(active)
            {
                phase=0; time=0; blendFrom=-1;
                seq[0].Pose(0); lastTick=Time.GetTicksUsec();
                tree.ProcessFrame+=Tick; ticking=true;
            }
            viewport.RenderTargetUpdateMode=active ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
        }
        internal void LoadShop(Node2D host, Vector2 view, out ShaderMaterial paper, out ShaderMaterial portrait)
        {
            ulong loadStart = Time.GetTicksUsec();
            ShopPrepared? prepared = TakeShopPrep();
            foreach (string file in Images[..3]) textures[file] = ShopTexture(file, prepared);
            if (prepared != null) { foreach (Image left in prepared.Images.Values) left.Dispose(); prepared.Images.Clear(); }
            Node3D world = new();
            viewport.AddChild(world);
            camera = new Camera3D { KeepAspect = Camera3D.KeepAspectEnum.Width, Fov = 43f, Current = true,
                Position = new Vector3(0.17f, 0.015f, 0.18f), RotationDegrees = new Vector3(0, 0, -8f) };
            world.AddChild(camera);
            for (int mi = 0; mi < 3; mi++)
            {
                string model = Models[mi];
                Node3D scene;
                if (prepared != null && prepared.Scenes.Count == 3 && prepared.Scenes[mi].Model == model && prepared.Scenes[mi].Scene is Node3D ready)
                    scene = ready;
                else
                {
                    using GltfDocument doc = new(); using GltfState state = new();
                    if (doc.AppendFromBuffer(Read(model), Path, state) != Error.Ok || doc.GenerateScene(state) is not Node3D parsed)
                        throw new InvalidOperationException(model);
                    scene = parsed;
                }
                world.AddChild(scene);
                int firstRig = heroes.Count;
                Walk(scene, false);
                if (model == "SK_PC0051_F000.glb" && heroes.Count > firstRig) faceRig = heroes[firstRig];
                scene.Position = new Vector3(.22110437f, .00706703f, .02424381f);
                scene.RotationDegrees = new Vector3(-1f,-6.872659f,0);
            }
            foreach (var mat in outlineMats) mat.SetShaderParameter("on", 0f);
            foreach (string clip in new[]{"Campequip","CampequipLoop","CampequipUni01","CampequipLoop","CampequipUni02"})
                motions.Add(clip == "CampequipLoop" && motions.Count > 2 ? motions[1] : new Motion(clip,ShopClip(clip,prepared),null,heroes,cards));
            seq = motions; act = Act.Equip; Resize(view);
            portrait = new ShaderMaterial {Shader = Shaders.HeroKeyOut};
            portrait.SetShaderParameter("shop_slide_x", .22f);
            portrait.SetShaderParameter("shop_alpha", 0f);
            Display.Material = portrait;
            noseLine.Hide(); noseUpper.Hide(); Display.AddChild(noseLine); Display.AddChild(noseUpper);
            Display.Size = view; Display.Texture = viewport.GetTexture();
            tree.Root.AddChild(viewport); owner = host; host.AddChild(Display);
            owner.TreeExiting += StopShop;
            seq[1].Pose(0);
            int nose = faceRig?.Index("Jnt_C_Nose_00") ?? -1;
            if (faceRig != null && nose >= 0)
            {
                var skel = faceRig.Skeleton;
                Vector3 point = camera.ToLocal(skel.ToGlobal(skel.GetBoneGlobalPose(nose).Origin));
                var proj = camera.GetCameraProjection();
                float halfWidth = -point.Z / proj.X.X;
                float halfHeight = -point.Z / proj.Y.Y;
                // Keep the face in the user's marked area while bringing the outer leg into frame.
                camera.Position += camera.Basis * new Vector3(point.X - .42f * halfWidth, point.Y + .68f * halfHeight, 0);
            }
            Texture2D word = ShopAssets.Texture("shop/equip_word.png") ?? throw new InvalidOperationException("equip_word.png");
            paper = new ShaderMaterial { Shader = Shaders.ShopPaper };
            paper.SetShaderParameter("model_tex", Display.Texture);
            paper.SetShaderParameter("paper_y", -.30f);
            paper.SetShaderParameter("paper_alpha", 0f);
            paper.SetShaderParameter("word_y", .13f);
            paper.SetShaderParameter("word_alpha", 0f);
            paper.SetShaderParameter("hero_x", .22f);
            paper.SetShaderParameter("hero_alpha", 0f);
            paper.SetShaderParameter("equip_tex", word);
            var plate = new ColorRect { Material = paper, ShowBehindParent = true, MouseFilter = Control.MouseFilterEnum.Ignore };
            Display.AddChild(plate);
            plate.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            seq[0].Pose(0); Playing = true; ticking = true; lastTick = Time.GetTicksUsec();
            tree.ProcessFrame += Tick; viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
            GD.Print($"{ModEntry.Tag} perf shop stage built in {(Time.GetTicksUsec() - loadStart) / 1000f:0.0}ms (prepared={prepared != null})");
            ShopPrefetch();   // scenes for the next visit, off the main thread
        }
    }
}
