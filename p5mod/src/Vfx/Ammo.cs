using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 小刀弹药（用户 2026-10-29）：能量球旁边一个左轮弹巢，手牌里有几张小刀牌就装几发，最多 6 发（6 张以上照样显示 6 发）。
/// 素材是 Codex 画的 ammo_cylinder（转轮，正上方一个弹膛、每 60° 一个）/ ammo_round（装好的子弹）/ ammo_spent（打过的弹壳）；
/// 素材没到之前用简单图形代替。
/// 开枪（Fire，Joker 开火那一刻调用）：正上方那发一闪变成弹壳，转轮"咔"地转一格，弹壳淡掉；手里小刀还多于 6 张时，空出的弹膛马上补一发。
/// 拿到小刀：空弹膛一发发"啪"地塞进去。少了但没开枪（弃掉 / 消耗）：0.4s 后照样退掉。
/// Set(count) 只记目标，Tick(delta) 每帧推进（游戏里由 NEnergyCounter._Process 驱动，预览里由预览驱动）。
/// </summary>
public sealed class AmmoView
{
	public const int Chambers = 6;

	public readonly Node2D Root;
	private readonly Node2D _wheel;
	private readonly Sprite2D?[] _round = new Sprite2D?[Chambers];
	private readonly Node2D[] _slot = new Node2D[Chambers];
	private readonly Sprite2D _glow;
	private readonly bool[] _loaded = new bool[Chambers];
	private readonly float[] _pop = new float[Chambers];
	private readonly Texture2D? _roundTex, _spentTex;
	private readonly float _size;
	private int _top;                 // 现在转到正上方的是第几个弹膛
	private float _spin, _spinTarget, _flash, _pendingTimer;
	private int _target, _pendingFires;
	private readonly List<(Sprite2D S, float T)> _casings = new();

	public AmmoView(Node parent, float size = 104f)
	{
		_size = size;
		Root = new Node2D { Name = "P5Ammo" };
		parent.AddChild(Root);
		_glow = new Sprite2D { Texture = Glow.Soft, Material = Fx.Additive, Modulate = new Color(Pal5.Bright, 0f), Scale = Vector2.One * size / 64f * 1.6f };
		Root.AddChild(_glow);
		_wheel = new Node2D();
		Root.AddChild(_wheel);
		if (Fx.Tex("ammo_cylinder") is Texture2D cyl)
		{
			_wheel.AddChild(new Sprite2D { Texture = cyl, Scale = Vector2.One * size / cyl.GetWidth() });
		}
		else
		{
			// 素材没到：深色圆盘 + 红边 + 六个黑洞
			_wheel.AddChild(new Polygon2D { Polygon = Fx.Circle(size * 0.47f, 40), Color = new Color(0.1f, 0.02f, 0.04f) });
			_wheel.AddChild(new Line2D { Points = Fx.Circle(size * 0.47f, 40), Closed = true, Width = 3f, DefaultColor = Pal5.Crimson, Antialiased = true });
		}
		_roundTex = Fx.Tex("ammo_round");
		_spentTex = Fx.Tex("ammo_spent");
		float r = size * 0.5f * 0.55f, chamber = size * 0.2f;
		for (int i = 0; i < Chambers; i++)
		{
			_slot[i] = new Node2D { Position = Vector2.Up.Rotated(Mathf.Tau * i / Chambers) * r };
			_wheel.AddChild(_slot[i]);
			if (_roundTex == null)
			{
				_slot[i].AddChild(new Polygon2D { Polygon = Fx.Circle(chamber * 0.5f, 16), Color = new Color(0f, 0f, 0f) });
			}
			Sprite2D round = new()
			{
				Texture = _roundTex ?? Glow.Soft,
				Scale = Vector2.One * chamber / (_roundTex?.GetWidth() ?? 40f),
				Modulate = _roundTex == null ? new Color(1f, 0.85f, 0.6f) : Colors.White,
				Visible = false,
			};
			_slot[i].AddChild(round);
			_round[i] = round;
		}
	}

	public void Set(int count) => _target = Mathf.Clamp(count, 0, Chambers);

	/// <summary>Joker 开火那一刻：正上方那发打掉、转一格。</summary>
	public void Fire()
	{
		if (_pendingFires > 0)
		{
			_pendingFires--;
			_pendingTimer = _pendingFires > 0 ? _pendingTimer : 0f;
		}
		Shoot();
	}

	private int Loaded()
	{
		int n = 0;
		foreach (bool b in _loaded)
		{
			n += b ? 1 : 0;
		}
		return n;
	}

	private void Shoot()
	{
		int top = _top;
		if (_loaded[top])
		{
			_loaded[top] = false;
			_round[top]!.Visible = false;
			// 弹壳：在原地一闪、留一下、淡掉
			Sprite2D casing = new()
			{
				Texture = _spentTex ?? Glow.Soft, Scale = _round[top]!.Scale, Modulate = new Color(1.6f, 1.4f, 1.3f, 1f),
			};
			_slot[top].AddChild(casing);
			_casings.Add((casing, 0f));
		}
		_flash = 1f;
		_top = (_top + 1) % Chambers;          // 转一格：下一发转到正上方
		_spinTarget -= Mathf.Tau / Chambers;
	}

	public void Tick(float delta)
	{
		int loaded = Loaded();
		if (_target > loaded)
		{
			// 装弹：从正上方开始顺着往后找空弹膛，一帧塞一发
			for (int k = 0; k < Chambers; k++)
			{
				int i = (_top + k) % Chambers;
				if (!_loaded[i])
				{
					_loaded[i] = true;
					_round[i]!.Visible = true;
					_pop[i] = 1f;
					break;
				}
			}
		}
		else if (_target < loaded)
		{
			// 少了：先等开火（Fire）来打掉；0.4s 没开火（弃掉 / 消耗）就自己退
			if (_pendingFires < loaded - _target && _pendingTimer <= 0f)
			{
				_pendingFires = loaded - _target;
				_pendingTimer = 0.4f;
			}
			_pendingTimer -= delta;
			if (_pendingTimer <= 0f)
			{
				// 等了 0.4s 没开火：剩下的每 0.06s 退一发
				_pendingFires = Mathf.Max(0, _pendingFires - 1);
				Shoot();
				_pendingTimer = _pendingFires > 0 ? 0.06f : 0f;
			}
		}
		_spin = Mathf.Lerp(_spin, _spinTarget, 1f - Mathf.Exp(-delta * 22f));
		_wheel.Rotation = _spin;
		_flash = Mathf.Max(0f, _flash - delta * 5f);
		_glow.Modulate = new Color(Pal5.Bright, 0.15f + 0.6f * _flash);
		Root.Scale = Vector2.One * (1f + 0.07f * _flash);
		for (int i = 0; i < Chambers; i++)
		{
			_pop[i] = Mathf.Max(0f, _pop[i] - delta * 6f);
			float baseScale = _size * 0.2f / (_roundTex?.GetWidth() ?? 40f);
			_round[i]!.Scale = Vector2.One * baseScale * (1f + 0.5f * _pop[i]);
			_round[i]!.Modulate = _roundTex == null ? new Color(1f, 0.85f, 0.6f) : new Color(1f + _pop[i], 1f + _pop[i], 1f + _pop[i]);
		}
		for (int c = _casings.Count - 1; c >= 0; c--)
		{
			var (s, t) = _casings[c];
			t += delta;
			s.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((t - 0.25f) / 0.3f, 0f, 1f));
			if (t > 0.6f)
			{
				s.QueueFree();
				_casings.RemoveAt(c);
			}
			else
			{
				_casings[c] = (s, t);
			}
		}
		Root.Visible = true;
		Root.Modulate = new Color(1f, 1f, 1f, loaded > 0 || _casings.Count > 0 ? 1f : 0.55f);
	}
}
