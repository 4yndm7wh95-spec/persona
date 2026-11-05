using System;
using Godot;
namespace P3Makoto.Vfx;

internal static partial class HeroStage
{
    // A private viewport per merchant room lets the pile menu keep its own active stage.
    internal static Action ShowShopHero(Node2D host, Vector2 size)
    {
        Stage stage = new(host.GetTree());
        try { stage.LoadShop(host, size); }
        catch { stage.Dispose(); throw; }
        return stage.Dispose;
    }

    private sealed partial class Stage
    {
        internal void LoadShop(Node2D host, Vector2 view)
        {
            foreach (string file in Images[..3])
            {
                using Image image = new();
                if (image.LoadPngFromBuffer(Read("tex/" + file)) != Error.Ok) throw new InvalidOperationException(file);
                textures[file] = ImageTexture.CreateFromImage(image);
            }
            Node3D world = new();
            viewport.AddChild(world);
            camera = new Camera3D { KeepAspect = Camera3D.KeepAspectEnum.Width, Fov = 41f, Current = true,
                Position = new Vector3(0.17f, 0.015f, 0.18f) };
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
            Display.Material = new ShaderMaterial {Shader = Shaders.HeroKeyOut};
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
                camera.Position += new Vector3(point.X - .36f * halfWidth, point.Y + .66f * halfHeight, 0);
            }
            using var word = new Image();
            if (word.LoadPngFromBuffer(FileAccess.GetFileAsBytes("res://p3makoto/shop/equip_word.png")) != Error.Ok)
                throw new InvalidOperationException("equip_word.png");
            var paper = new ShaderMaterial { Shader = Shaders.ShopPaper };
            paper.SetShaderParameter("model_tex", Display.Texture);
            paper.SetShaderParameter("equip_tex", ImageTexture.CreateFromImage(word));
            var plate = new ColorRect { Material = paper, ShowBehindParent = true, MouseFilter = Control.MouseFilterEnum.Ignore };
            Display.AddChild(plate);
            plate.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            seq[0].Pose(0); Playing = true; ticking = true; lastTick = Time.GetTicksUsec();
            tree.ProcessFrame += Tick; viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        }
    }
}
