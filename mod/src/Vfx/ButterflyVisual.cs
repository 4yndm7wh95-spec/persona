using Godot;

namespace P3Makoto.Vfx;

/// <summary>结局和菜单共用的蓝蝶：分翼振翅、间歇滑翔、随身柔光、下落亮粉。只用 Node2D，不参与 UI 命中。</summary>
internal sealed class ButterflyVisual
{
	internal Node2D Root { get; } = new() { Name = "P3ButterflyVisual" };
	internal Node2D Fly { get; } = new() { Name = "P3Butterfly" };
	private readonly Node2D trail = new(), wingL = new(), wingR = new(), body = new();
	private readonly Sprite2D aura;
	private readonly float size, dot;
	private float lastDust;

	internal ButterflyVisual(Node2D parent, float size)
	{
		this.size = size;
		dot = Fx.SoftDot.GetWidth();
		parent.AddChild(Root);
		Root.AddChild(trail);
		Root.AddChild(Fly);
		aura = new Sprite2D { Texture = Fx.SoftDot, Material = Fx.Additive, Scale = Vector2.One * size * 3.2f / dot, Modulate = new Color(0.35f, 0.7f, 1f, 0.5f) };
		Fly.AddChild(aura);
		Fly.AddChild(wingL);
		Fly.AddChild(wingR);
		Fly.AddChild(body);
		static Texture2D? Tex(string name)
		{
			string path = $"res://p3makoto/ui/{name}.png";
			return ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
		}
		Texture2D? tl = Tex("butterfly_wing_l"), tr = Tex("butterfly_wing_r"), tb = Tex("butterfly_body");
		if (tl != null && tr != null)
		{
			float sc = size * 2f / tl.GetWidth();
			wingL.AddChild(new Sprite2D { Texture = tl, Scale = Vector2.One * sc });
			wingR.AddChild(new Sprite2D { Texture = tr, Scale = Vector2.One * sc });
			if (tb != null) body.AddChild(new Sprite2D { Texture = tb, Scale = Vector2.One * sc });
		}
		else
		{
			foreach ((Node2D w, float sgn) in new[] { (wingL, -1f), (wingR, 1f) })
			{
				Vector2[] fore = { new(0f, -size * 0.05f), new(sgn * size * 0.5f, -size * 0.75f), new(sgn * size * 1.0f, -size * 0.6f), new(sgn * size * 0.85f, -size * 0.1f), new(sgn * size * 0.2f, size * 0.05f) };
				Vector2[] hind = { new(0f, size * 0.05f), new(sgn * size * 0.7f, size * 0.15f), new(sgn * size * 0.75f, size * 0.55f), new(sgn * size * 0.3f, size * 0.75f), new(sgn * size * 0.05f, size * 0.35f) };
				foreach (Vector2[] shape in new[] { fore, hind })
				{
					Polygon2D g = Fx.Poly(w, shape, new Color(0.35f, 0.75f, 1f, 0.55f), additive: true);
					g.Scale = Vector2.One * 1.12f;
					Fx.Poly(w, shape, new Color(0.12f, 0.45f, 1f, 0.85f));
					Fx.Line(w, shape, new Color(0.8f, 0.95f, 1f, 0.9f), 2f, closed: true);
				}
			}
			Fx.Poly(body, new[] { new Vector2(0f, -size * 0.35f), new Vector2(size * 0.05f, 0f), new Vector2(0f, size * 0.4f), new Vector2(-size * 0.05f, 0f) }, new Color(0.1f, 0.15f, 0.35f));
		}
	}

	internal void Update(Vector2 at, float angle, float time, float alpha, float perspective = 1f, bool emit = true)
	{
		float glide = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(time * Mathf.Tau / 1.6f), 6f);
		float flap = Mathf.Cos(time * Mathf.Tau * 6f);
		float open = Mathf.Lerp(0.18f + 0.82f * (0.5f + 0.5f * flap), 0.92f, glide);
		wingL.Scale = new Vector2(open, 1f);
		wingR.Scale = new Vector2(open, 1f);
		float bob = (1f - glide) * size * 0.06f * Mathf.Sin(time * Mathf.Tau * 6f + 1.2f);
		Fly.Position = at + new Vector2(0f, bob * perspective);
		Fly.Rotation = angle + 0.08f * Mathf.Sin(time * 2.3f);
		Fly.Scale = Vector2.One * perspective;
		Fly.Modulate = new Color(1f, 1f, 1f, alpha);
		aura.Modulate = new Color(0.35f, 0.7f, 1f, (0.4f + 0.15f * flap) * alpha);
		if (emit && time - lastDust > 0.03f)
		{
			lastDust = time;
			Sprite2D g = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Position = at + Fx.RandDir() * size * 0.15f * perspective, Scale = Vector2.One * size * perspective * Fx.Rand(0.08f, 0.16f) / dot, Modulate = new Color(0.55f, 0.85f, 1f, 0.8f) };
			trail.AddChild(g);
			Vector2 g0 = g.Position, drift = new(Fx.Rand(-8f, 8f), Fx.Rand(14f, 34f));
			Fx.Animate(g, 0.9f, u =>
			{
				g.Position = g0 + drift * Ease.OutCubic(u);
				g.Modulate = new Color(0.55f, 0.85f, 1f, 0.8f * (1f - u) * (0.6f + 0.4f * Mathf.Abs(Mathf.Cos(u * 9f))));
			});
			Fx.FreeAfter(g, 0.95f);
		}
	}
}
