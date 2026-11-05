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
            camera = new Camera3D { KeepAspect = Camera3D.KeepAspectEnum.Width, Fov = 37.35f, Current = true,
                Position = new Vector3(0.17f, 0.015f, 0.18f) };
            world.AddChild(camera);
            foreach (string model in Models[..3])
            {
                using GltfDocument doc = new(); using GltfState state = new();
                if (doc.AppendFromBuffer(Read(model), Path, state) != Error.Ok || doc.GenerateScene(state) is not Node3D scene)
                    throw new InvalidOperationException(model);
                world.AddChild(scene); Walk(scene, false);
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
            seq[0].Pose(0); Playing = true; ticking = true; lastTick = Time.GetTicksUsec();
            tree.ProcessFrame += Tick; viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        }
    }
}
