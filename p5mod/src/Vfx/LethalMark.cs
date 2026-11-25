using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 毒必杀标记：一张预告信钉在血条右端（左端是格挡盾牌）——P5 里预告信 = 「你的心已经被盯上了」。
/// 出现：从右上方转着飞进来「啪」地钉住（回弹 + 一圈红色冲击环）；停住后轻轻摆、背后一团红光呼吸；
/// 不再必杀（被加了无实体、毒被清掉……）：翻面缩走；毒死了（血条刷新时已经没血）：一下胀开、碎成几片红黑碎片散掉。
/// 挂在血条容器（%HpBarContainer）下面，跟着血条走、跟着血条一起隐藏。
/// </summary>
public partial class LethalMark : Node2D
{
	private const string NodeName = "P5LethalMark";
	private enum State { In, Idle, Out, Burst }

	private State _state = State.In;
	private float _t;
	private Sprite2D _card = null!, _glow = null!;
	private Node2D _pin = null!;
	private float _seed;

	/// <summary>血条刷新之后调：sure = 当前是否毒必杀；dead = 这个怪已经没血了（毒死的那一下）。</summary>
	public static void Sync(Control bar, bool sure, bool dead)
	{
		LethalMark? mark = bar.GetNodeOrNull<LethalMark>(NodeName);
		if (sure && !dead)
		{
			if (mark == null || mark._state is State.Out or State.Burst)
			{
				mark?.QueueFree();
				mark = new LethalMark { Name = NodeName };
				bar.AddChild(mark);
			}
			mark.Place(bar);
			return;
		}
		if (mark == null || mark._state is State.Out or State.Burst) return;
		mark._state = dead ? State.Burst : State.Out;
		mark._t = 0f;
		if (dead) mark.Shatter();
	}

	public override void _Ready()
	{
		_seed = Fx.Rand(0f, 6.28f);
		ZIndex = 2;
		_glow = new Sprite2D { Texture = Glow.Soft, Modulate = new Color(0.95f, 0.05f, 0.1f, 0f), Material = Fx.Additive };
		AddChild(_glow);
		_pin = new Node2D();
		AddChild(_pin);
		_card = new Sprite2D { Texture = Fx.Tex("calling_card"), TextureFilter = TextureFilterEnum.LinearWithMipmaps };
		_pin.AddChild(_card);
		if (_card.Texture is Texture2D t) _card.Scale = Vector2.One * (58f / t.GetHeight());   // 卡高 58px（血条约 20px 高）
		_glow.Scale = Vector2.One * (150f / Math.Max(_glow.Texture.GetWidth(), 1));
		Visible = false;
	}

	private void Place(Control bar)
	{
		Position = new Vector2(bar.Size.X - 2f, bar.Size.Y * 0.5f - 6f);
	}

	private static float OutBack(float x, float s = 1.6f) { float c = s + 1f, y = x - 1f; return 1f + c * y * y * y + s * y * y; }

	public override void _Process(double delta)
	{
		_t += (float)delta;
		Visible = true;
		float sway = Mathf.Sin(_t * 2.1f + _seed) * 0.06f;
		switch (_state)
		{
			case State.In:
			{
				// 0.22 秒从右上方转着飞进来钉住
				float p = Mathf.Clamp(_t / 0.22f, 0f, 1f);
				float e = OutBack(p);
				_pin.Position = new Vector2(70f, -80f) * (1f - Mathf.Clamp(e, 0f, 1.2f));
				_pin.Rotation = Mathf.DegToRad(12f) + Mathf.DegToRad(-260f) * (1f - p) * (1f - p);
				_pin.Scale = Vector2.One * Mathf.Lerp(1.6f, 1f, e);
				_card.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(p * 3f, 0f, 1f));
				if (p >= 1f) { _state = State.Idle; _t = 0f; Ring(); }
				break;
			}
			case State.Idle:
			{
				// 轻摆 + 红光呼吸（不抢眼，但一直在动，扫一眼能看到）
				_pin.Position = new Vector2(0f, Mathf.Sin(_t * 1.7f + _seed) * 1.5f);
				_pin.Rotation = Mathf.DegToRad(12f) + sway;
				float pulse = 0.5f + 0.5f * Mathf.Sin(_t * 3.2f);
				_pin.Scale = Vector2.One * (1f + 0.04f * pulse);
				_glow.Modulate = new Color(0.95f, 0.05f, 0.1f, 0.25f + 0.3f * pulse);
				break;
			}
			case State.Out:
			{
				// 翻面缩走（横向缩放到 0 = 翻过去）
				float p = Mathf.Clamp(_t / 0.18f, 0f, 1f);
				_pin.Scale = new Vector2(Mathf.Cos(p * Mathf.Pi * 0.5f), 1f - 0.3f * p);
				_card.Modulate = new Color(1f, 1f, 1f, 1f - p);
				_glow.Modulate = new Color(0.95f, 0.05f, 0.1f, 0.3f * (1f - p));
				if (p >= 1f) QueueFree();
				break;
			}
			case State.Burst:
			{
				// 毒死：一下胀开、变亮、散掉（碎片在 Shatter 里）
				float p = Mathf.Clamp(_t / 0.3f, 0f, 1f);
				_pin.Scale = Vector2.One * (1f + 0.6f * Mathf.Sqrt(p));
				_card.Modulate = new Color(1f + p, 1f, 1f, 1f - p * p);
				_glow.Modulate = new Color(1f, 0.15f, 0.15f, 0.8f * (1f - p));
				if (_t > 0.9f) QueueFree();
				break;
			}
		}
	}

	/// <summary>钉住那一下：一圈红色冲击环往外扩散淡掉。</summary>
	private void Ring()
	{
		var ring = new Line2D { Width = 3f, DefaultColor = new Color(1f, 0.12f, 0.15f, 0.9f), Closed = true, Antialiased = true };
		for (int i = 0; i < 24; i++) ring.AddPoint(Vector2.FromAngle(i / 24f * Mathf.Tau) * 18f);
		AddChild(ring);
		Tween tw = ring.CreateTween().SetParallel();
		tw.TweenProperty(ring, "scale", Vector2.One * 2.6f, 0.28f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		tw.TweenProperty(ring, "modulate:a", 0f, 0.28f);
		tw.Chain().TweenCallback(Callable.From(ring.QueueFree));
	}

	/// <summary>毒死：红黑两色的纸片碎片往四周甩开、转着落下淡掉。</summary>
	private void Shatter()
	{
		for (int i = 0; i < 9; i++)
		{
			bool red = i % 3 != 0;
			float w = Fx.Rand(6f, 13f), h = Fx.Rand(9f, 18f);
			var shard = new Polygon2D
			{
				Polygon = new[] { new Vector2(-w * 0.5f, -h * 0.5f), new Vector2(w * 0.5f, -h * 0.3f), new Vector2(w * 0.3f, h * 0.5f), new Vector2(-w * 0.4f, h * 0.35f) },
				Color = red ? new Color(0.9f, 0.06f, 0.1f) : new Color(0.05f, 0.03f, 0.04f),
				Rotation = Fx.Rand(0f, 6.28f),
			};
			AddChild(shard);
			Vector2 dir = Vector2.FromAngle(Fx.Rand(0f, Mathf.Tau));
			Vector2 to = dir * Fx.Rand(40f, 90f) + new Vector2(0f, Fx.Rand(20f, 50f));
			Tween tw = shard.CreateTween().SetParallel();
			tw.TweenProperty(shard, "position", to, 0.7f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
			tw.TweenProperty(shard, "rotation", shard.Rotation + Fx.Rand(-6f, 6f), 0.7f);
			tw.TweenProperty(shard, "modulate:a", 0f, 0.7f).SetDelay(0.2f);
		}
	}
}
