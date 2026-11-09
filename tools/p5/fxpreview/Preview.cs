using System;
using System.Collections.Generic;
using Godot;
using P5Joker;

/// <summary>
/// 假的战斗场景（1920x1080，和游戏里一样大）：左边 Joker（真的骨骼动画，SpinePlayer 读 art/p5/rig/joker.*，按游戏里的大小），右边一个敌人剪影，
/// 前景一层 Control 当 Stage.Front。按 --scenario=<名字> 在固定时间点放特效，render.sh 用 --write-movie 录下来。
/// </summary>
public partial class Preview : Node2D
{
	// 游戏里的大致站位：Joker 脚底、敌人中心（VfxSpawnPosition）
	public static readonly Vector2 JokerFeet = new(430f, 830f);
	// 命中点 = 原版怪物身体中心（research/sts2_preview_scene 的 chomper / inklet，脚底在 y 830）
	public static readonly Vector2 Enemy = new(1420f, 715f);
	public static readonly Vector2 Enemy3 = new(1720f, 725f);
	public static readonly Vector2 Enemy2 = new(1140f, 700f);
	// 姿势图像素 → 屏幕：ORIGIN (815,1622)，K 0.87 × Visuals 缩放 0.29（tools/p5/build_rig_p5.py、p5mod/src/Joker.cs）
	public static Vector2 Pose(float x, float y) => JokerFeet + new Vector2(x - 815f, y - 1622f) * 0.87f * 0.29f;
	public static Vector2 Chest => Pose(820f, 600f);

	private Node2D _world = null!;
	private Control _front = null!, _back = null!;
	private int _shake;
	private float _shakeLeft;
	private double _clock;

	private SpinePlayer? _joker;
	private readonly Dictionary<Vector2, Node2D> _enemies = new();

	// 游戏里的时序：打击 attack 动画开始后 0.15s 结算伤害（静默猎手 AttackAnimDelay），开枪 shiv 0.2s 后
	private static readonly Dictionary<string, Action<Preview>> Scenarios = new()
	{
		// 召唤：拿起技能牌 → 打出 → 舞台 + 原版眼神特写 → 咒怨
		["summon"] = p =>
		{
			// 稀有召唤（标杆 1：两拍）：拿起技能牌 → 打出 → 舞台 + 眼神特写 → 拍 1 召唤（面具喷光、特写碎开、亚森现身）→ 拍 2 亚森出手（单体抓握）
			Persona.Spec spec = Persona.ArseneSpec with { Linger = 1.1f };
			p.Ready(0.25, Stance.Mask, "mask", "cast_from_mask", play: true, lockOn: Enemy);
			p.RareSummon(1.85, spec);
			float tall = (JokerFeet - Pose(815f, 120f)).Length() * 1.35f;
			p.At(1.85, () => Grip.Play(p._front, 1ul, Persona.ChestOf(spec, JokerFeet, tall) + new Vector2(0.22f * tall, -0.12f * tall), Enemy, 170f,
				0.04f + SummonStage.EvokeAt + spec.AttackAt, 1f, c => p._enemies[Enemy].Modulate = c, ownImpact: true));
		},

		["damage"] = p =>
		{
			p.Strike(0.3, Enemy, 7);
			p.Strike(1.5, Enemy, 24);
			p.Shot(2.6, Enemy2, 13);
			p.Strike(3.6, Enemy, 158);
		},
		["multihit"] = p =>
		{
			p.At(0.3, () => p._joker?.Play("attack"));
			for (int i = 0; i < 4; i++)
			{
				p.Hit(0.45 + 0.18 * i, Enemy, 4, false);
			}
			for (int i = 0; i < 3; i++)
			{
				p.Shot(1.9 + 0.45 * i, Enemy, 9);
			}
		},
		// 开枪动作：选中小刀牌举枪瞄准 → 打出去 → 收枪；再来一张直接开枪（没先瞄准）
		// 开枪：选中小刀牌（转枪出枪、瞄准）→ 打出去 → 转枪收走；直接开一枪；转枪转到一半就打出（接着转完开火，p5mod/src/Aim.cs）
		["gun"] = p =>
		{
			p.At(0.3, () => { p._joker?.Play("aim_in"); p._joker?.Queue("aim_loop", true); });
			p.At(1.3, () => p._joker?.Play("shiv_from_aim"));
			p.At(1.48, () => HitFx.Muzzle(p._front, Muzzle));
			p.Hit(1.5, Enemy, 6, true);
			p.Shot(2.5, Enemy, 6);
			p.At(3.6, () => { p._joker?.Play("aim_in"); p._joker?.Queue("aim_loop", true); });
			float skip = 0f;
			p.At(3.67, () => { skip = p._joker!.Time; p._joker.PlayFrom("shiv", skip); p.At(0.18 - skip, () => HitFx.Muzzle(p._front, Muzzle)); });
			p.Hit(3.87, Enemy, 6, true);
		},
		// 打击转刀：直接打一刀；选中打击牌转刀起手停住 → 打出；转刀转到一半就打出（接着转完再砍）
		["knife"] = p =>
		{
			p.Strike(0.3, Enemy, 7);
			p.At(1.6, () => { p._joker?.Play("knife_in"); p._joker?.Queue("knife_loop", true); });
			p.At(2.6, () => p._joker?.Play("attack_from_ready"));
			p.At(2.73, () => HitFx.Swing(p._front, Pose(760f, 260f), Pose(600f, 850f), Pose(1376f, 450f)));
			p.Hit(2.75, Enemy, 12, false);
			p.At(3.8, () => { p._joker?.Play("knife_in"); p._joker?.Queue("knife_loop", true); });
			float skip = 0f;
			p.At(3.85, () => { skip = p._joker!.Time; p._joker.PlayFrom("attack", skip);
				p.At(Math.Max(0, 0.13 - skip), () => HitFx.Swing(p._front, Pose(760f, 260f), Pose(600f, 850f), Pose(1376f, 450f))); });
			p.Hit(4.0, Enemy, 9, false);
		},
		// 第一批出牌特效（拿起 → 打出）：防御（光盾）→ 中和（斩 + 虚弱锁环）→ 致命毒药（咒怨）
		["cards"] = p =>
		{
			p.Ready(0.2, Stance.Guard, "guard", "cast_guard_from_ready", play: true);
			p.At(1.83, () => CardFx.Guard(p._front, Pose(885f, 310f), Pose(1060f, 470f), 1280f * 0.87f * 0.29f));
			p.Ready(3.0, Stance.Knife, "knife", "attack_from_ready", play: true, lockOn: Enemy);
			p.At(3.0 + 1.6 + 0.13, () => HitFx.Swing(p._front, Pose(760f, 260f), Pose(600f, 850f), Pose(1376f, 450f)));
			p.Hit(3.0 + 1.6 + 0.15, Enemy, 4, false);
			p.At(3.0 + 1.6, () => CardFx.Weaken(p._front, Enemy, Enemy + new Vector2(0f, -170f), 0.12f));
			p.Ready(6.4, Stance.Mask, "mask", "cast_from_mask", play: true, lockOn: Enemy2);
			p.At(6.4 + 1.64, () => { SummonStage.Play(p._back, p._front, Pose(1010, 215)); CutIn.Eyes(p._back); });
			p.At(6.4 + 1.6, () => CardFx.Eiha(p._front, Pose(1010f, 215f), Enemy2, Enemy2 + new Vector2(0f, -128f), c => p._enemies[Enemy2].Modulate = c));
		},
		// 腐蚀波（稀有，但按罕见召唤）：亚森现身 → 亚森把咒怨砸进 Joker 身前的地面 → 脚下咒怨翻腾 → 抽一张、再抽一张、连抽三张：咒火浪贴地涌向敌人
		["corrosive_v4"] = p =>
		{
			// 用户 2026-11-03：腐蚀波朴素一些，不要眼神特写和舞台 → 按罕见召唤（Tiers.PlainRare）：亚森直接现身出手
			Persona.Spec spec = Persona.ArseneSpec;
			float hit = 0.04f + spec.AttackAt;
			bool active = false;
			float tall = (JokerFeet - Pose(815f, 120f)).Length() * 1.35f;
			p.At(0.3, () => p._joker?.Play("cast"));
			p.At(0.34, () => { Sfx.Play(Sfx.PersonaFlame, 0.8f); Persona.Show(spec, p._back, JokerFeet, tall); });
			Vector2 ground = JokerFeet + new Vector2(130f, 0f);
			p.At(0.3 + hit + 0.01, () =>
			{
				CorrosiveFx.Plunge(p._front, ArseneFx.TryClaw(out Vector2 claw) ? claw : ground + new Vector2(-200f, -420f), ground);
				active = true;
			});
			p.Every(() => { if (active) { CorrosiveFx.Aura(p._front, JokerFeet); } });
			var targets = new List<CorrosiveFx.Target>();
			p.At(0.1, () =>
			{
				foreach (Vector2 e in new[] { Enemy2, Enemy, Enemy3 })
				{
					p._enemies.TryGetValue(e, out Node2D? n);
					targets.Add(new CorrosiveFx.Target(e, e - new Vector2(0f, 120f), 230f, n));
				}
			});
			foreach (double t in new[] { 2.6, 3.4, 4.3, 4.32, 4.34 })
			{
				p.At(t, () => CorrosiveFx.OnDraw(p._front, JokerFeet, 1f, targets));
			}
		},
		// 毒性爆发（稀有）：完整召唤 + 伊邪那岐大神转杖 + 万物真言 + 咒怨毒火，打全体
		["outbreak"] = p =>
		{
			// 毒性爆发（金卡，两拍）：拍 1 召唤亚森 → 手从深处伸到敌人身上虚握 → 拍 2 亚森出手 = 所有手捏爆（= 结算 + 第一轮毒伤）→ 触媒再 2 轮
			Persona.Spec spec = Persona.ArseneSpec with { First = 10, Linger = 1.8f };
			float tall = (JokerFeet - Pose(815f, 120f)).Length() * 1.35f;
			p.Ready(0.25, Stance.Mask, "mask", "cast_from_mask", play: true);
			p.RareSummon(1.85, spec);
			float hit = 0.04f + SummonStage.EvokeAt + spec.AttackAt;
			var es = new[] { (Enemy, 170f, 1ul, 270f), (Enemy2, 128f, 2ul, 202f), (Enemy3, 128f, 3ul, 216f) };
			p.At(1.85, () => DepthGrip.Play(p._front, p._back, Persona.ChestOf(spec, JokerFeet, tall) + new Vector2(0.22f * tall, -0.12f * tall),
				Array.ConvertAll(es, e => (e.Item3, e.Item1, e.Item2, e.Item4, (Action<Color>?)(c => p._enemies[e.Item1].Modulate = c))), hit - DepthGrip.CrushDelay));
			for (int r = 0; r < 3; r++)
			{
				foreach (var (e, hh, id, _) in es)
				{
					int rr = r;
					p.At(1.85 + hit + 0.3 * rr + 0.02 * id, () => { DepthGrip.OnTick(id); Popups.Damage(p._front, id, e, 9 - rr, false); });
				}
			}
		},



		// 人格面具之手（罕见单体毒牌）：召唤亚森 → 伸手攥住 → 模拟触媒结算 3 轮毒伤，每轮再捏一下 → 松开缩回
		["grip"] = p =>
		{
			Persona.Spec spec = Persona.ArseneSpec with { Linger = 1.6f };
			float tall = (JokerFeet - Pose(815f, 120f)).Length() * 1.35f;
			p.Ready(0.25, Stance.Mask, "mask", "cast_from_mask", play: true, lockOn: Enemy);
			p.At(1.89, () =>
			{
				CutIn.SplitAt = spec.AttackAt - 0.05f;
				SummonStage.Play(p._back, p._front, Pose(1010, 215));
				CutIn.Eyes(p._back);
				Persona.Show(spec, p._back, JokerFeet, tall);
			});
			p.At(1.89 + spec.AttackAt, () => SummonStage.Beat(p._front, Persona.ChestOf(spec, JokerFeet, tall), p._back));
			p.At(1.85, () => Grip.Play(p._front, 1ul, Persona.ChestOf(spec, JokerFeet, tall) + new Vector2(0.22f * tall, -0.12f * tall), Enemy, 170f,
				0.04f + spec.AttackAt, 1f, c => p._enemies[Enemy].Modulate = c));
			for (int i = 0; i < 3; i++)
			{
				p.At(1.89 + spec.AttackAt + 0.35 + 0.3 * i, () => { Grip.OnTick(1ul); Popups.Damage(p._front, 1ul, Enemy, 9, false); });
			}
		},
		// 致命毒药（普通白卡，Tiers.Common）：选中 → 打出，不召唤，精简咒怨
		["poison"] = p =>
		{
			p.Ready(0.2, Stance.Mask, "mask", "cast_from_mask", play: true, lockOn: Enemy);
			// 普通毒牌：亚森 + 手捏，不出舞台 / 眼神特写（用户 2026-11-01）
			Persona.Spec spec = Persona.ArseneSpec with { Linger = 1.1f };
			float tall = (JokerFeet - Pose(815f, 120f)).Length() * 1.35f;
			p.At(1.89, () => { Sfx.Play(Sfx.PersonaFlame, 0.8f); Persona.Show(spec, p._back, JokerFeet, tall); });   // 同 Aim：没有舞台时自己放蓝焰声
			p.At(1.85, () => Grip.Play(p._front, 1ul, Persona.ChestOf(spec, JokerFeet, tall) + new Vector2(0.22f * tall, -0.12f * tall), Enemy, 170f,
				0.04f + spec.AttackAt, 1f, c => p._enemies[Enemy].Modulate = c, ownImpact: true));
		},
		// 防御（设计说明 docs/p5/FX_DESIGN_HITS.md 方案 A：菱形光格）：选中防御牌 → 打出，再来一次
		["guard"] = p =>
		{
			p.Ready(0.2, Stance.Guard, "guard", "cast_guard_from_ready", play: true);
			p.At(0.2 + 1.63, () => CardFx.Guard(p._front, Pose(885f, 310f), Pose(1060f, 470f), 1280f * 0.87f * 0.29f));
			p.Ready(3.0, Stance.Guard, "guard", "cast_guard_from_ready", play: true);
			p.At(3.0 + 1.63, () => CardFx.Guard(p._front, Pose(885f, 310f), Pose(1060f, 470f), 1280f * 0.87f * 0.29f));
		},
		// 打击（设计说明 docs/p5/FX_DESIGN_HITS.md）：直接砍一刀 → 选中转刀 + 锁定再砍 → 连砍三下（只有第一下顿帧）
		["strike"] = p =>
		{
			p.Strike(0.3, Enemy, 7);
			p.Ready(1.4, Stance.Knife, "knife", "attack_from_ready", play: true, lockOn: Enemy);
			p.At(1.4 + 1.6 + 0.13, () => HitFx.Swing(p._front, Pose(760f, 260f), Pose(600f, 850f), Pose(1376f, 450f)));
			p.Hit(1.4 + 1.6 + 0.15, Enemy, 12, false);
			p.Strike(4.2, Enemy2, 5);
			p.Strike(4.75, Enemy2, 5);
			p.Strike(5.3, Enemy2, 5);
		},
		// 2026-11-01 P5 平面风重做：打击（选中转刀 + 锁定 → 砍）、开枪（瞄准 + 锁定 → 开火）、防御
		["hits"] = p =>
		{
			p.Ready(0.2, Stance.Knife, "knife", "attack_from_ready", play: true, lockOn: Enemy);
			p.At(0.2 + 1.6 + 0.13, () => HitFx.Swing(p._front, Pose(760f, 260f), Pose(600f, 850f), Pose(1376f, 450f)));
			p.Hit(0.2 + 1.6 + 0.15, Enemy, 6, false);
			p.Ready(2.6, Stance.Gun, "aim", "shiv_from_aim", play: true, lockOn: Enemy2);
			p.At(2.6 + 1.6 + 0.18, () => HitFx.Muzzle(p._front, Muzzle));
			p.Hit(2.6 + 1.6 + 0.2, Enemy2, 6, true);
			p.Ready(5.0, Stance.Guard, "guard", "cast_guard_from_ready", play: true);
			p.At(5.0 + 1.63, () => CardFx.Guard(p._front, Pose(885f, 310f), Pose(1060f, 470f), 1280f * 0.87f * 0.29f));
		},
		// 只看蓝焰墙本身（放大，对照用户的参考图调细节）
		["eiha"] = p => p.At(0.3, () => CardFx.Eiha(p._front, Pose(1010f, 215f), Enemy, Enemy + new Vector2(0f, -170f), c => p._enemies[Enemy].Modulate = c)),
		["arsene"] = p => p.At(0.1, () => Persona.Arsene(p._back, JokerFeet, (JokerFeet - Pose(815f, 120f)).Length() * 1.35f)),
		["cutin"] = p => p.At(0.3, () => CutIn.Eyes(p._back)),
		// 毒性爆发的 3D 巨手（CrushHand）：四个瞬间 2×2 并排看造型 —— 远处、冲到眼前张开、抓住、攥拳
		["hand3d"] = p => p.At(0.05, () =>
		{
			// 对照用户拍的照片：张开 = 掌心正对镜头、手臂笔直伸进画面（藏在手后面）；攥拳 = 手腕往前扣，拳面朝镜头，拇指从右下横压过来
			(Vector3 pos, float tilt, float roll, float curl, float thumb, float spread, float grip)[] st =
			{
				(new Vector3(0f, -0.25f, 4.6f), -0.15f, 0.1f, 0f, 0f, 1.4f, 0f),
				(new Vector3(0.05f, -0.8f, 2.6f), -0.15f, 0.1f, 0.05f, 0f, 1.4f, 0f),
				(new Vector3(0.05f, -0.6f, 2.8f), -0.6f, 0.2f, 0.5f, 0.4f, 1.0f, 0.3f),
				(new Vector3(0.05f, -0.3f, 2.45f), -1.1f, 0.3f, 1f, 1f, 0.9f, 1f),
			};
			for (int i = 0; i < 4; i++)
			{
				CrushHand h = new(p._front, new Vector2(960f * (i % 2), 540f * (i / 2)), new Vector2(960f, 540f));
				h.Set("hand_pos", st[i].pos); h.Set("tilt", st[i].tilt); h.Set("roll", st[i].roll); h.Set("curl", st[i].curl);
				h.Set("thumb", st[i].thumb); h.Set("spread", st[i].spread); h.Set("grip", st[i].grip);
				h.Set("elbow", st[i].pos + new Vector3(-0.3f, -0.6f, 6f));
				ColorRect bg = new() { Color = new Color(0.55f, 0.52f, 0.5f), Size = new Vector2(960f, 300f), Position = new Vector2(960f * (i % 2), 540f * (i / 2) + 110f) };
				p._front.AddChild(bg); p._front.MoveChild(bg, h.Rect.GetIndex());          // 假的横幅：看手从横幅里穿出来
			}
		}),
		["cutindbg"] = p =>
		{
			p.At(0.3, () => CutIn.Eyes(p._back));
			p.At(0.6, () =>
			{
				foreach (Node n in p._back.GetChildren())
				{
					if (n.Name.ToString().StartsWith("P5CutIn"))
					{
						int i = 0;
						foreach (Node c in n.GetChildren())
						{
							GD.Print($"[DBG] child {i++} {c.GetType().Name} vis={(c as CanvasItem)?.Visible} pos={(c as Node2D)?.Position} scale={(c as Node2D)?.Scale} kids={c.GetChildCount()} mod={(c as CanvasItem)?.Modulate}");
						}
					}
				}
			});
		},
		["wall"] = p =>
		{
			PersonaMask? w = null;
			p.At(0.1, () => w = new PersonaMask(p._back, p._front, new Vector2(960f, 600f), 360f));
			p.At(3.2, () => w?.End(true));
		},
		// 刀扇：一发子弹弹过所有敌人（近 → 远）
		["ricochet"] = p =>
		{
			p.At(0.3, () => p._joker?.Play("shiv"));
			p.At(0.48, () => HitFx.Muzzle(p._front, Muzzle));
			p.At(0.5, () => HitFx.Ricochet(p._front, Muzzle, new[] { Enemy2, Enemy, Enemy3 }));
			p.At(1.5, () => p._joker?.Play("shiv"));
			p.At(1.68, () => HitFx.Muzzle(p._front, Muzzle));
			p.At(1.7, () => HitFx.Ricochet(p._front, Muzzle, new[] { Enemy2, Enemy, Enemy3 }));
		},
		// 小刀弹药：装 3 → 开一枪 → 装到 8（只显示 6）→ 开两枪（还剩 6：打掉一发转一格、马上补上）→ 全部弃掉
		["ammo"] = p =>
		{
			AmmoView? view = null;
			p.At(0.1, () => { view = new AmmoView(p._front, 220f); view.Root.Position = new Vector2(960f, 520f); });
			p.Every(() => view?.Tick(1f / 30f));
			p.At(0.4, () => view?.Set(3));
			p.At(1.2, () => view?.Set(2));
			p.At(1.38, () => view?.Fire());
			p.At(2.0, () => view?.Set(8));
			p.At(2.8, () => view?.Fire());
			p.At(3.3, () => view?.Fire());
			p.At(4.0, () => view?.Set(0));
		},
		// 预备出牌（拖着牌选目标）：每种架势摆好、特效循环一会儿，再打出 / 放回（p5mod/src/Aim.cs、Vfx/Ready.cs）
		["ready"] = p =>
		{
			p.Ready(0.2, Stance.Mask, "mask", "cast_from_mask", play: true, lockOn: Enemy);
			p.Ready(2.4, Stance.Guard, "guard", "cast_guard_from_ready", play: true);
			p.Ready(4.4, Stance.Glove, "glove", "cast_glove_from_ready", play: true);
		},
		["ready2"] = p =>
		{
			p.Ready(0.2, Stance.Knife, "knife", "attack_from_ready", play: true, lockOn: Enemy);
			p.At(0.2 + 1.6 + 0.13, () => HitFx.Swing(p._front, Pose(760f, 260f), Pose(600f, 850f), Pose(1376f, 450f)));
			p.Hit(0.2 + 1.6 + 0.15, Enemy, 6, false);
			p.Ready(2.8, Stance.Gun, "aim", "shiv_from_aim", play: true, lockOn: Enemy2);
			p.At(2.8 + 1.6 + 0.18, () => HitFx.Muzzle(p._front, Muzzle));
			p.Hit(2.8 + 1.6 + 0.2, Enemy2, 6, true);
			p.Ready(5.3, Stance.Mask, "mask", "", play: false);
		},
		// 选中打击牌（转刀起手停住）→ 取消（放下手、转刀收起）
		["knifecancel"] = p =>
		{
			p.At(0.3, () => { p._joker?.Play("knife_in"); p._joker?.Queue("knife_loop", true); });
			p.At(1.3, () => p._joker?.Play("knife_out"));
		},
		// 腐蚀波（稀有，但按罕见召唤）：亚森现身 → 亚森把咒怨砸进 Joker 身前的地面 → 脚下咒怨翻腾 → 抽一张、再抽一张、连抽三张：咒火浪贴地涌向敌人
		["corrosive_v4"] = p =>
		{
			// 用户 2026-11-03：腐蚀波朴素一些，不要眼神特写和舞台 → 按罕见召唤（Tiers.PlainRare）：亚森直接现身出手
			Persona.Spec spec = Persona.ArseneSpec;
			float hit = 0.04f + spec.AttackAt;
			bool active = false;
			float tall = (JokerFeet - Pose(815f, 120f)).Length() * 1.35f;
			p.At(0.3, () => p._joker?.Play("cast"));
			p.At(0.34, () => { Sfx.Play(Sfx.PersonaFlame, 0.8f); Persona.Show(spec, p._back, JokerFeet, tall); });
			Vector2 ground = JokerFeet + new Vector2(130f, 0f);
			p.At(0.3 + hit + 0.01, () =>
			{
				CorrosiveFx.Plunge(p._front, ArseneFx.TryClaw(out Vector2 claw) ? claw : ground + new Vector2(-200f, -420f), ground);
				active = true;
			});
			p.Every(() => { if (active) { CorrosiveFx.Aura(p._front, JokerFeet); } });
			var targets = new List<CorrosiveFx.Target>();
			p.At(0.1, () =>
			{
				foreach (Vector2 e in new[] { Enemy2, Enemy, Enemy3 })
				{
					p._enemies.TryGetValue(e, out Node2D? n);
					targets.Add(new CorrosiveFx.Target(e, e - new Vector2(0f, 120f), 230f, n));
				}
			});
			foreach (double t in new[] { 2.6, 3.4, 4.3, 4.32, 4.34 })
			{
				p.At(t, () => CorrosiveFx.OnDraw(p._front, JokerFeet, 1f, targets));
			}
		},
		// 毒性爆发（稀有）：眼神特写 + 舞台 + 新亚森 → 出手，咒怨光束扇形射向三个敌人（各 9 层）→ 立即触发中毒：全场咒火一起引爆
		// 毒性爆发巨手版（同 p5mod/src/Aim.cs OutbreakHand）：眼神特写 → 亚森现身 → 亚森出手 = 巨手从横幅冲出来 → 张开 → 攥拳（敌人被标记）→ 颤抖 → 捏碎 = 横幅碎开 = 全体引爆
		["outbreak_hand"] = p =>
		{
			Persona.Spec spec = Persona.ArseneSpec with { First = 18, Linger = CrushHand.CrushAfter + 0.5f };   // 同 Aim.PersonaFor("Outbreak")
			float emerge = 0.3f + spec.AttackAt;                                                  // 同 Aim.OutbreakEmerge
			float hit = 0.04f + emerge + CrushHand.CrushAfter;                                    // 同 Aim.HitFor
			float tall = (JokerFeet - Pose(815f, 120f)).Length() * 1.35f;
			Vector2[] es = { Enemy2, Enemy, Enemy3 };
			p.At(0.3, () => { p._joker?.Play("cast_big"); PoisonFx.BeginOutbreak(hit + 1.5f); });
			p.At(0.34, () =>
			{
				CutInPlayback clip = new(emerge + CrushHand.CrushAfter, crush: true);
				clip.Start();
				SummonStage.Play(p._back, p._front, Pose(1010, 215), maskFlame: false, playback: clip);
				CutIn.Eyes(p._back, clip);
				CrushHand.Outbreak(p._back, emerge, onFist: () =>
				{
					var targets = new List<PoisonFx.Target>();
					foreach (Vector2 e in es)
					{
						p._enemies.TryGetValue(e, out Node2D? n);
						targets.Add(new PoisonFx.Target(e, e - new Vector2(0f, 120f), 230f, n));
					}
					PoisonFx.Squeeze(p._front, targets);
				}, onCrush: () =>
				{
					CrushWave.Play(p._back, clip.ShatterFrom ?? Fx.Screen(p._back) * 0.5f);
					foreach (Vector2 e in es)
					{
						p._enemies.TryGetValue(e, out Node2D? n);
						PoisonFx.Tick(p._front, e, e - new Vector2(0f, 120f), 230f, n, outbreak: true);
						Popups.Damage(p._front, (ulong)e.X, e, 9, false);
					}
				}, playback: clip);
				Fx.After(p._back, 0.3, () =>
				{
					Sfx.Play(Sfx.PersonaFlame, 0.5f);
					ArseneFx.NextClench = (spec.AttackAt + CrushHand.FistAfter, spec.AttackAt + CrushHand.CrushAfter);
					Persona.Show(spec, p._back, JokerFeet, tall, playback: clip, clockOffset: 0.3f);
				});
			});
		},
		["outbreak_v4"] = p =>
		{
			Persona.Spec spec = Persona.ArseneSpec with { First = 10, Linger = 1.8f };   // 同 Aim.PersonaFor("Outbreak")
			float hit = 0.04f + SummonStage.EvokeAt + spec.AttackAt;                    // 同 Aim.HitFor
			p.At(0.3, () => { p._joker?.Play("cast_big"); PoisonFx.BeginOutbreak(hit + 1.5f); });
			p.RareSummon(0.3, spec);
			p.Poison(0.3 + hit, Enemy2, 9);
			p.Poison(0.3 + hit + 0.01, Enemy, 9);
			p.Poison(0.3 + hit + 0.02, Enemy3, 9);
			foreach ((Vector2 e, double dt) in new[] { (Enemy2, 0.04), (Enemy, 0.05), (Enemy3, 0.06) })
			{
				p.At(0.3 + hit + dt, () =>
				{
					p._enemies.TryGetValue(e, out Node2D? n);
					PoisonFx.Tick(p._front, e, e - new Vector2(0f, 120f), 230f, n);
					Fx.After(p._front, PoisonFx.BlastWait(), () => Popups.Damage(p._front, (ulong)e.X, e, 9, false));   // 同 DamageNumberPatch
				});
			}
		},
		// 第四版毒组 + 新亚森：致命毒药（亚森现身 → 出手把咒怨甩过去 → 炸开）→ 弹跳药瓶（亚森出手，三跳）→ 带毒刺击（不召唤，只有落点）→ 一轮毒伤
		["arsene_poison"] = p =>
		{
			float height = (JokerFeet - Pose(815f, 120f)).Length() * 1.35f;
			float atk = Persona.ArseneSpec.AttackAt;
			p.At(0.3, () => p._joker?.Play("cast"));
			p.At(0.34, () => ArseneFx.Show(p._back, JokerFeet, height, 1f, atk, atk + 1.1f));
			p.Poison(0.34 + atk, Enemy, 5);
			p.At(2.4, () => p._joker?.Play("cast"));
			p.At(2.44, () => ArseneFx.Show(p._back, JokerFeet, height, 1f, atk, atk + 1.3f));
			p.Poison(2.44 + atk, Enemy2, 3);
			p.Poison(2.44 + atk + 0.2, Enemy3, 3);
			p.Poison(2.44 + atk + 0.4, Enemy, 3);
			p.StrikeV4(4.6, Enemy2, 6, Weight.Medium, false);
			p.Poison(4.78, Enemy2, 3);
			foreach ((Vector2 e, double dt) in new[] { (Enemy2, 0.0), (Enemy, 0.3), (Enemy3, 0.6) })
			{
				p.At(5.9 + dt, () =>
				{
					p._enemies.TryGetValue(e, out Node2D? n);
					PoisonFx.Tick(p._front, e, e - new Vector2(0f, 120f), 230f, n);
				});
			}
		},
		// 第四版毒组：致命毒药（5 层）→ 弹跳药瓶（3 跳随机）→ 一次加 9 层 → 回合开始三个敌人各结算一轮毒伤
		["poison_v4"] = p =>
		{
			p.At(0.2, () => p._joker?.Play("cast"));
			p.Poison(0.55, Enemy, 5);
			p.At(1.6, () => p._joker?.Play("cast"));
			p.Poison(1.9, Enemy2, 3);
			p.Poison(2.12, Enemy3, 3);
			p.Poison(2.34, Enemy, 3);
			p.At(3.2, () => p._joker?.Play("cast"));
			p.Poison(3.5, Enemy3, 9);
			foreach ((Vector2 e, double dt) in new[] { (Enemy2, 0.0), (Enemy, 0.35), (Enemy3, 0.7) })
			{
				p.At(4.8 + dt, () =>
				{
					p._enemies.TryGetValue(e, out Node2D? n);
					PoisonFx.Tick(p._front, e, e - new Vector2(0f, 120f), 230f, n);
					Popups.Damage(p._front, (ulong)(e.X * 7f + (float)dt), e, 5, false);
				});
			}
		},
		// 原版火焰 / 毒 / 烟换咒怨 LUT 逐个看
		["curse_test"] = p =>
		{
			string[] all = { Vanilla.FireCenter, Vanilla.FireLeft, Vanilla.FireRight, Vanilla.Fire1, Vanilla.Fire2, Vanilla.Fire3, Vanilla.Embers,
				Vanilla.SmokeFlip, Vanilla.PoisonSkull, Vanilla.RoundSmoke };
			for (int i = 0; i < all.Length; i++)
			{
				string path = all[i];
				Vector2 at = new(200f + (i % 5) * 380f, 400f + (i / 5) * 420f);
				p.At(0.3, () => Vanilla.Burst(p._front, path, at, 0f, 1f, path == Vanilla.SmokeFlip || path == Vanilla.RoundSmoke ? Lut5.Smoke : Lut5.Curse));
			}
		},
		// 原版组件逐个看（换 P5 LUT）
		["vanilla_test"] = p =>
		{
			string[] all = { Vanilla.HitFlare, Vanilla.BigSlashCore, Vanilla.BigSlashSmoke, Vanilla.Specks, Vanilla.Streaks, Vanilla.SlashCore,
				Vanilla.CleanFlare, Vanilla.BluntCore, Vanilla.RoundSmoke, Vanilla.DaggerImpact };
			for (int i = 0; i < all.Length; i++)
			{
				string path = all[i];
				Vector2 at = new(200f + (i % 5) * 380f, 300f + (i / 5) * 420f);
				p.At(0.3, () => Vanilla.Burst(p._front, path, at, 0f, 1f, Lut5.Blood));
				p.At(1.3, () => Vanilla.Burst(p._front, path, at, 0f, 1f, Lut5.Blood));
			}
		},
		// 只放出刀动作、不放特效（量刀尖轨迹用）
		// 第四版枪组：一张小刀 → 三张小刀连打 → 刀扇（0.7.27 的弹射保留）→ 一枪打死
		["gun_v4"] = p =>
		{
			p.ShotV4(0.3, Enemy, 4, Weight.Light);
			for (int i = 0; i < 3; i++)
			{
				p.ShotV4(1.3 + 0.55 * i, Enemy2, 4, Weight.Light);
			}
			p.At(3.4, () => p._joker?.Play("shiv"));
			p.At(3.58, () => GunFx.Muzzle(p._front, Muzzle, new Vector2(1f, 0.06f)));
			p.At(3.6, () => HitFx.Ricochet(p._front, Muzzle, new[] { Enemy2, Enemy, Enemy3 }));
			p.ShotV4(4.7, Enemy3, 9, Impact.Grade(Weight.Light, 9, 9, true, false), dies: true);
		},
		// 原版怪物（research/sts2_preview_scene，tools/spine/skel_reader.py 转成 JSON）在预览里显示对不对
		// 弹跳烧瓶（同原版 BouncingFlask 的节奏：每跳扔出后 0.5s 落地上毒）：咒怨火球从亚森爪尖出手，三跳，最后一跳最大
		["flask"] = p =>
		{
			Persona.Spec spec = Persona.ArseneSpec with { Linger = 1.7f };   // 同 Aim.PersonaFor("BouncingFlask")
			float hit = 0.04f + spec.AttackAt;
			float tall = (JokerFeet - Pose(815f, 120f)).Length() * 1.35f;
			p.At(0.3, () => { p._joker?.Play("cast"); Sfx.Play(Sfx.PersonaFlame, 0.8f); Persona.Show(spec, p._back, JokerFeet, tall); SnakeFx.BeginFlask(3); });
			Vector2[] hops = { Enemy2, Enemy3, Enemy };
			Vector2 from = Pose(820f, 600f);
			for (int i = 0; i < hops.Length; i++)
			{
				Vector2 a0 = i == 0 ? from : hops[i - 1], b0 = hops[i];
				double t0 = 0.3 + hit + 0.5 * i;
				p.At(t0, () => SnakeFx.Throw(p._front, a0, b0));
				p.Poison(t0 + 0.5, b0, 3);
			}
		},
		// 刀扇（同 Aim.PlayTwirl + JokerFx.OnPlay）：选中掏枪瞄准 → 打出转枪收起，指尖红光环，枪停下所有敌人闪锁定框
		["fan"] = p =>
		{
			p.At(0.2, () => { p._joker?.Play("aim_in"); p._joker?.Queue("aim_loop", true); });
			var es = new List<ShivFx.Target> { new(Enemy2, 180f), new(Enemy, 230f), new(Enemy3, 180f) };
			p.At(1.2, () => { p._joker?.Play("aim_out"); ShivFx.FanOfKnives(p._front, Pose(1275f, 345f), es); });
		},
		// 小刀流第二批（同 JokerFx.OnPlay）：子弹时间（转枪 + 慢波纹 + 四周压暗）→ 残影 → 精心策划（预告信）→ 隐形（暗烟 + 变暗）
		["batch2"] = p =>
		{
			p.At(0.3, () => { p._joker?.Play("aim_in"); p._joker?.Queue("aim_out", false); ShivFx.BulletTime(p._front, Chest); });
			p.At(1.7, () => p._joker?.Play("cast_glove"));
			p.At(1.9, () => ShivFx.Afterimage(p._back, parent => BodyAura.CloneBody!(parent), 1f));
			p.At(3.0, () => p._joker?.Play("cast_glove"));
			p.At(3.2, () => ShivFx.CallingCard(p._front, Pose(1010f, 410f)));
			p.At(4.3, () => p._joker?.Play("cast_glove"));
			p.At(4.48, () =>
			{
				float tall = (JokerFeet - Pose(815f, 120f)).Length();
				ShivFx.Shadowmeld(p._back, JokerFeet, tall, p._joker);
			});
		},
		["warmup"] = p => p.At(0.1, () => Warmup.Run(p._front, 1ul)),
		["monsters"] = p => { },
		["attack_pose"] = p => p.At(0.3, () => p._joker?.Play("attack")),
		// 出刀刀光单独看（和动作对齐）
		["swing"] = p =>
		{
			for (int i = 0; i < 3; i++)
			{
				double t = 0.3 + i * 1.0;
				p.At(t, () => p._joker?.Play("attack"));
				p.At(t + 0.13, () => KnifeFx.Swing(p._front, Pose(600f, 850f), Pose(1376f, 450f)));
			}
		},
		["hit_parts"] = p =>
		{
			float rot = -0.55f;
			p.At(0.3, () => Paint.Pop(p._front, "burst_01", new Vector2(300f, 400f), 0f, 0.46f, null, 0.04f, 0.16f, 1.4f));
			p.At(0.3, () => Paint.Sweep(p._front, "slash_thin", new Vector2(750f, 400f), rot, new Vector2(0.46f, 0.345f), Lut5.Blood, 0.05f, 0.05f, 0.24f, 40f));
			p.At(0.3, () => Vanilla.Burst(p._front, Vanilla.BigSlashCore, new Vector2(1150f, 400f), rot + Mathf.Pi * 0.5f, 0.9f, Lut5.Blood));
			p.At(0.3, () => Vanilla.Burst(p._front, Vanilla.BigSlashSmoke, new Vector2(1550f, 400f), rot, 0.85f, Lut5.Smoke));
			p.At(0.3, () => Vanilla.Burst(p._front, Vanilla.Specks, new Vector2(750f, 800f), rot, 1f, Lut5.Blood, amount: 22));
			p.At(0.3, () => Vanilla.Burst(p._front, Vanilla.Streaks, new Vector2(1150f, 800f), 0f, 0.95f, Lut5.Blood));
		},
		["smoke_test"] = p =>
		{
			p.At(0.3, () => Vanilla.Burst(p._front, Vanilla.BigSlashSmoke, new Vector2(500f, 500f), -0.55f, 0.85f, Lut5.Smoke));
			p.At(0.3, () => Vanilla.Burst(p._front, Vanilla.BigSlashSmoke, new Vector2(1000f, 500f), 0f, 1f, Lut5.Smoke));
			p.At(0.3, () => Vanilla.Burst(p._front, Vanilla.BigSlashSmoke, new Vector2(1500f, 500f), -0.55f, 0.85f, Lut5.Blood));
			p.At(0.3, () => Paint.Sweep(p._front, "slash_thin", new Vector2(1000f, 850f), -0.55f, new Vector2(0.46f, 0.35f), Lut5.Blood));
		},
		// 第四版刀组：手绘刀光（扫出 → 溶解）+ 原版命中粒子换 P5 配色 + 漫画爆框；普通一刀 → 再一刀 → 重斩 → 打死
		["knife_v4"] = p =>
		{
			p.StrikeV4(0.3, Enemy, 7, Weight.Medium, false);
			p.StrikeV4(1.4, Enemy, 8, Weight.Medium, false);
			p.StrikeV4(2.6, Enemy, 24, Weight.Heavy, true);
			p.StrikeV4(4.0, Enemy2, 30, Weight.Kill, true, dies: true);
		},
		// 打击确认感（Vfx/Impact.cs）的分档，开枪（只有小刀牌开枪；投掷匕首等是打击动作）：一张小刀（轻）→ 三张小刀连着打（每张轻）→ 刀扇一发弹过三个敌人（群攻轻，整组只顿一次）
		// → 一枪打出 14 点（升到中）→ 一枪打死一个（击杀）
		["confirm_gun"] = p =>
		{
			p.ShotC(0.3, Enemy, 4, Weight.Light);
			for (int i = 0; i < 3; i++)
			{
				p.ShotC(1.3 + 0.5 * i, Enemy, 4, Weight.Light);
			}
			p.At(3.3, () => p._joker?.Play("shiv"));
			p.At(3.48, () => HitFx.Muzzle(p._front, Muzzle));
			p.At(3.5, () => HitFx.Ricochet(p._front, Muzzle, new[] { Enemy2, Enemy, Enemy3 }));
			foreach ((Vector2 e, double dt) in new[] { (Enemy2, 0.05), (Enemy, 0.1), (Enemy3, 0.15) })
			{
				p.HitC(3.5 + dt, e, 4, Weight.Light, false, gun: true);
			}
			p.ShotC(4.6, Enemy, 14, Impact.Grade(Weight.Light, 14, 40, false, false));
			p.ShotC(5.7, Enemy3, 9, Impact.Grade(Weight.Light, 9, 9, true, false), dies: true);
		},
		// 打击确认感的分档，刀：连斩（前几下轻、最后一下中）→ 普通一刀（中）→ 大招（重）→ 1 秒内第二次重击（不再红黑帧）
		// → 打死一个（击杀）→ 打死最后一个（战斗结束）
		["confirm"] = p =>
		{
			for (int i = 0; i < 4; i++)
			{
				p.HitC(0.3 + 0.16 * i, Enemy, 4, Impact.Grade(i < 3 ? Weight.Light : Weight.Medium, 4, 60, false, false, i < 3), i == 0);
			}
			p.HitC(1.5, Enemy, 7, Impact.Grade(Weight.Medium, 7, 50, false, false), true);
			p.HitC(2.5, Enemy, 18, Impact.Grade(Weight.Heavy, 18, 40, false, false), true);
			p.HitC(3.1, Enemy, 27, Impact.Grade(Weight.Medium, 27, 30, false, false), true);
			p.HitC(4.1, Enemy3, 20, Impact.Grade(Weight.Medium, 20, 20, true, false), true, dies: true);
			p.HitC(5.2, Enemy2, 40, Impact.Grade(Weight.Heavy, 40, 30, true, true), true, dies: true);
		},
		// 防御（用户 2026-11-08）：盾墙挡在身前 → 缩成小盾飞进血条上的格挡数字。普通一次、格挡 10 点以上一次
		["guard_wall"] = p =>
		{
			float tall = (JokerFeet - Pose(815f, 120f)).Length();
			Vector2 block = JokerFeet + new Vector2(-70f, 40f);
			p.Ready(0.2, Stance.Guard, "guard", "cast_guard_from_ready", play: true);
			p.At(0.2 + 1.63, () => GuardWall.Play(p._front, Pose(885f, 310f), tall, 1f, 5, block));
			p.Ready(2.6, Stance.Guard, "guard", "cast_guard_from_ready", play: true);
			p.At(2.6 + 1.63, () => GuardWall.Play(p._front, Pose(885f, 310f), tall, 1f, 12, block));
		},
		// 防御（用户 2026-11-08）：照结城理 mod 的六边形蜂巢护盾（着色器版）。普通一次、格挡 10 点以上一次
		["hex_guard"] = p =>
		{
			float tall = (JokerFeet - Pose(815f, 120f)).Length();
			p.Ready(0.2, Stance.Guard, "guard", "cast_guard_from_ready", play: true);
			p.At(0.2 + 1.63, () => HexGuard.Play(p._front, Chest, tall, 1f, 5, Pose(885f, 310f)));
			p.Ready(2.8, Stance.Guard, "guard", "cast_guard_from_ready", play: true);
			p.At(2.8 + 1.63, () => HexGuard.Play(p._front, Chest, tall, 1f, 12, Pose(885f, 310f)));
		},
		// 华丽收场 = 大罪穿甲弹（用户 2026-11-08）：场景 + 子弹预览，撒旦耶尔第 2 版立绘还没到，枪口先放在 Joker 身后上方
		["sinful_shell"] = p =>
		{
			// 选中华丽收场：扶面具的架势（面具蓝焰）→ 打出：攥面具、手顺着弧线放下直接转枪（cast_sinful_from_mask）；面具不再冲出两道蓝焰（用户 2026-11-09）
			p.Ready(0.0, Stance.Mask, "mask", "cast_sinful_from_mask", play: true);
			p.At(1.6, () => SinfulShell.Play(p._back, p._front, new SinfulShell.Setup
			{
				JokerFeet = JokerFeet, JokerHead = Pose(1010f, 215f), JokerMuzzle = Muzzle, PersonaMuzzle = new Vector2(720f, 380f), FloorY = 830f,
				Enemies = new List<Vector2> { Enemy2, Enemy, Enemy3 },
				OnJokerShoot = () => p._joker?.Play("shiv_from_aim"),
				OnPierce = i => Popups.Damage(p._front, (ulong)(i + 1), new[] { Enemy2, Enemy, Enemy3 }[i], 50, true),
				Actors = new List<CanvasItem> { p._joker!, p._enemies[Enemy], p._enemies[Enemy2], p._enemies[Enemy3] },
			}));
		},
		["block"] = p =>
		{
			p.EnemyAttack(0.3, Enemy);
			p.At(0.42, () => Popups.Block(p._front, Chest));
			for (int i = 0; i < 3; i++)
			{
				p.EnemyAttack(1.8 + 0.3 * i, Enemy);
				p.At(1.92 + 0.3 * i, () => Popups.Block(p._front, Chest));
			}
		},
	};

	/// <summary>稀有召唤（同 Aim.PlayCast 的稀有分支）：t 时打出，0.04s 后舞台 + 眼神特写，EvokeAt 拍 1（面具喷光、特写碎开、人格面具现身）。</summary>
	public void RareSummon(double t, Persona.Spec spec)
	{
		float tall = (JokerFeet - Pose(815f, 120f)).Length() * 1.35f;
		At(t + 0.04, () =>
		{
			CutIn.SplitAt = SummonStage.EvokeAt - 0.05f;
			SummonStage.Play(_back, _front, Pose(1010, 215));
			CutIn.Eyes(_back);
		});
		At(t + 0.04 + SummonStage.EvokeAt, () =>
		{
			SummonStage.Beat(_front, Pose(1010, 215), _back, 1f, Persona.ChestOf(spec, JokerFeet, tall));
			Persona.Show(spec, _back, JokerFeet, tall);
		});
	}

	// 和 p5mod/src/JokerFx.cs 一样的点和时刻
	private void Strike(double t, Vector2 enemy, int amount)
	{
		At(t, () => _joker?.Play("attack"));
		At(t + 0.13, () => HitFx.Swing(_front, Pose(760f, 260f), Pose(600f, 850f), Pose(1376f, 450f)));
		Hit(t + 0.15, enemy, amount, false);
	}

	private void StrikeV4(double t, Vector2 enemy, int amount, Weight w, bool heavy, bool dies = false)
	{
		At(t, () => _joker?.Play("attack"));
		At(t + 0.13, () => KnifeFx.Swing(_front, Pose(600f, 850f), Pose(1376f, 450f)));
		At(t + 0.15, () =>
		{
			KnifeFx.Hit(_front, enemy, 1f, heavy);
			Popups.Damage(_front, (ulong)(enemy.X * 10f + (float)t), enemy, amount, false);
			_enemies.TryGetValue(enemy, out Node2D? n);
			Impact.Confirm(_front, n, enemy, new Vector2(1f, 0.12f), w);
			if (dies && n != null)
			{
				n.CreateTween().TweenProperty(n, "modulate:a", 0f, 0.6).SetDelay(0.35);
			}
		});
	}

	private void Poison(double t, Vector2 enemy, int amount) => At(t, () =>
	{
		_enemies.TryGetValue(enemy, out Node2D? n);
		PoisonFx.Apply(_front, enemy, enemy - new Vector2(0f, 120f), 230f, n, amount);
	});

	private void ShotV4(double t, Vector2 enemy, int amount, Weight w, bool dies = false)
	{
		At(t - 0.2, () => _joker?.Play("shiv"));
		At(t - 0.02, () => GunFx.Muzzle(_front, Muzzle, new Vector2(1f, 0.06f)));
		At(t, () =>
		{
			GunFx.Shot(_front, Muzzle, enemy);
			Popups.Damage(_front, (ulong)(enemy.X * 10f + (float)t), enemy, amount, true);
			_enemies.TryGetValue(enemy, out Node2D? n);
			Impact.Confirm(_front, n, enemy, enemy - Muzzle, w);
			if (dies && n != null)
			{
				n.CreateTween().TweenProperty(n, "modulate:a", 0f, 0.6).SetDelay(0.35);
			}
		});
	}

	private void ShotC(double t, Vector2 enemy, int amount, Weight w, bool dies = false)
	{
		At(t - 0.2, () => _joker?.Play("shiv"));
		At(t - 0.02, () => HitFx.Muzzle(_front, Muzzle));
		At(t, () => HitFx.Shot(_front, Muzzle, enemy));
		HitC(t, enemy, amount, w, false, dies, gun: true);
	}

	private void HitC(double t, Vector2 enemy, int amount, Weight w, bool swing, bool dies = false, bool gun = false)
	{
		if (swing)
		{
			At(t - 0.15, () => _joker?.Play("attack"));
			At(t - 0.02, () => HitFx.Swing(_front, Pose(760f, 260f), Pose(600f, 850f), Pose(1376f, 450f)));
		}
		At(t, () =>
		{
			if (!gun)
			{
				HitFx.Slash(_front, enemy);
			}
			Popups.Damage(_front, (ulong)(enemy.X * 10f + (float)t), enemy, amount, gun);
			_enemies.TryGetValue(enemy, out Node2D? n);
			Impact.Confirm(_front, n, enemy, gun ? (enemy - Muzzle) : new Vector2(1f, 0.12f), w);
			if (dies && n != null)
			{
				n.CreateTween().TweenProperty(n, "modulate:a", 0f, 0.6).SetDelay(0.35);
			}
		});
	}

	/// <summary>拿起一张牌：摆架势（anim_in → anim_loop）、特效循环 1.6s（0.5s 起指到 lockOn 那个敌人）→ 打出（playAnim）/ 放回（anim_out）。</summary>
	private new void Ready(double t, Stance stance, string anim, string playAnim, bool play, Vector2? lockOn = null)
	{
		ReadyFx? fx = null;
		LockOn? lk = null;
		At(t, () =>
		{
			_joker?.Play(anim + "_in");
			_joker?.Queue(anim + "_loop", true);
			fx = ReadyFx.Start(_front, _back, stance, v => Pose(v.X, v.Y));
		});
		if (lockOn is Vector2 e)
		{
			At(t + 0.5, () => { lk = LockOn.Start(_front, e, e == Enemy ? 230f : 180f); });
		}
		At(t + 1.6, () =>
		{
			lk?.Release();
			fx?.End(play);
			_joker?.Play(play ? playAnim : anim + "_out");
		});
	}

	private void Shot(double t, Vector2 enemy, int amount)
	{
		At(t, () => _joker?.Play("shiv"));
		At(t + 0.18, () => HitFx.Muzzle(_front, Muzzle));
		Hit(t + 0.2, enemy, amount, true);
	}

	public static Vector2 Muzzle => Pose(1347f, 306f);

	private void Hit(double t, Vector2 enemy, int amount, bool gun, bool fx = true) => At(t, () =>
	{
		if (!fx)
		{
		}
		else if (gun)
		{
			HitFx.Shot(_front, Muzzle, enemy);
		}
		else
		{
			HitFx.Slash(_front, enemy);
		}
		Popups.Damage(_front, enemy == Enemy ? 1ul : 2ul, enemy, amount, gun);
		if (_enemies.TryGetValue(enemy, out Node2D? n))
		{
			Tween tw = n.CreateTween();
			tw.TweenProperty(n, "position", enemy + new Vector2(28f, 0f), 0.04);
			tw.TweenProperty(n, "position", enemy, 0.25).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		}
	});

	/// <summary>敌人往左扑一下（0.12s 到最远），扑到的那一刻被 Joker 挡住。</summary>
	private void EnemyAttack(double t, Vector2 enemy) => At(t, () =>
	{
		if (_enemies.TryGetValue(enemy, out Node2D? n))
		{
			Tween tw = n.CreateTween();
			tw.TweenProperty(n, "position", enemy + new Vector2(-260f, 10f), 0.12).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
			tw.TweenProperty(n, "position", enemy, 0.35).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		}
	});

	public override void _Ready()
	{
		string scenario = "damage";
		foreach (string a in OS.GetCmdlineUserArgs())
		{
			if (a.StartsWith("--scenario="))
			{
				scenario = a["--scenario=".Length..];
			}
		}
		string texDir = ProjectSettings.GlobalizePath("res://.tex");
		Stage.LoadTexture = name =>
		{
			string path = $"{texDir}/{name}.png";
			if (!FileAccess.FileExists(path))
				path = ProjectSettings.GlobalizePath($"res://../../../p5mod/pck_stage/p5joker/fx/{name}.png");
			return FileAccess.FileExists(path) ? ImageTexture.CreateFromImage(Image.LoadFromFile(path)) : null;
		};
		Stage.Report = (where, e) => GD.PrintErr($"[P5Joker] {where}: {e}");
		Stage.NowMs = () => (ulong)(_clock * 1000.0);
		HitStop.RealSeconds = () => _frames / 30.0;
		string sfxLog = ProjectSettings.GlobalizePath("res://") + $"out/{scenario}/sfx.txt";
		System.IO.File.WriteAllText(sfxLog, "");
		Sfx.Recorder = (name, vol) => System.IO.File.AppendAllText(sfxLog, $"{_frames / 30.0:0.000} {name} {vol:0.00}\n");                 // 顿帧按录像帧数计时（不受时间流速影响）
		GD.Seed(52029);
		Stage.Shake = s => { _shake = s; _shakeLeft = 0.25f; _shakeDir = null; };
		BodyAura.CloneBody = parent =>
		{
			string rig = ProjectSettings.GlobalizePath("res://") + "../../../art/p5/rig/joker";
			SpinePlayer c = SpinePlayer.Load(rig + ".spine-json", rig + ".atlas", rig + ".png");
			parent.AddChild(c);
			c.PlayFrom(_joker!.Current, _joker.Time);
			c.Queue("mask_loop", true);
			return (c, _joker.GlobalTransform);
		};
		Stage.ShakeDir = (s, deg) => { _shake = s; _shakeLeft = 0.25f; _shakeDir = Vector2.FromAngle(Mathf.DegToRad(deg)); };

		_world = new Node2D();
		AddChild(_world);
		BuildBackdrop();
		_back = Layer();
		BuildActors();
		_front = Layer();
		Stage.Front = _front;
		Stage.Back = _back;

		if (!Scenarios.TryGetValue(scenario, out var run))
		{
			GD.PrintErr($"[P5Joker] unknown scenario {scenario}: {string.Join(", ", Scenarios.Keys)}");
			return;
		}
		run(this);
	}

	private readonly List<Action> _every = new();

	/// <summary>每帧都调用（弹药 UI 这类需要逐帧推进的）。</summary>
	public void Every(Action action) => _every.Add(action);

	private Vector2? _shakeDir;
	private long _frames;

	public override void _Process(double delta)
	{
		_clock += delta;
		_frames++;
		foreach (Action a in _every)
		{
			Stage.Safe("every", a);
		}
		if (_shakeLeft > 0f)
		{
			_shakeLeft -= (float)delta;
			float a = _shake * 4f * Mathf.Max(_shakeLeft / 0.25f, 0f);
			_world.Position = _shakeDir is Vector2 d
				? d * a * 2.2f * Mathf.Cos(_shakeLeft * 60f) + new Vector2((float)GD.RandRange(-a, a), (float)GD.RandRange(-a, a)) * 0.3f
				: new Vector2((float)GD.RandRange(-a, a), (float)GD.RandRange(-a, a));
		}
		else
		{
			_world.Position = Vector2.Zero;
		}
	}

	public void At(double seconds, Action action) =>
		GetTree().CreateTimer(seconds).Timeout += () => Stage.Safe("scenario", action);

	private Control Layer()
	{
		Control c = new() { Size = new Vector2(1920f, 1080f), MouseFilter = Control.MouseFilterEnum.Ignore };
		_world.AddChild(c);
		return c;
	}

	/// <summary>暗色的室内背景 + 地面，颜色接近杀戮尖塔 2 第一幕的战斗场景，只为看清特效和人的关系。</summary>
	private void BuildBackdrop()
	{
		// 原版战斗背景（overgrowth，research/sts2_preview_scene）：有就用真的，没有退回纯色
		const string bg = "res://scenes/backgrounds/overgrowth/overgrowth_background.tscn";
		if (ResourceLoader.Exists(bg) && ResourceLoader.Load<PackedScene>(bg)?.Instantiate() is Control real)
		{
			_world.AddChild(real);
			real.Position = new Vector2(960f, 540f);                // 各层以原点为中心（2764×1296），游戏里背景居中
			// 游戏里由 NCombatBackground 脚本给每层挑一个变体填进去；预览里固定用 a（前景层会挡住人物，不放）
			for (int i = 0; i <= 4; i++)
			{
				string layer = $"res://scenes/backgrounds/overgrowth/layers/overgrowth_bg_0{i}_a.tscn";
				if (real.GetNodeOrNull<Control>($"Layer_0{i}") is Control holder && ResourceLoader.Exists(layer))
				{
					holder.AddChild(ResourceLoader.Load<PackedScene>(layer).Instantiate());
				}
			}
			return;
		}
		Polygon2D sky = new()
		{
			Polygon = new[] { new Vector2(0f, 0f), new Vector2(1920f, 0f), new Vector2(1920f, 1080f), new Vector2(0f, 1080f) },
			VertexColors = new[] { new Color("2a3340"), new Color("2a3340"), new Color("15191f"), new Color("15191f") },
		};
		_world.AddChild(sky);
		Polygon2D floor = new()
		{
			Polygon = new[] { new Vector2(0f, 820f), new Vector2(1920f, 820f), new Vector2(1920f, 1080f), new Vector2(0f, 1080f) },
			VertexColors = new[] { new Color("3a3528"), new Color("3a3528"), new Color("1d1a14"), new Color("1d1a14") },
		};
		_world.AddChild(floor);
	}

	/// <summary>Joker 的副本（爆发的剪影分身）：放在本体后面、同一个动作同一时刻。</summary>
	private void Clone(Action<Node2D> use)
	{
		string rig = RigPath();
		SpinePlayer c = SpinePlayer.Load(rig + ".spine-json", rig + ".atlas", rig + ".png");
		c.Position = JokerFeet;
		c.Scale = new Vector2(0.29f, -0.29f);
		_world.AddChild(c);
		_world.MoveChild(c, _joker!.GetIndex());
		c.PlayFrom(_joker.Current, _joker.Time);
		use(c);
	}

	private void BuildActors()
	{
		string rig = RigPath();
		_joker = SpinePlayer.Load(rig + ".spine-json", rig + ".atlas", rig + ".png");
		_joker.Position = JokerFeet;
		_joker.Scale = new Vector2(0.29f, -0.29f);
		_world.AddChild(_joker);
		Enemy1(Enemy, 1f);
		Enemy1(Enemy2, 0.75f);
		Enemy1(Enemy3, 0.8f);
	}

	private static string RigPath()
	{
		string rig = ProjectSettings.GlobalizePath("res://../../../art/p5/rig/joker");
		return FileAccess.FileExists(rig + ".png") ? rig
			: ProjectSettings.GlobalizePath("res://../../../p5mod/pck_stage/animations/characters/silent/silent");
	}

	/// <summary>敌人剪影：一个深紫色的大块头（大小接近第一幕的普通怪），中心就是 VfxSpawnPosition。</summary>
	// 原版怪物代替紫色圆团：(json 名, 缩放)；脚底放在地面，命中点（VfxSpawnPosition）还是 center
	private static readonly Dictionary<Vector2, (string Name, float Scale)> Monsters = new()
	{
		[Enemy] = ("chomper", 0.55f), [Enemy2] = ("inklet", 0.62f), [Enemy3] = ("chomper", 0.48f),
	};

	private void Enemy1(Vector2 center, float k)
	{
		string monDir = ProjectSettings.GlobalizePath("res://.mon");
		string src = ProjectSettings.GlobalizePath("res://") + "../../../research/sts2_preview_scene/recovered/animations/monsters";
		if (Monsters.TryGetValue(center, out var mon) && FileAccess.FileExists($"{monDir}/{mon.Name}.json"))
		{
			SpinePlayer m = SpinePlayer.Load($"{monDir}/{mon.Name}.json", $"{src}/{mon.Name}/{mon.Name}.atlas", $"{src}/{mon.Name}/{mon.Name}.png");
			m.Position = new Vector2(center.X, 830f);
			m.Scale = new Vector2(-mon.Scale, -mon.Scale);           // 朝左（面向 Joker），Spine 的 y 朝上
			_world.AddChild(m);
			m.Play("idle_loop", true);
			_enemies[center] = m;
			return;
		}
		Node2D n = new() { Position = center };
		_world.AddChild(n);
		_enemies[center] = n;
		var pts = new Vector2[28];
		for (int i = 0; i < pts.Length; i++)
		{
			float a = Mathf.Tau * i / pts.Length;
			pts[i] = new Vector2(Mathf.Cos(a) * 135f, Mathf.Sin(a) * 170f + (Mathf.Sin(a) > 0f ? 40f : 0f)) * k;
		}
		n.AddChild(new Polygon2D { Polygon = pts, Color = new Color("4b2f5c") });
		n.AddChild(new Polygon2D { Polygon = Fx.Circle(16f * k, 16), Color = new Color("f2d14a"), Position = new Vector2(-45f, -60f) * k });
		n.AddChild(new Polygon2D { Polygon = Fx.Circle(16f * k, 16), Color = new Color("f2d14a"), Position = new Vector2(20f, -66f) * k });
	}
}
