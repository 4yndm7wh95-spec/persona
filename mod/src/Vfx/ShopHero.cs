using System;
using Godot;
namespace P3Makoto.Vfx;

internal static partial class HeroStage
{
    // A private viewport per merchant room lets the pile menu keep its own active stage.
    internal static Action ShowShopHero(Node2D host, Vector2 size, Tween opening)
    {
        Stage stage = new(host.GetTree());
        try { stage.LoadShop(host, size, opening); }
        catch { stage.Dispose(); throw; }
        return stage.Dispose;
    }

    private sealed partial class Stage
    {
        internal void LoadShop(Node2D host, Vector2 view, Tween opening)
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
            var portrait = new ShaderMaterial {Shader = Shaders.HeroKeyOut};
            portrait.SetShaderParameter("shop_slide_x", .65f);
            Display.Material = portrait;
            noseLine.Hide(); noseUpper.Hide(); Display.AddChild(noseLine); Display.AddChild(noseUpper);
            Display.Size = view; Display.Texture = viewport.GetTexture();
            tree.Root.AddChild(viewport); owner = host; host.AddChild(Display);
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
            var paper = new ShaderMaterial { Shader = Shaders.ShopPaper };
            paper.SetShaderParameter("model_tex", Display.Texture);
            paper.SetShaderParameter("paper_y", -1.1f);
            paper.SetShaderParameter("word_y", .4f);
            paper.SetShaderParameter("hero_x", .65f);
            paper.SetShaderParameter("equip_tex", ImageTexture.CreateFromImage(word));
            var plate = new ColorRect { Material = paper, ShowBehindParent = true, MouseFilter = Control.MouseFilterEnum.Ignore };
            Display.AddChild(plate);
            plate.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            opening.TweenProperty(paper, "shader_parameter/paper_y", 0f, .78).SetDelay(.28)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            opening.TweenProperty(paper, "shader_parameter/word_y", 0f, .82).SetDelay(.44)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            opening.TweenMethod(Callable.From<float>(x =>
            {
                portrait.SetShaderParameter("shop_slide_x", x);
                paper.SetShaderParameter("hero_x", x);
            }), .65f, 0f, .9).SetDelay(.60)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            seq[0].Pose(0); Playing = true; ticking = true; lastTick = Time.GetTicksUsec();
            tree.ProcessFrame += Tick; viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        }
    }
}
