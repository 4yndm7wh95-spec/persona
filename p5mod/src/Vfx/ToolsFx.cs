using Godot;

namespace P5Joker;

/// <summary>
/// 必备工具（能力：每回合开始多抽 1 张、再弃 1 张；用户：特效太简单、用了公用特效没特色）第三版「弹牌」（2026-11-18；前两版：剪刀斩一合（贴图 / 着色器），都被否）。
/// 怪盗的看家手艺 —— 魔术师的弹牌（card spring）：
/// · 四周稍微压暗，他手里亮出一沓预告信；
/// · 「哗——」一沓牌从他手里连珠弹出去，在身前划一道弧，一张接一张落成半空里悬着的一叠（每张跟着弧线转、带一点立体的晃）；
/// · 弧顶那一张被弹飞出去（= 弃一张）：翻着跟头飞开、从角上烧起来、烧没；
/// · 牌叠上方一张新牌凭空显形（= 多抽一张）：一道高光扫过、闪一下，落到牌叠上「嗒」；
/// · 整叠牌一口气飞回他手里，在手里「唰」地扇开、再合上，身上一圈红色的回响（能力生效）。~1.4s。
/// 全是着色器画的牌（Phantom.Card：牌背 / 牌面 / 高光 / 切开烧掉）+ 闪光，不用贴图。
/// </summary>
public static class ToolsFx
{
	public static void Play(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control front = c.Front;
		Control back = c.Back != null && GodotObject.IsInstanceValid(c.Back) ? c.Back : c.Front;
		Vector2 hand = c.Hand;
		Vector2 catchP = hand + new Vector2(f * h * 0.78f, -h * 0.04f);
		Vector2 ctrl = hand + new Vector2(f * h * 0.36f, -h * 0.72f);
		float w = h * 0.17f;
		const int N = 14, Drop = 6;
		const float go = 0.12f, gap = 0.024f, fly = 0.2f, fresh = 0.62f, settle = 0.72f, ret0 = 0.8f, ret1 = 0.97f, fan0 = 1.0f, end = 1.42f;
		Vector2 B(float e) => hand.Lerp(ctrl, e).Lerp(ctrl.Lerp(catchP, e), e);
		Vector2 T(float e) => ((ctrl - hand) * (1f - e) + (catchP - ctrl) * e).Normalized();

		// ── 声音 ──
		Sfx.Take("gx_card_slide", 0.12f, 0.02f);
		Sfx.Take("gx_fan_open", 0.18f, go);
		for (int k = 0; k < 7; k++) Sfx.Play("card_flick", 0.14f, go + 0.045f * k);
		Sfx.Play("card_flick", 0.3f, go + gap * Drop + fly * 0.5f);
		Sfx.Take("gx_burn", 0.18f, go + gap * Drop + fly * 0.5f + 0.1f);
		Sfx.Take("gx_ping", 0.22f, fresh);
		Sfx.Take("gx_card_stack", 0.16f, settle);
		Sfx.Take("gx_whoosh", 0.12f, ret0);
		Sfx.Take("gx_card_stack", 0.26f, ret1);
		Sfx.Take("gx_fan_open", 0.16f, fan0 + 0.02f);
		Sfx.Take("gx_fan_close", 0.14f, fan0 + 0.24f);

		SkillRedo5.Dim(back, hand.Lerp(catchP, 0.5f) + new Vector2(0f, -h * 0.2f), new Vector2(h * 3.6f, h * 2.6f), end,
			s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.15f)) * (1f - Ease5.Seg(s, ret1, end - ret1)) * 0.45f);
		Glow.Flash(back, ctrl.Lerp(catchP, 0.3f), new Color(0.5f, 0.03f, 0.08f), h * 1.8f, ret0);
		Glow.Flash(front, hand, Pal5.Hot, h * 0.14f, 0.1f);

		// ── 弹出去的一沓牌 ──
		var cards = new Phantom.P5Card[N];
		var lean = new float[N];
		for (int i = 0; i < N; i++)
		{
			cards[i] = Phantom.Card(front, hand, w, end + 0.05);
			cards[i].Root.Visible = false;
			lean[i] = Fx.Rand(-0.08f, 0.08f);
		}
		// 新显形的那张（多抽一张）
		Phantom.P5Card fresh1 = Phantom.Card(front, catchP, w, end + 0.05);
		fresh1.Root.Visible = false;
		// 扇开的五张（回到手里之后）
		var fanCards = new Phantom.P5Card[5];
		for (int i = 0; i < 5; i++)
		{
			fanCards[i] = Phantom.Card(front, hand, w, end + 0.05);
			fanCards[i].Root.Visible = false;
		}

		Vector2 dropV = Vector2.Zero, dropP = Vector2.Zero;
		bool dropInit = false;
		Node2D drive = Fx.Root(front, hand, "P5ToolsDrive", end + 0.05);
		Fx.Animate(drive, 0, end, t =>
		{
			float s = t * end;
			// 落下来的牌叠高度（已经落下的张数）
			int landed = 0;
			for (int i = 0; i < N; i++) if (i != Drop && s >= go + gap * i + fly) landed++;
			float ret = Ease5.Seg(s, ret0, ret1 - ret0);
			for (int i = 0; i < N; i++)
			{
				var cd = cards[i];
				float t0 = go + gap * i;
				if (s < t0 || s > ret1 + 0.01f * i) { cd.Root.Visible = false; continue; }
				cd.Root.Visible = true;
				float e = Mathf.Min(1f, (s - t0) / fly);
				if (i == Drop && e >= 0.5f)
				{
					// 弧顶被弹飞：翻着跟头飞开、从角上烧掉
					if (!dropInit)
					{
						dropInit = true;
						dropP = B(0.5f);
						dropV = (T(0.5f) + new Vector2(f * 0.3f, -0.55f)).Normalized() * h * 1.5f;
					}
					float dt = s - (t0 + fly * 0.5f);
					Vector2 p = dropP + dropV * dt + new Vector2(0f, h * 2.6f * dt * dt);
					float burn = Mathf.Clamp((dt - 0.08f) / 0.32f, 0f, 1.2f);
					cd.Place(p, f * dt * 14f, Mathf.Pi + dt * 22f, 1f);
					cd.Set("clip", new Vector3(-0.6f, 0.8f, -1.4f));
					cd.Set("burn", burn);
					cd.Set("alpha", 1f - Ease5.Seg(dt, 0.36f, 0.08f));
					if (dt > 0.45f) cd.Root.Visible = false;
					continue;
				}
				Vector2 pos;
				float tilt, flip;
				if (e < 1f)
				{
					pos = B(e);
					Vector2 tg = T(e);
					tilt = Mathf.Atan2(tg.Y, tg.X) - (f > 0 ? 0f : Mathf.Pi);             // 跟着弧线转（压一半，不然太晃）
					tilt = Mathf.Wrap(tilt, -Mathf.Pi, Mathf.Pi) * 0.55f;
					flip = Mathf.Pi + 0.55f * Mathf.Sin(e * Mathf.Pi);          // 牌背朝外，跟着弧线带一点立体的晃
				}
				else
				{
					// 落进牌叠：越后面落下的越在上面
					int order = i < Drop ? i : i - 1;
					pos = catchP + new Vector2(0f, -order * 1.3f * u) + new Vector2(Mathf.Sin(s * 4f) * 1.5f * u, 0f);
					tilt = lean[i];
					flip = Mathf.Pi;
				}
				if (ret > 0f)
				{
					// 整叠飞回手里（略带一点先后，像一股牌流回去）
					float r = Fx.InCubic(Mathf.Clamp((s - ret0 - 0.006f * i) / (ret1 - ret0), 0f, 1f));
					Vector2 mid = catchP.Lerp(hand, 0.5f) + new Vector2(0f, -h * 0.22f);
					pos = pos.Lerp(mid, r).Lerp(mid.Lerp(hand, r), r);
					tilt = Mathf.Lerp(tilt, -0.3f * f, r);
				}
				cd.Place(pos, tilt, flip, 1f - 0.25f * ret);
				cd.Set("sheen", e < 1f ? e * 3f - 1f : -1f);
			}
			// 新的一张：凭空显形、落到牌叠上
			if (s >= fresh - 0.1f && s < ret1)
			{
				fresh1.Root.Visible = true;
				float a = Mathf.Clamp((s - fresh + 0.1f) / 0.12f, 0f, 1f);
				float drop = Fx.InCubic(Ease5.Seg(s, settle - 0.08f, 0.08f));
				Vector2 above = catchP + new Vector2(0f, -h * 0.3f + Mathf.Sin(s * 6f) * h * 0.01f);
				Vector2 onTop = catchP + new Vector2(0f, -(landed + 1) * 1.3f * u);
				Vector2 p = above.Lerp(onTop, drop);
				if (ret > 0f)
				{
					float r = Fx.InCubic(ret);
					Vector2 mid = catchP.Lerp(hand, 0.5f) + new Vector2(0f, -h * 0.22f);
					p = p.Lerp(mid, r).Lerp(mid.Lerp(hand, r), r);
				}
				fresh1.Place(p, 0.1f * (1f - drop), Mathf.Pi * 2f * (1f - Fx.OutCubic(a)), (0.8f + 0.2f * a) * (1f - 0.25f * ret));
				fresh1.Set("alpha", a);
				fresh1.Set("glow", 0.9f * (1f - a) + 0.25f * Mathf.Exp(-(s - fresh) * 8f));
				fresh1.Set("sheen", Mathf.Clamp((s - fresh) / 0.2f, 0f, 1f) * 3f - 1f);
			}
			else fresh1.Root.Visible = false;
			// 回到手里：扇开、合上
			float fo = Ease5.Seg(s, fan0, 0.14f), fc = Ease5.Seg(s, fan0 + 0.22f, 0.14f);
			for (int i = 0; i < 5; i++)
			{
				var fcd = fanCards[i];
				bool on = s >= fan0 - 0.02f && s < end - 0.04f;
				fcd.Root.Visible = on;
				if (!on) continue;
				float spread = Ease5.OutBack(fo) * (1f - Fx.InCubic(fc));
				float a = (i - 2f) * 0.26f * spread - 0.3f * f;
				Vector2 pivot = hand + new Vector2(0f, w * 0.75f);
				Vector2 p = pivot + Vector2.FromAngle(a - Mathf.Pi * 0.5f) * w * 0.85f;
				fcd.Place(p, a, Mathf.Pi * (i == 4 ? 0f : 1f), 0.85f);
				fcd.Set("sheen", Mathf.Clamp((s - fan0 - 0.04f * i) / 0.16f, 0f, 1f) * 3f - 1f);
				fcd.Set("alpha", 1f - Ease5.Seg(s, end - 0.14f, 0.1f));
			}
		});
		// 被弹飞的那张烧起来：几点火星
		Fx.After(front, go + gap * Drop + fly * 0.5f + 0.12f, () =>
		{
			Vector2 p = dropP + dropV * 0.12f + new Vector2(0f, h * 2.6f * 0.0144f);
			Phantom.Shards(front, p, 6, 80f * u, 240f * u, Vector2.Up, 120f, 0.35f, 200f * u, 7f * u);
			Glow.Flash(front, p, new Color(1f, 0.4f, 0.2f), w * 2.2f, 0.3f);
		});
		// 新牌显形：一点闪光
		Fx.After(front, fresh, () =>
		{
			Glow.Glint(front, catchP + new Vector2(w * 0.4f, -h * 0.3f - w * 0.6f), 0.7f);
			Glow.Flash(front, catchP + new Vector2(0f, -h * 0.3f), Pal5.Hot, w * 1.6f, 0.14f);
		});
		Fx.After(front, settle, () => Glow.Flash(front, catchP, Pal5.Bright, w * 2.4f, 0.2f));
		// 回到手里：手里一亮，身上一圈红色的回响
		Fx.After(front, ret1, () =>
		{
			HitStop.Request(0.03);
			Glow.Flash(front, hand, Pal5.Hot, h * 0.2f, 0.1f);
			Glow.Flash(back, c.Chest, Pal5.Bright, h * 1.2f, 0.35f);
		});
		Fx.After(front, ret1 + 0.02f, () => SkillRedo5.EchoPulsePublic(c, 0.05f));
	}
}
