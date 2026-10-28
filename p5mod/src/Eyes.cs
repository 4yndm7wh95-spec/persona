using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace P5Joker;

/// <summary>
/// 「1 MORE」眼神特写（用户 2026-10-28：原版打中弱点时那张撕纸露出 Joker 眼睛的切入，很帅）。
/// 贴图是 P5R 原版的战斗切入 BATTLE/CUTIN/BCT_P_BC0001（research/p5r/art/portrait 导出，3840x1536 透明底的眼睛特写）。
/// 画面中线一道横向撕开的口子：暗灰的底上，撕口上下是白色毛边、两头露出红色碎纸，口子里是 Joker 的眼睛（慢慢横移）；
/// 从左往右撕开 → 停 hold 秒（眼睛慢慢横移）→ 从左往右收掉（左进右出）。
/// 用在（用户 2026-10-28：这个帅，多用一些；召唤人格面具时当背景，不碎）：完整版召唤亚森（Persona.Summon，在人物身后撑满召唤全程）、
/// 大招攻击（暗杀、猎杀、掠夺者）出牌时。打中易伤的敌人只弹原版 WEAK 字样。
/// </summary>
public static class Eyes
{
	private static readonly Color Paper = new(0.97f, 0.96f, 0.93f), Dark = new(0.13f, 0.13f, 0.15f), Red = new(0.9f, 0.05f, 0.1f);
	private static ulong _last;

	/// <summary>打中易伤的敌人：头顶弹原版 WEAK 字样（同一个敌人 0.5 秒内只弹一次，多段攻击不刷屏）。</summary>
	public static void OnWeakHit(NCreature target)
	{
		Control? front = Joker.FxLayer();
		if (front == null || Time.GetTicksMsec() - _last < 500)
		{
			return;
		}
		_last = Time.GetTicksMsec();
		Weak(front, target.VfxSpawnPosition + new Vector2(0f, -(target.Hitbox?.Size.Y ?? 240f) * 0.6f));
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

	/// <summary>眼神特写：撕口从左往右撕开（前沿是锯齿形的毛边，沿途甩出白色碎纸），停 hold 秒（眼睛慢慢横移），
	/// 再从左往右收掉（尾沿追着前沿往右走，画面被往右拽出去）—— 原版就是左进右出，不是整张图平移。</summary>
	public static void Show(Control layer, bool big = false, float hold = 0.62f)
	{
		if (Fx.Tex("eyes") is not Texture2D tex)
		{
			return;
		}
		Vector2 screen = Fx.Screen(layer);
		float w = screen.X * 1.15f, h = screen.Y * (big ? 0.34f : 0.3f);
		Node2D root = Fx.Root(layer, new Vector2(screen.X * 0.5f, screen.Y * 0.4f), "P5Eyes", hold + 0.6);
		root.Rotation = -0.06f;

		Vector2[] bgShape = Torn(w * 1.02f, h * 1.9f, 12f);
		Vector2[] edge = Torn(w * 1.01f, h * 1.18f, 30f);
		Vector2[] torn = Torn(w, h, 22f);
		Polygon2D bg = Fx.Poly(root, new Vector2[3], Dark);
		Polygon2D paper = Fx.Poly(root, new Vector2[3], Paper);
		Polygon2D redL = Fx.Poly(root, new Vector2[3], Red), redR = Fx.Poly(root, new Vector2[3], Red);
		Vector2[] redShapeL = { new(-w * 0.5f, -h * 0.42f), new(-w * 0.36f, -h * 0.56f), new(-w * 0.33f, h * 0.08f), new(-w * 0.42f, h * 0.46f), new(-w * 0.5f, h * 0.2f) };
		Vector2[] redShapeR = redShapeL.Select(p => new Vector2(-p.X, -p.Y * 0.8f)).ToArray();
		Polygon2D face = new() { Texture = tex };
		root.AddChild(face);
		Vector2 ts = tex.GetSize();
		float regionH = ts.X * h / w, eyeY = ts.Y * 0.55f;
		float[] zig = Enumerable.Range(0, 7).Select(_ => Fx.Rand(-1f, 1f)).ToArray();

		const float enter = 0.16f, leave = 0.2f;
		float total = enter + hold + leave;
		float lastLead = -w / 2f;
		Fx.Animate(root, 0, total, t =>
		{
			float time = total * t;
			float xb = -w / 2f + w * Fx.OutCubic(Mathf.Clamp(time / enter, 0f, 1f));
			float xa = -w / 2f + w * Fx.InCubic(Mathf.Clamp((time - enter - hold) / leave, 0f, 1f));
			float pull = 260f * Fx.InCubic(Mathf.Clamp((time - enter - hold) / leave, 0f, 1f));       // 收的时候画面被往右拽
			float pan = 50f - 100f * time / total - pull * 3f;
			bg.Polygon = Slice(bgShape, xa - 30f, xb + 30f, zig, 26f);
			paper.Polygon = Slice(edge, xa - 12f, xb + 12f, zig, 30f);
			Vector2[] hole = Slice(torn, xa, xb, zig, 22f);
			face.Polygon = hole;
			face.UV = hole.Select(p => new Vector2((p.X / w + 0.5f) * ts.X * 0.92f + ts.X * 0.04f - pan, eyeY + p.Y / h * regionH)).ToArray();
			redL.Polygon = Slice(redShapeL, xa, xb, zig, 0f);
			redR.Polygon = Slice(redShapeR, xa, xb, zig, 0f);
			// 撕开时前沿、收掉时尾沿甩出白色碎纸
			float lead = time < enter ? xb : time > enter + hold ? xa : float.NaN;
			if (!float.IsNaN(lead) && Mathf.Abs(lead - lastLead) > w * 0.06f)
			{
				lastLead = lead;
				Scrap(root, new Vector2(lead, Fx.Rand(-h * 0.6f, h * 0.6f)), time < enter ? 1f : 1f);
			}
		});
		Fx.Shake(big ? ShakeStrength.Medium : ShakeStrength.Weak);
	}

	/// <summary>一片白色碎纸：从撕口的边往外（右上 / 右下）翻着飞出去。</summary>
	private static void Scrap(Node2D root, Vector2 at, float dir)
	{
		float s = Fx.Rand(14f, 30f);
		Polygon2D p = Fx.Poly(root, new[] { new Vector2(-s, -s * 0.4f), new Vector2(s * 0.8f, -s * 0.6f), new Vector2(s, s * 0.3f), new Vector2(-s * 0.3f, s * 0.6f) },
			Fx.Rng.Next(4) == 0 ? Red : Paper);
		p.Position = at;
		Vector2 v = new(dir * Fx.Rand(120f, 260f), Mathf.Sign(at.Y) * Fx.Rand(80f, 200f));
		float spin = Fx.Rand(-10f, 10f);
		Fx.Animate(p, 0, 0.45, t =>
		{
			p.Position = at + v * Fx.OutCubic(t);
			p.Rotation = spin * t;
			p.Modulate = new Color(1, 1, 1, 1f - Fx.InCubic(t));
		});
	}

	/// <summary>把「上沿左→右 + 下沿右→左」排列的撕口形状裁到 x∈[xa, xb]，两个切口画成锯齿竖边（zig 是固定的起伏，不会闪）。</summary>
	private static Vector2[] Slice(Vector2[] shape, float xa, float xb, float[] zig, float jag)
	{
		if (xb - xa < 2f)
		{
			return new[] { Vector2.Zero, Vector2.Zero, Vector2.Zero };
		}
		int n = shape.Length / 2;
		Vector2[] top = shape.Take(n).ToArray(), bottom = shape.Skip(n).Reverse().ToArray();
		float YAt(Vector2[] line, float x)
		{
			for (int i = 0; i < line.Length - 1; i++)
			{
				if (x >= line[i].X && x <= line[i + 1].X)
				{
					return Mathf.Lerp(line[i].Y, line[i + 1].Y, (x - line[i].X) / Mathf.Max(0.001f, line[i + 1].X - line[i].X));
				}
			}
			return x < line[0].X ? line[0].Y : line[^1].Y;
		}
		float lo = Mathf.Max(xa, top[0].X), hi = Mathf.Min(xb, top[^1].X);
		if (hi - lo < 2f)
		{
			return new[] { Vector2.Zero, Vector2.Zero, Vector2.Zero };
		}
		List<Vector2> pts = new() { new Vector2(lo, YAt(top, lo)) };
		pts.AddRange(top.Where(p => p.X > lo && p.X < hi));
		pts.Add(new Vector2(hi, YAt(top, hi)));
		float yt = YAt(top, hi), yb = YAt(bottom, hi);
		for (int k = 1; k < zig.Length; k++)
		{
			pts.Add(new Vector2(hi + zig[k] * jag, Mathf.Lerp(yt, yb, (float)k / zig.Length)));
		}
		pts.Add(new Vector2(hi, yb));
		pts.AddRange(bottom.Where(p => p.X > lo && p.X < hi).Reverse());
		pts.Add(new Vector2(lo, YAt(bottom, lo)));
		yt = YAt(top, lo);
		yb = YAt(bottom, lo);
		for (int k = zig.Length - 1; k >= 1; k--)
		{
			pts.Add(new Vector2(lo - zig[k] * jag, Mathf.Lerp(yt, yb, (float)k / zig.Length)));
		}
		return pts.ToArray();
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
