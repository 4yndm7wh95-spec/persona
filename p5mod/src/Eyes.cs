using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace P5Joker;

/// <summary>
/// 「1 MORE」眼神特写（用户 2026-10-28：原版打中弱点时那张撕纸露出 Joker 眼睛的切入，很帅）。
/// 贴图是 P5R 原版的战斗切入 BATTLE/CUTIN/BCT_P_BC0001（research/p5r/art/portrait 导出，3840x1536 透明底的眼睛特写）。
/// 画面中线一道横向撕开的口子：暗灰的底上，撕口上下是白色毛边、两头露出红色碎纸，口子里是 Joker 的眼睛（慢慢横移）；
/// 0.1 秒撕开 → 停 0.5 秒 → 合上。STS 里没有「弱点」，对应成「打中易伤的敌人」，旁边弹原版 WEAK 字样；眼神特写 8 秒内最多一次。
/// </summary>
public static class Eyes
{
	private static readonly Color Paper = new(0.97f, 0.96f, 0.93f), Dark = new(0.13f, 0.13f, 0.15f), Red = new(0.9f, 0.05f, 0.1f);
	private static ulong _last;

	/// <summary>打中易伤的敌人：WEAK 字样（每次），眼神特写（8 秒内一次）。</summary>
	public static void OnWeakHit(NCreature target)
	{
		Control? front = Joker.FxLayer();
		if (front == null)
		{
			return;
		}
		Weak(front, target.VfxSpawnPosition + new Vector2(0f, -(target.Hitbox?.Size.Y ?? 240f) * 0.6f));
		if (Time.GetTicksMsec() - _last < 8000)
		{
			return;
		}
		_last = Time.GetTicksMsec();
		Show(front);
	}

	private static void Weak(Control layer, Vector2 at)
	{
		if (Fx.Tex("weak") is not Texture2D w)
		{
			return;
		}
		Node2D root = Fx.Root(layer, at, "P5Weak", 0.9);
		Sprite2D s = Fx.Sprite(root, w, additive: false);
		s.Rotation = -0.1f;
		Fx.Animate(s, 0, 0.8, t =>
		{
			float pop = Mathf.Clamp(t / 0.1f, 0f, 1f);
			s.Scale = Vector2.One * 0.24f * (1f + 0.7f * (1f - Fx.OutCubic(pop)));
			s.Position = new Vector2(0f, -25f * Fx.InCubic(Mathf.Clamp((t - 0.7f) / 0.3f, 0f, 1f)));
			s.Modulate = new Color(1, 1, 1, 1f - Mathf.Clamp((t - 0.75f) / 0.25f, 0f, 1f));
		});
	}

	public static void Show(Control layer)
	{
		if (Fx.Tex("eyes") is not Texture2D tex)
		{
			return;
		}
		Vector2 screen = Fx.Screen(layer);
		float w = screen.X * 1.15f, h = screen.Y * 0.3f;
		Node2D root = Fx.Root(layer, new Vector2(screen.X * 0.5f, screen.Y * 0.42f), "P5Eyes", 0.95);
		root.Rotation = -0.06f;

		// 暗灰底：比撕口高一倍的一条，带一点斜线纹
		Polygon2D bg = Fx.Poly(root, Rect(w, h * 1.9f), Dark);
		for (int i = 0; i < 26; i++)
		{
			float x = -w / 2f + w * i / 26f;
			Fx.Poly(bg, new[] { new Vector2(x, -h * 0.95f), new Vector2(x + 8f, -h * 0.95f), new Vector2(x + 8f + h * 0.6f, h * 0.95f), new Vector2(x + h * 0.6f, h * 0.95f) },
				new Color(1f, 1f, 1f, 0.04f));
		}
		// 撕口：白色毛边（大一圈的锯齿形）→ 两头的红碎纸 → 眼睛（锯齿形里贴原版切入图）
		Vector2[] torn = Torn(w, h, 22f);
		Vector2[] edge = Torn(w * 1.01f, h * 1.18f, 30f);
		Polygon2D paper = Fx.Poly(root, edge, Paper);
		foreach (float side in new[] { -1f, 1f })
		{
			Fx.Poly(root, new[] { new Vector2(side * w * 0.36f, -h * 0.55f), new Vector2(side * w * 0.5f, -h * 0.4f), new Vector2(side * w * 0.5f, h * 0.2f),
				new Vector2(side * w * 0.42f, h * 0.45f), new Vector2(side * w * 0.33f, h * 0.1f) }, Red);
		}
		Vector2 ts = tex.GetSize();
		float regionH = ts.X * h / w;                                   // 保持比例：撕口里显示贴图里眼睛那一条
		float eyeY = ts.Y * 0.55f;
		Polygon2D face = new() { Texture = tex, Polygon = torn };
		root.AddChild(face);
		Vector2[] Uv(float pan)
		{
			Vector2[] uv = new Vector2[torn.Length];
			for (int i = 0; i < torn.Length; i++)
			{
				uv[i] = new Vector2((torn[i].X / w + 0.5f) * ts.X * 0.92f + ts.X * 0.04f + pan, eyeY + torn[i].Y / h * regionH);
			}
			return uv;
		}
		// 动画：0.1 秒从中线撕开（纵向放大），慢慢横移，0.75 秒后合上
		Fx.Animate(root, 0, 0.9, t =>
		{
			float time = 0.9f * t;
			float open = Fx.OutCubic(Mathf.Clamp(time / 0.1f, 0f, 1f)) * (1f - Fx.InCubic(Mathf.Clamp((time - 0.72f) / 0.18f, 0f, 1f)));
			root.Scale = new Vector2(1f + 0.05f * (1f - open), Mathf.Max(0.02f, open));
			face.UV = Uv(-60f + 120f * t);
			bg.Modulate = new Color(1, 1, 1, Mathf.Min(1f, open * 1.5f));
			paper.Modulate = new Color(1, 1, 1, open);
		});
		Fx.Shake(ShakeStrength.Weak);
	}

	private static Vector2[] Rect(float w, float h) =>
		new[] { new Vector2(-w / 2f, -h / 2f), new Vector2(w / 2f, -h / 2f), new Vector2(w / 2f, h / 2f), new Vector2(-w / 2f, h / 2f) };

	/// <summary>横向撕口：上下两条锯齿边（随机起伏 + 偶尔一个尖），两头收窄。</summary>
	private static Vector2[] Torn(float w, float h, float jag)
	{
		const int n = 34;
		Vector2[] pts = new Vector2[n * 2];
		for (int i = 0; i < n; i++)
		{
			float u = (float)i / (n - 1);
			float x = -w / 2f + w * u;
			float taper = Mathf.Min(1f, Mathf.Min(u, 1f - u) * 6f) * 0.75f + 0.25f;
			float spike = Fx.Rng.Next(5) == 0 ? jag * 1.6f : 0f;
			pts[i] = new Vector2(x, -h / 2f * taper + Fx.Rand(-jag, jag) - spike);
			pts[n * 2 - 1 - i] = new Vector2(x, h / 2f * taper + Fx.Rand(-jag, jag) + (Fx.Rng.Next(5) == 0 ? jag * 1.6f : 0f));
		}
		return pts;
	}
}
