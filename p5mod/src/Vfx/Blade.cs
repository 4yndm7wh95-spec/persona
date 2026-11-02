using Godot;

namespace P5Joker;

/// <summary>
/// P5 的刀光（2026-11-01 设计说明 docs/p5/FX_DESIGN_HITS.md：做法照结城理 mod 的 Motifs.ArcSlash，材质换成 P5）。
/// 一道窄长的月牙：外层红辉光（加法）+ 深红底 + 实色正红带 + 白芯（普通混合，硬）；头端一颗细长的十字闪带着运动方向。
/// 时间轴四段：
///  入  tIn   —— 从一端「划」到另一端（OutExpo），头端十字闪领着走；
///  满  tFlash —— 整条闪白、往外鼓一下（白芯变粗、辉光提亮）；
///  停  tHold —— 定住（P5 的定格感）；
///  出  tOut  —— 尾巴追着头收拢、整体变细、头端继续往前滑一点，最后头端炸成一簇红白光点。
/// 位置：at = 月牙弦的中点，angle = 弦的方向，bow = 拱高（往局部 -y 拱；flip 翻到另一边）。
/// </summary>
public static class Blade
{
	public static void Arc(Control layer, Vector2 at, float angle, float length, float bow, float thick, bool flip = false,
		float tIn = 0.05f, float tFlash = 0.04f, float tHold = 0.05f, float tOut = 0.16f, bool endSparks = true, float delay = 0f)
	{
		Fx.After(layer, delay, () =>
		{
			float total = tIn + tFlash + tHold + tOut;
			Node2D root = Fx.Root(layer, at, "P5Blade", total + 0.1);
			root.Rotation = angle;
			root.Scale = new Vector2(1f, flip ? -1f : 1f);
			const int n = 32;
			Polygon2D glow = Fx.Poly(root, new Vector2[3], new Color(1f, 0f, 0.04f, 0.75f), additive: true);
			Polygon2D shade = Fx.Poly(root, new Vector2[3], new Color(0.32f, 0f, 0.03f, 0.9f));      // 深红底：压住背景，红才「实」
			Polygon2D band = Fx.Poly(root, new Vector2[3], new Color(1f, 0.02f, 0.07f));            // 实色绯红（不叠加，不会发粉）
			Polygon2D core = Fx.Poly(root, new Vector2[3], new Color(1f, 0.97f, 0.96f));
			// 头：细长的十字闪（前后长、上下短）+ 一小团红辉光
			Node2D head = new();
			root.AddChild(head);
			Glow.SoftSprite(head, Vector2.Zero, Pal5.Bright, 0.9f);
			Fx.Poly(head, Cross(thick * 1.9f, thick * 0.55f, thick * 0.09f), new Color(1f, 0.97f, 0.96f), additive: true);

			Vector2 Point(float u, float off) => new((u - 0.5f) * length, -Mathf.Sin(u * Mathf.Pi) * bow + off);

			// 只画 [a, b] 这一段：中段最粗、两端收尖；头端（b）粗、尾端拖细；外沿（-y）厚、内沿薄
			Vector2[] Crescent(float a, float b, float th, float k)
			{
				var pts = new Vector2[n * 2];
				for (int i = 0; i < n; i++)
				{
					float u = Mathf.Lerp(a, b, i / (float)(n - 1));
					float local = (u - a) / Mathf.Max(b - a, 1e-4f);
					float w = Mathf.Pow(Mathf.Sin(u * Mathf.Pi), 0.8f) * Mathf.Pow(Mathf.Sin(local * Mathf.Pi * 0.5f + 0.0001f), 0.7f) * th * k;
					w *= 0.3f + 0.7f * local;
					pts[i] = Point(u, -w);
					pts[n * 2 - 1 - i] = Point(u, w * 0.22f);
				}
				return pts;
			}

			Fx.Animate(root, 0, total, t =>
			{
				float time = t * total;
				float inU = Ease5.OutExpo(Mathf.Clamp(time / tIn, 0f, 1f));
				float flash = Mathf.Clamp((time - tIn) / tFlash, 0f, 1f);
				float pulse = Mathf.Sin(flash * Mathf.Pi);
				float outU = Mathf.Clamp((time - tIn - tFlash - tHold) / tOut, 0f, 1f);
				outU = outU < 0.5f ? 4f * outU * outU * outU : 1f - Mathf.Pow(-2f * outU + 2f, 3f) / 2f;      // InOutCubic
				float b = inU + 0.06f * outU;
				float a = Mathf.Lerp(0f, Mathf.Min(b, 1.04f) - 0.02f, outU);
				float swell = 1f + 0.2f * pulse + 0.08f * outU;
				float thin = 1f - 0.6f * outU;
				glow.Polygon = Crescent(a, b, thick * 1.0f, swell * thin);
				shade.Polygon = Crescent(a, b, thick * 0.72f, swell * thin);
				band.Polygon = Crescent(a, b, thick * 0.58f, swell * thin);
				core.Polygon = Crescent(a, b, thick * 0.14f, (1f + 0.9f * pulse) * thin);
				float lift = 1f + 0.8f * pulse;
				glow.Modulate = new Color(lift, lift, lift, 1f);
				band.Modulate = new Color(1f + 0.25f * pulse, 1f + 0.25f * pulse, 1f + 0.25f * pulse, 1f);
				head.Position = Point(Mathf.Min(b, 1f), -thick * 0.15f);
				float headPop = time < tIn ? 1f : 1f - Fx.InCubic(Mathf.Clamp((time - tIn) / (tFlash + tHold + tOut * 0.6f), 0f, 1f)) * 0.75f;
				head.Scale = Vector2.One * headPop * (1f + 0.15f * pulse);
				root.Modulate = new Color(1f, 1f, 1f, 1f - Fx.InCubic(Mathf.Clamp((outU - 0.6f) / 0.4f, 0f, 1f)));
			});
			if (endSparks)
			{
				// 收尾：头端炸成一簇红白光点（顺着刀的去向）
				Fx.After(root, tIn + tFlash + tHold + tOut * 0.75f, () =>
				{
					Vector2 end = root.ToGlobal(Point(1f, 0f));
					Vector2 dir = Vector2.Right.Rotated(angle);
					Glow.Sparks(layer, end, 16, 120f, 520f, 0.35f, new[] { Pal5.Hot, Pal5.Bright, Pal5.Deep }, 0f, 0.1f, 0.3f, dir, 40f);
				});
			}
		});
	}

	/// <summary>细长十字闪：前后半长 len、上下半长 up，腰宽 w。</summary>
	public static Vector2[] Cross(float len, float up, float w) => new[]
	{
		new Vector2(len, 0f), new Vector2(w, -w), new Vector2(0f, -up), new Vector2(-w, -w),
		new Vector2(-len * 0.55f, 0f), new Vector2(-w, w), new Vector2(0f, up), new Vector2(w, w),
	};

	/// <summary>一圈很细的红色冲击环（线，不是面），从 r0 扩到 r1、变细消失；squash = 竖向压扁。</summary>
	public static void Ring(Control layer, Vector2 at, float r0, float r1, float width, float life = 0.3f, float delay = 0f, float squash = 1f)
	{
		Fx.After(layer, delay, () =>
		{
			Node2D root = Fx.Root(layer, at, "P5Ring", life + 0.05);
			root.Scale = new Vector2(1f, squash);
			Line2D ring = new() { Points = Fx.Circle(1f, 48), Closed = true, DefaultColor = new Color(1f, 0.02f, 0.07f) };
			Line2D hot = new() { Points = Fx.Circle(1f, 48), Closed = true, DefaultColor = new Color(1f, 0.95f, 0.94f) };
			root.AddChild(ring);
			root.AddChild(hot);
			Fx.Animate(root, 0, life, t =>
			{
				float r = Mathf.Lerp(r0, r1, Ease5.OutExpo(t));
				float w = width * (1f - Fx.OutCubic(t));
				ring.Scale = hot.Scale = Vector2.One * r;
				ring.Width = w * 1.6f / r;
				hot.Width = w * 0.6f / r;
			});
		});
	}
}
