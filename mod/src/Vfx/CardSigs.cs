using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace P3Makoto.Vfx;

/// <summary>
/// 每张牌出牌时的专属演出（v0.9 卡牌特效大改）。原则：贴合卡牌名字和效果、贴合 P3R 主题（塔罗 / 影时间 / Shuffle Time /
/// 弱点大字 / 星象），尽量每张不同；要大、要明显、要有确认感。攻击牌每一击的命中特效由 CardStyles 的元素决定，这里只管"出牌那一下"。
/// 塔罗牌编号与名称取自大阿卡纳（P3 的社群 Arcana）。
/// </summary>
public static class CardSigs
{
	public sealed record Ctx(Control Front, Control Back, NCreature Caster, Vector2 Head, Vector2 Center, Vector2 Feet, List<NCreature> Targets)
	{
		public Vector2 Above => Head + new Vector2(-10f, -170f);
		public Vector2[] TargetCenters => Targets.Count > 0
			? Targets.Select(t => t.VfxSpawnPosition).ToArray()
			: new[] { Front.GetViewportRect().Size * new Vector2(0.7f, 0.5f) };
		public Vector2 FirstTarget => TargetCenters[0];
	}

	private static void After(Ctx c, double s, Action a) => Fx.After(c.Front, s, "card sig", a);

	// 大塔罗牌放在人物身后的图层、人物左侧与头同高（不挡人；原来放在头顶上方，会超出屏幕顶边），碎裂的玻璃和闪光仍在前景
	private static void Tarot(Ctx c, string numeral, string name, Color tint, double delay = 0.15) =>
		After(c, delay, () =>
		{
			Vector2 at = c.Head + new Vector2(-230f, 0f);                      // 卡牌中心与头同高
			at.Y = Mathf.Max(at.Y, 225f);                     // 卡高约 270px：中心不高于 225，顶边不压到顶栏
			at.X = Mathf.Max(at.X, 110f);
			Motifs.BigTarot(c.Back, at, numeral, name, tint, s: 0.85f, fxLayer: c.Front);
		});

	// 规则：以结城理自己为中心、会大面积盖住他的实心光效（爆发、光柱、汇聚、闪光）一律放在人物身后（Back）；
	// 文字、火花、蝴蝶、星座连线这类轻量元素留在前面（Front）。
	private static readonly Dictionary<string, Action<Ctx>> Sigs = new()
	{
		// ═════ 攻击：出牌瞬间的"起手" ═════
		["WroughtInWar"] = c => Motifs.ForgeSpark(c.Front, c.Center + new Vector2(90f, -20f), 0.8f),
		["BeatIntoShape"] = c => After(c, 0.18, () => Motifs.ForgeSpark(c.Front, c.FirstTarget, 1.2f)),
		["HeirloomHammer"] = c => After(c, 0.16, () => Motifs.CutIn(c.Front, c.FirstTarget, "CRITICAL", Pal.Blood)),
		["KnockoutBlow"] = c => After(c, 0.16, () => Motifs.CutIn(c.Front, c.FirstTarget, "ONE MORE!", Pal.Royal)),
		["KinglyKick"] = c => After(c, 0.16, () => Motifs.CutIn(c.Front, c.FirstTarget, "GOD'S HAND", Pal.Royal)),
		["KinglyPunch"] = c => After(c, 0.14, () => Motifs.Smash(c.Front, c.FirstTarget + new Vector2(-40f, 0f), c.Feet, Pal.Cyan, 0.5f, crack: false)),
		["CrushUnder"] = c => After(c, 0.14, () =>
		{
			foreach (NCreature t in c.Targets)
			{
				Motifs.GroundCrack(c.Front, t.GetBottomOfHitbox(), 1.4f);
				Motifs.Seal(c.Front, t.VfxSpawnPosition, t.GetBottomOfHitbox(), Pal.Blood, up: false, 0.8f);
			}
		}),
		["CrescentSpear"] = c => After(c, 0.1, () => Motifs.FullMoon(c.Back, c.FirstTarget, 0.6f)),
		["PhotonCut"] = c => After(c, 0.1, () => P3Fx.SpeedLines(c.Front, c.FirstTarget, Pal.Ice, 26, 60f, 700f, 0.3f)),
		["HeavenlyDrill"] = c => After(c, 0.12, () => Motifs.Drill(c.Front, c.FirstTarget, 1.2f, 0.7f)),
		["SolarStrike"] = c => After(c, 0.12, () => Motifs.SolarFlare(c.Front, c.FirstTarget + new Vector2(0f, -120f), 0.9f)),
		["Hegemony"] = c => After(c, 0.14, () => Motifs.CutIn(c.Front, c.FirstTarget, "HAMAON", ElementPal.HolyGold)),
		["Radiate"] = c => P3Fx.LightPillar(c.Back, c.Feet, ElementPal.HolyGold, 220f, 900f, 0.8f),
		["LunarBlast"] = c => { foreach (NCreature t in c.Targets) { Lunar.Rise(t); } },
		["GammaBlast"] = c => After(c, 0.1, () => Motifs.CutIn(c.Front, c.FirstTarget, "ZIODYNE", ElementPal.Volt)),
		["Devastate"] = c => After(c, 0.1, () => Motifs.CutIn(c.Front, c.FirstTarget, "MEGIDO", ElementPal.Almighty)),
		["MakeItSo"] = c => After(c, 0.1, () => Motifs.CutIn(c.Front, c.FirstTarget, "MAKE IT SO", Pal.Cyan)),
		["Bombardment"] = c => Motifs.MeteorRain(c.Front, c.TargetCenters, 6, Pal.Cyan, 0.9f),
		["Comet"] = c => Motifs.Comet(c.Front, c.FirstTarget, Pal.Cyan, 1.2f),
		["CrashLanding"] = c => After(c, 0.05, () => Motifs.Comet(c.Front, c.FirstTarget, ElementPal.FireMid, 1.4f)),
		["MeteorShower"] = c => Motifs.MeteorRain(c.Front, c.TargetCenters, 14, Pal.Cyan, 1.0f),
		["SevenStars"] = c => Motifs.Constellation(c.Front, c.Above + new Vector2(260f, -20f), Motifs.BigDipper, Pal.Cyan, 1.1f),
		["GuidingStar"] = c => Motifs.Constellation(c.Front, c.Above + new Vector2(120f, 0f),
			new[] { new Vector2(-120f, 30f), new Vector2(0f, -40f), new Vector2(120f, 20f) }, ElementPal.HolyGold),
		["AstralPulse"] = c => { P3Fx.Shockwave(c.Front, c.Center, Pal.Cyan, 1400f, 0.7f); P3Fx.Ripple(c.Front, c.Center, 1400f, 0.9f, 0.04f); },
				["CelestialMight"] = c => After(c, 0.1, () => Motifs.MultiCut(c.Front, c.FirstTarget, 5, Pal.Cyan, 1.0f)),
		["DyingStar"] = c => After(c, 0.3, () => { foreach (Vector2 t in c.TargetCenters) { Bold.Mudoon(c.Front, t, 1.2f); } }),
		["Supermassive"] = c => After(c, 0.1, () => Motifs.CutIn(c.Front, c.FirstTarget, "SUPERMASSIVE", ElementPal.Curse)),
		["CollisionCourse"] = c => After(c, 0.12, () => Motifs.Meteor(c.Front, c.FirstTarget, ElementPal.FireMid, 0.7f)),

		// ═════ 技能：防御 ═════
		["CloakOfStars"] = c => Motifs.Constellation(c.Front, c.Center,
			new[] { new Vector2(-150f, 60f), new Vector2(-120f, -90f), new Vector2(0f, -170f), new Vector2(120f, -90f), new Vector2(150f, 60f) }, Pal.Cyan, 0.9f),
		["Glitterstream"] = c => { for (int i = 0; i < 6; i++) { P3Fx.Butterfly(c.Front, c.Feet + new Vector2(-200f + 70f * i, 0f), c.Head + new Vector2(-120f + 50f * i, -100f), 1.2f, 0.8f); } },
		["GatherLight"] = c => { P3Fx.Gather(c.Back, c.Center, Pal.Ice, 520f, 0.4f); After(c, 0.35, () => P3Fx.Flash(c.Back, c.Center, Pal.Ice, 420f, 0.3f)); },
		["Bulwark"] = c => After(c, 0.1, () => Motifs.ForgeSpark(c.Front, c.Center + new Vector2(110f, 0f), 1.1f)),
		["ParticleWall"] = c => { Node2D p = Fx.Root(c.Front, c.Center + new Vector2(120f, 0f), "P3ParticleWall"); Fx.Sparks(p, Pal.Cyan, 90, 60f, 260f, 0.8f, scaleMin: 0.1f, scaleMax: 0.3f); Fx.FreeAfter(p, 1.0); },
		["IAmInvincible"] = c => Motifs.CutIn(c.Front, c.Center + new Vector2(200f, 0f), "TETRAKARN", Pal.Royal),
		["Reflect"] = c => Motifs.CutIn(c.Front, c.Center + new Vector2(200f, 0f), "MAKARAKARN", ElementPal.Almighty),
		["CosmicIndifference"] = c => Motifs.DarkClock(c.Front, c.Above, 0.7f),
		["ManifestAuthority"] = c => { Motifs.ShuffleTime(c.Front, c.Center, 3, ElementPal.HolyGold); Tarot(c, "IV", "EMPEROR", ElementPal.HolyGold, 0.05); },

		// ═════ 技能：增益 / 减益 ═════
		["Patter"] = c => Motifs.Seal(c.Front, c.Center, c.Feet, ElementPal.HolyGold, up: true),
		["Terraforming"] = c => { Motifs.Seal(c.Front, c.Center, c.Feet, ElementPal.HolyGold, up: true, 1.2f); Motifs.GroundCrack(c.Front, c.Feet, 1.2f); },
		["Monologue"] = c => { Motifs.Seal(c.Front, c.Center, c.Feet, Pal.Blood, up: true); Tarot(c, "I", "MAGICIAN", Pal.Blood); },
		["KnowThyPlace"] = c => After(c, 0.2, () =>
		{
			foreach (NCreature t in c.Targets)
			{
				Motifs.Seal(c.Front, t.VfxSpawnPosition, t.GetBottomOfHitbox(), ElementPal.Curse, up: false, 1.1f);
				Motifs.CutIn(c.Front, t.VfxSpawnPosition, "DEBILITATE", ElementPal.Curse, 70f);
			}
		}),
		["Resonance"] = c =>
		{
			Motifs.Seal(c.Front, c.Center, c.Feet, Pal.Blood, up: true);
			After(c, 0.25, () => { foreach (NCreature t in c.Targets) { Motifs.Seal(c.Front, t.VfxSpawnPosition, t.GetBottomOfHitbox(), Pal.Royal, up: false, 0.8f); } });
		},
		["Conqueror"] = c => After(c, 0.15, () => { Motifs.CutIn(c.Front, c.FirstTarget, "CONQUEROR", Pal.Royal); Motifs.ForgeSpark(c.Front, c.Center + new Vector2(110f, -20f), 0.8f); }),
		["Charge"] = c => { Motifs.Seal(c.Front, c.Center, c.Feet, ElementPal.HolyGold, up: true, 1.2f); Motifs.CutIn(c.Front, c.Center + new Vector2(220f, 0f), "CHARGE!!", ElementPal.HolyGold); },
		["Begone"] = c => Motifs.CutIn(c.Front, c.Center + new Vector2(220f, 0f), "BEGONE!", Pal.Royal),
		["Guards"] = c => Motifs.CutIn(c.Front, c.Center + new Vector2(220f, 0f), "GUARDS!!!", Pal.Royal),

		// ═════ 技能：辉星 / 能量 / 抽牌（塔罗与星象）═════
		["Venerate"] = c => Tarot(c, "XVII", "THE STAR", Pal.Cyan),
		// 抬手（cast_light 的托掌在 ~0.15s 到位）时从掌心翻出塔罗牌
		["RoyalGamble"] = c => After(c, 0.15, () => Grand.FortuneWheel(c.Back, c.Front, WeaponFx.Bone(c.Caster, "hand") ?? c.Center, c.Head + new Vector2(-235f, -125f),
			c.Center + new Vector2(0f, -20f), ElementPal.HolyGold, 0.75f)),
		["HiddenCache"] = c => Tarot(c, "IX", "HERMIT", Pal.Royal),
		["Glow"] = c => { P3Fx.Flash(c.Back, c.Center, Pal.Ice, 520f, 0.4f); P3Fx.ButterflyBurst(c.Front, c.Head, 7, 340f); },
		["Alignment"] = c => Motifs.Constellation(c.Front, c.Above,
			new[] { new Vector2(-180f, 0f), new Vector2(-60f, 0f), new Vector2(60f, 0f), new Vector2(180f, 0f) }, ElementPal.Volt),
		["Convergence"] = c => { Motifs.DarkClock(c.Front, c.Above, 0.8f); P3Fx.Gather(c.Back, c.Center, Pal.DarkHour, 600f, 0.45f); },
		["Prophesize"] = c => After(c, 0.15, () => ThemeFx.Compendium(c.Caster)),
		["ForegoneConclusion"] = c => After(c, 0.15, () => ThemeFx.Contract(c.Caster)),
		["DecisionsDecisions"] = c => After(c, 0.1, () => ThemeFx.ShuffleTime(c.Caster)),
		["Glimmer"] = c => { P3Fx.Flash(c.Back, c.Center, Pal.Cyan, 380f, 0.25f); Motifs.ShuffleTime(c.Front, c.Center, 2, Pal.Cyan); },
		["Plot"] = c => { Motifs.DarkClock(c.Front, c.Above, 0.6f); Motifs.ShuffleTime(c.Front, c.Center, 3, Pal.DarkHour); },
		["Tutor"] = c => Motifs.ShuffleTime(c.Front, c.Center, 1, Pal.Cyan),
		["BundleOfJoy"] = c => { Motifs.ShuffleTime(c.Front, c.Center, 5, ElementPal.HolyGold); P3Fx.ButterflyBurst(c.Front, c.Head, 6, 320f, ElementPal.HolyGold); },
		["Quasar"] = c => { Bold.Megidolaon(c.Back, c.Above, 0.45f); After(c, 0.3, () => Motifs.ShuffleTime(c.Front, c.Center, 3, ElementPal.Almighty)); },
		["Largesse"] = c => Motifs.ShuffleTime(c.Front, c.Center, 2, ElementPal.HolyGold),
		["Constellation"] = c => Motifs.Constellation(c.Front, c.Above,
			new[] { new Vector2(-140f, 40f), new Vector2(-40f, -60f), new Vector2(60f, -20f), new Vector2(150f, -90f), new Vector2(110f, 50f) }, Pal.Cyan),
		["SpoilsOfBattle"] = c => { Motifs.ForgeSpark(c.Front, c.Center + new Vector2(100f, 0f), 0.8f); Motifs.ShuffleTime(c.Front, c.Center, 2, ElementPal.FireMid); },
		["RefineBlade"] = c => Motifs.ForgeSpark(c.Front, c.Center + new Vector2(110f, -20f), 1.0f),
		["TheSmith"] = c => { for (int i = 0; i < 3; i++) { After(c, 0.18 * i, () => Motifs.ForgeSpark(c.Front, c.Center + new Vector2(110f, -20f), 0.9f + 0.2f)); } },
		["SummonForth"] = c => { Motifs.ForgeSpark(c.Front, c.Center + new Vector2(110f, -20f), 1.1f); Motifs.CutIn(c.Front, c.Center + new Vector2(220f, 0f), "SUMMON FORTH", Pal.Royal); },
		["BigBang"] = c => After(c, 0.3, () => { Bold.Megidolaon(c.Back, c.Above, 1.3f); Motifs.ShuffleTime(c.Front, c.Center, 5, ElementPal.Almighty); }),

		// ═════ 能力 ═════
		["Tyranny"] = c => Tarot(c, "IV", "EMPEROR", Pal.Blood),
		["MonarchsGaze"] = c => { Tarot(c, "XIX", "THE SUN", ElementPal.HolyGold); After(c, 0.3, () => { foreach (NCreature t in c.Targets) { Motifs.Seal(c.Front, t.VfxSpawnPosition, t.GetBottomOfHitbox(), Pal.Royal, up: false, 0.7f); } }); },
		["Parry"] = c => Tarot(c, "VII", "CHARIOT", Pal.Royal),
		["NeutronAegis"] = c => { Extras.HexShield(c.Front, c.Center, 1.4f, false, 0.8f); Tarot(c, "XI", "STRENGTH", Pal.Cyan); },
		["Arsenal"] = c => { for (int i = 0; i < 3; i++) { After(c, 0.12 * i, () => Motifs.ForgeSpark(c.Front, c.Center + new Vector2(80f + 40f * i, -60f + 30f * i), 0.8f)); } },
		["SwordSage"] = c => After(c, 0.2, () => Motifs.ForgeSpark(c.Front, c.Center + new Vector2(110f, -40f), 1.3f)),
		["HammerTime"] = c => { Motifs.ForgeSpark(c.Front, c.Center + new Vector2(110f, -20f), 1.4f); Motifs.CutIn(c.Front, c.Center + new Vector2(220f, 0f), "HAMMER TIME", ElementPal.FireMid); },
		["SeekingEdge"] = c => After(c, 0.2, () => { foreach (Vector2 t in c.TargetCenters) { Motifs.Thrust(c.Front, t, Pal.Royal, 0.7f); } }),
		["Furnace"] = c => Bold.Agidyne(c.Back, c.Center, c.Feet, 0.8f),
		["Orbit"] = c => Tarot(c, "XXI", "THE WORLD", Pal.Cyan),
		["PaleBlueDot"] = c => Motifs.FullMoon(c.Back, c.Above, 0.45f),
		["ChildOfTheStars"] = c => { Tarot(c, "XVII", "THE STAR", Pal.Cyan); Motifs.Constellation(c.Front, c.Center, Motifs.BigDipper, Pal.Ice, 0.7f); },
		["SpectrumShift"] = c => Motifs.ShuffleTime(c.Front, c.Center, 4, ElementPal.Almighty),
		["Royalties"] = c => { Tarot(c, "X", "FORTUNE", ElementPal.HolyGold); After(c, 0.3, () => Grand.CoinRain(c.Front, c.Center, c.Feet.Y, 34)); },
		["PillarOfCreation"] = c => After(c, 0.1, () => ThemeFx.SummonGate(c.Caster)),
		["Genesis"] = c => After(c, 0.35, () => Bold.Hamaon(c.Back, c.Center, c.Feet, 1.5f)),
		["TheSealedThrone"] = c => { Motifs.DarkClock(c.Front, c.Above, 1.0f); After(c, 0.4, () => Bold.Hamaon(c.Back, c.Center, c.Feet, 1.3f)); },
	};

	public static void Play(CardModel card, Ctx c)
	{
		if (Sigs.TryGetValue(card.GetType().Name, out Action<Ctx>? sig))
		{
			ModEntry.Safe("card sig " + card.GetType().Name, () => sig(c));
		}
	}
}
