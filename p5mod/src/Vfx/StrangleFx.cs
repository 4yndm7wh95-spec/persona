using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 紧勒（0.9.49 审阅：敌人那里出现一圈像被绳子勒住的特效，要是特效不要贴图；这回合每打一张牌敌人掉血时都要有反馈）。
/// 第二版「咒烟绞索」（2026-11-18，第一版着色器绳圈被否：像一个平的光环）：材质用群蛇 / 巨手那套咒烟（CurseForm），
/// · 套上（Bind，刀命中后）：一道咒烟细索从他手里甩出去，飞到敌人身上，绕着脖子 / 上身缠两圈（螺旋往下，后半圈在敌人身后、前半圈在身前），
///   一拽 —— 两圈猛地收紧（过冲），绞索里透出一下暗红的光、顿帧、一声「咔」、身体一暗；连着他手的那截细索从他那头散成烟，
///   缠着的两圈留一会儿、慢慢散掉。
/// · 每勒一次（Cinch，StranglePower 每次结算，挂在 PowerModel.Flash 上）：两圈从烟里一下聚出来、再狠狠勒紧一截、红光一闪、极短顿帧、身体一暗、散掉。
/// </summary>
public static class StrangleFx
{
	private const int Turns = 2, PerTurn = 18, CordPts = 12;

	private readonly record struct Coil(Vector2 Neck, float Rx, float Ry, float Pitch, float R, float Th0);

	private static Coil Make(Vector2 center, Vector2 top, float width, float face)
	{
		float h = Mathf.Max(80f, (center - top).Length());
		float rx = Mathf.Clamp(width * 0.36f, h * 0.36f, h * 0.8f);
		// 从 Joker 那一侧起绕（朝右站时绳子从左边来）
		return new Coil(center.Lerp(top, 0.3f), rx, rx * 0.3f, h * 0.15f, Mathf.Clamp(h * 0.085f, 8f, 24f), face > 0 ? Mathf.Pi : 0f);
	}

	/// <summary>螺旋上的点：u 0～1（两圈），s 松紧（1 = 贴身）。z = 前后（下半圈在前）。</summary>
	private static (Vector2 P, float Z) CoilAt(Coil c, float u, float s, float face)
	{
		float th = c.Th0 + face * Mathf.Tau * Turns * u;
		Vector2 p = c.Neck + new Vector2(Mathf.Cos(th) * c.Rx * s, Mathf.Sin(th) * c.Ry * s + c.Pitch * (u * Turns - Turns * 0.5f) * 0.9f);
		return (p, Mathf.Sin(th));
	}

	/// <summary>把绞索（可选带一截连到手上的细索）放进 form。reveal = 从手那头往前长出来多少（0～1，按点数）。</summary>
	private static void Build(CurseForm form, Coil c, float face, float s, float reveal, Vector2? hand, float cordDiss, float coilDiss, float alpha)
	{
		form.Segs.Clear();
		var pts = new List<Vector2>();
		var rad = new List<float>();
		var z = new List<float>();
		int coilN = Turns * PerTurn + 1;
		if (hand is Vector2 from)
		{
			Vector2 to = CoilAt(c, 0f, s, face).P;
			Vector2 mid = from.Lerp(to, 0.5f) + new Vector2(0f, from.DistanceTo(to) * 0.12f);
			for (int i = 0; i < CordPts; i++)
			{
				float t = i / (float)CordPts;
				pts.Add(from.Lerp(mid, t).Lerp(mid.Lerp(to, t), t));
				rad.Add(c.R * Mathf.Lerp(0.45f, 0.85f, t));
				z.Add(1f);
			}
		}
		int cordN = pts.Count;
		for (int i = 0; i < coilN; i++)
		{
			var (p, zz) = CoilAt(c, i / (float)(coilN - 1), s, face);
			pts.Add(p);
			// 绞索两头细、中间粗一点（绳头收尖）
			float u = i / (float)(coilN - 1);
			rad.Add(c.R * (0.75f + 0.25f * Mathf.Sin(Mathf.Pi * Mathf.Min(1f, u * 1.15f))));
			z.Add(zz);
		}
		// 长出来：只画到 reveal 那一点（最后一段插值）
		float total = pts.Count - 1;
		float headF = Mathf.Clamp(reveal, 0f, 1f) * total;
		int head = Mathf.Min((int)headF, pts.Count - 2);
		if (headF <= 0.01f) { form.Commit(0f); return; }
		var vp = new List<Vector2>();
		var vr = new List<float>();
		var vz = new List<float>();
		for (int i = 0; i <= head; i++) { vp.Add(pts[i]); vr.Add(rad[i]); vz.Add(z[i]); }
		float frac = headF - head;
		vp.Add(pts[head].Lerp(pts[head + 1], frac));
		vr.Add(Mathf.Lerp(rad[head], rad[head + 1], frac) * 0.7f);
		vz.Add(Mathf.Lerp(z[head], z[head + 1], frac));
		// 细索和绞索分开加（各自散的程度不同）
		if (cordN > 0)
		{
			int cn = Mathf.Min(cordN + 1, vp.Count);
			if (cn >= 2) form.AddPolyline(vp.GetRange(0, cn), vr.GetRange(0, cn), vz.GetRange(0, cn), alpha, cordDiss, 0.9f);
			if (vp.Count > cordN)
			{
				int start = cordN;
				form.AddPolyline(vp.GetRange(start, vp.Count - start), vr.GetRange(start, vr.Count - start), vz.GetRange(start, vz.Count - start), alpha, coilDiss, 0.8f, 2f);
			}
		}
		else
		{
			form.AddPolyline(vp, vr, vz, alpha, coilDiss, 0.8f);
		}
	}

	/// <summary>套上：刀命中后。hand = 他甩出细索的手（没有就直接在敌人身上聚出来），tall = Joker 身高。</summary>
	public static void Bind(Control layer, Control? back, Vector2 center, Vector2 top, float width, Action<Color>? tint, float delay = 0f,
		Vector2? hand = null, float tall = 600f, float face = 1f)
	{
		Fx.After(layer, delay, () =>
		{
			Coil c = Make(center, top, width, face);
			float u = Fx.Screen(layer).Y / 1080f;
			const float fly = 0.16f, wrap = 0.26f, cinch = 0.3f, life = 1.5f;
			CurseForm form = CurseForm.Create(layer, back, life + 0.05, tall);
			form.Ember = 0.5f;
			form.TwistF = Mathf.Tau * tall * 0.2f / (c.R * 2.8f);   // 一股绳的螺距 ≈ 1.4 倍绳粗
			Sfx.Take("gx_whoosh", 0.14f);
			Sfx.Take("gx_tock", 0.22f, cinch);
			float last = 0f;
			Node2D driver = Fx.Root(layer, c.Neck, "P5StrangleDrive", life + 0.05);
			Fx.Animate(driver, 0, life, t =>
			{
				float s = t * life, dt = s - last;
				last = s;
				// 长出来：先飞到身上（细索），再缠两圈
				float reveal = hand == null ? Ease5.OutQuint(Mathf.Min(1f, s / wrap)) : (s < fly ? 0.2f * (s / fly) : Mathf.Lerp(0.2f, 1f, Ease5.OutQuint(Mathf.Min(1f, (s - fly) / (wrap - fly)))));
				// 松紧：缠的时候松（1.35）→ 一拽勒紧（0.8，过冲）→ 慢慢松一点
				float k = Ease5.OutBack(Ease5.Seg(s, cinch, 0.12f));
				float sc = Mathf.Lerp(1.35f, 0.8f, k) + 0.04f * Ease5.Seg(s, cinch + 0.3f, 0.6f);
				form.Flare = Mathf.Exp(-Mathf.Pow((s - cinch - 0.04f) / 0.08f, 2f)) + 0.15f * Ease5.Seg(s, 0f, cinch) * (1f - Ease5.Seg(s, cinch, 0.3f));
				float cordDiss = Fx.InCubic(Ease5.Seg(s, cinch + 0.05f, 0.3f));
				float coilDiss = Ease5.Seg(s, life - 0.55f, 0.55f);
				float alpha = 1f - Ease5.Seg(s, life - 0.15f, 0.15f);
				Build(form, c, face, sc, reveal, hand, cordDiss, coilDiss, alpha);
				form.Commit(dt);
			});
			Fx.After(layer, cinch + 0.04f, () =>
			{
				HitStop.Request(0.05);
				Fx.Shake(1);
				Glow.Flash(back ?? layer, c.Neck, new Color(0.85f, 0.05f, 0.14f), c.Rx * 3f, 0.4f);
				Phantom.Shards(layer, c.Neck, 8, 160f * u, 420f * u, Vector2.Up, 150f, 0.3f, 300f * u, 10f * u);
				if (tint != null) Fx.Animate(layer, 0, 0.6, tt => tint(Colors.White.Lerp(new Color(0.55f, 0.36f, 0.42f), (1f - tt) * (1f - tt))));
			});
		});
	}

	/// <summary>每勒一次：两圈从烟里聚出来，再狠狠勒紧一截。</summary>
	public static void Cinch(Control layer, Control? back, Vector2 center, Vector2 top, float width, Action<Color>? tint, float tall = 600f, float face = 1f)
	{
		Coil c = Make(center, top, width, face);
		float u = Fx.Screen(layer).Y / 1080f;
		const float pull = 0.09f, life = 0.8f;
		CurseForm form = CurseForm.Create(layer, back, life + 0.05, tall);
		form.Ember = 0.5f;
		form.TwistF = Mathf.Tau * tall * 0.2f / (c.R * 2.8f);
		Sfx.Take("gx_tock", 0.2f, pull);
		float last = 0f;
		Node2D driver = Fx.Root(layer, c.Neck, "P5StrangleDrive", life + 0.05);
		Fx.Animate(driver, 0, life, t =>
		{
			float s = t * life, dt = s - last;
			last = s;
			float k = Ease5.OutBack(Ease5.Seg(s, pull, 0.1f));
			float sc = Mathf.Lerp(1.12f, 0.74f, k);
			form.Flare = Mathf.Exp(-Mathf.Pow((s - pull - 0.03f) / 0.07f, 2f)) * 1.2f;
			float diss = Mathf.Max(1f - Ease5.Seg(s, 0f, 0.07f), Ease5.Seg(s, 0.38f, 0.4f));
			Build(form, c, face, sc, 1f, null, 0f, diss, 1f - Ease5.Seg(s, life - 0.1f, 0.1f));
			form.Commit(dt);
		});
		Fx.After(layer, pull + 0.03f, () =>
		{
			HitStop.Request(0.03);
			Glow.Flash(back ?? layer, c.Neck, new Color(0.85f, 0.05f, 0.14f), c.Rx * 2.4f, 0.3f);
			Phantom.Shards(layer, c.Neck, 5, 140f * u, 360f * u, Vector2.Up, 150f, 0.26f, 300f * u, 9f * u);
			if (tint != null) Fx.Animate(layer, 0, 0.4, tt => tint(Colors.White.Lerp(new Color(0.58f, 0.38f, 0.45f), (1f - tt) * (1f - tt))));
		});
	}
}
