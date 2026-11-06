using System;
using Godot;
namespace P3Makoto.Vfx;

internal static partial class HeroStage
{
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
            foreach (string file in Images[..3])
            {
                using Image image = new();
                if (image.LoadPngFromBuffer(Read("tex/" + file)) != Error.Ok) throw new InvalidOperationException(file);
                textures[file] = ImageTexture.CreateFromImage(image);
            }
            Node3D world = new();
            viewport.AddChild(world);
            camera = new Camera3D { KeepAspect = Camera3D.KeepAspectEnum.Width, Fov = 43f, Current = true,
                Position = new Vector3(0.17f, 0.015f, 0.18f), RotationDegrees = new Vector3(0, 0, -8f) };
            world.AddChild(camera);
            foreach (string model in Models[..3])
            {
                using GltfDocument doc = new(); using GltfState state = new();
                if (doc.AppendFromBuffer(Read(model), Path, state) != Error.Ok || doc.GenerateScene(state) is not Node3D scene)
                    throw new InvalidOperationException(model);
                world.AddChild(scene);
                int firstRig = heroes.Count;
                Walk(scene, false);
                if (model == "SK_PC0051_F000.glb" && heroes.Count > firstRig) faceRig = heroes[firstRig];
                scene.Position = new Vector3(.22110437f, .00706703f, .02424381f);
                scene.RotationDegrees = new Vector3(-1f,-6.872659f,0);
            }
            foreach (var mat in outlineMats) mat.SetShaderParameter("on", 0f);
            foreach (string clip in new[]{"Campequip","CampequipLoop","CampequipUni01","CampequipLoop","CampequipUni02"})
                motions.Add(clip == "CampequipLoop" && motions.Count > 2 ? motions[1] : new Motion(clip,new Clip(clip,true),null,heroes,cards));
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
            using var word = new Image();
            if (word.LoadPngFromBuffer(FileAccess.GetFileAsBytes("res://p3makoto/shop/equip_word.png")) != Error.Ok)
                throw new InvalidOperationException("equip_word.png");
            paper = new ShaderMaterial { Shader = Shaders.ShopPaper };
            paper.SetShaderParameter("model_tex", Display.Texture);
            paper.SetShaderParameter("paper_y", -.30f);
            paper.SetShaderParameter("paper_alpha", 0f);
            paper.SetShaderParameter("word_y", .13f);
            paper.SetShaderParameter("word_alpha", 0f);
            paper.SetShaderParameter("hero_x", .22f);
            paper.SetShaderParameter("hero_alpha", 0f);
            paper.SetShaderParameter("equip_tex", ImageTexture.CreateFromImage(word));
            var plate = new ColorRect { Material = paper, ShowBehindParent = true, MouseFilter = Control.MouseFilterEnum.Ignore };
            Display.AddChild(plate);
            plate.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            seq[0].Pose(0); Playing = true; ticking = true; lastTick = Time.GetTicksUsec();
            tree.ProcessFrame += Tick; viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        }
    }
}
