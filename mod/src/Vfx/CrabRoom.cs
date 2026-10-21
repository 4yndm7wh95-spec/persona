using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace P3Makoto.Vfx;

/// <summary>All camera adaptation is confined to Kaiser Crab. Other rooms retain their original formulas.</summary>
public static class CrabRoom
{
    // Background is always the NCombatBackground wrapper. The crab controller is a child,
    // so testing the wrapper's runtime type never enabled any of the boss-only fixes.
    private const string BackgroundPath = "res://scenes/backgrounds/kaiser_crab_boss/kaiser_crab_boss_background.tscn";
    public static bool Active => NCombatRoom.Instance?.Background?.SceneFilePath == BackgroundPath;

    public static float Facing(NCreature actor) => Active ? Core.Makoto.Facing(actor) : 1f;
    public static Vector2 Offset(CanvasItem frame, Vector2 local) => Active ? frame.GetGlobalTransform().BasisXform(local) : local;
    public static float Height(Control layer, float worldHeight) => Active
        ? layer.GetGlobalTransform().AffineInverse().BasisXform(new Vector2(0f, worldHeight)).Y : worldHeight;

    /// <summary>Cancel the boss camera on a screen composition; leave normal-room nodes untouched.</summary>
    public static void FillScreen(Node2D root)
    {
        if (Active) root.GlobalTransform = Transform2D.Identity;
    }

    public static Control Screen(Control layer)
    {
        if (!Active || layer.Name == "P3CrabScreenLayer") return layer;
        Node2D? carrier = layer.GetNodeOrNull<Node2D>("P3CrabScreenSpace");
        if (carrier == null)
        {
            carrier = new Node2D { Name = "P3CrabScreenSpace" };
            layer.AddChild(carrier);
            carrier.AddChild(new Control { Name = "P3CrabScreenLayer", MouseFilter = Control.MouseFilterEnum.Ignore });
        }
        carrier.GlobalTransform = Transform2D.Identity;
        Control screen = carrier.GetNode<Control>("P3CrabScreenLayer");
        screen.Size = layer.GetViewportRect().Size;
        return screen;
    }
}
