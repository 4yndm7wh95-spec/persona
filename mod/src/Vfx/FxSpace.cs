using Godot;

namespace P3Makoto.Vfx;

/// <summary>World anchors stay in world coordinates; effect geometry stays in room units.</summary>
public static class FxSpace
{
    public static Vector2 Vector(CanvasItem layer, Vector2 local) => layer.GetGlobalTransform().BasisXform(local);
    public static Vector2 LocalVector(CanvasItem layer, Vector2 world) => layer.GetGlobalTransform().AffineInverse().BasisXform(world);
    public static float Scale(CanvasItem layer) => layer.GetGlobalTransform().X.Length();

    /// <summary>Cancel the room camera for screen compositions without changing draw order or game nodes.</summary>
    public static Control Screen(Control layer)
    {
        if (layer.Name == "P3ScreenLayer") return layer;
        Node2D? carrier = layer.GetNodeOrNull<Node2D>("P3ScreenSpace");
        if (carrier == null)
        {
            carrier = new Node2D { Name = "P3ScreenSpace" };
            layer.AddChild(carrier);
            carrier.AddChild(new Control { Name = "P3ScreenLayer", MouseFilter = Control.MouseFilterEnum.Ignore });
        }
        carrier.GlobalTransform = Transform2D.Identity;
        Control screen = carrier.GetNode<Control>("P3ScreenLayer");
        screen.Size = layer.GetViewportRect().Size;
        return screen;
    }
}
