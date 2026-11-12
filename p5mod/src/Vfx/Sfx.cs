using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 音效（2026-11-01）：P5R 原声，tools/p5/stage_p5.py 把 research/p5r/audio 里挑出来的 .ogg 原样放进 pck 的 res://p5joker/sfx/，
/// 这里读原始字节解码（不走导入系统），同 P3 mod 的 mod/src/Audio/Sfx.cs。只用 Godot，预览工具也能编译。
/// </summary>
public static class Sfx
{
	/// <summary>
	/// 原名 → pck 里的文件名（不带 .ogg）。2026-11-01 音效分层（用户：任何行为尽量加音效，但要有逻辑、有层次）：
	/// 每个动作分 预备 / 冲击 / 余韵 三层，冲击层最响，预备和余韵压低；同一时刻最多两三个声音。
	/// SYSTEM.ACB 的编号和原始流名见 research/p5r/audio/sfx/catalog.csv。
	/// </summary>
	public const string
		OneMore = "battle_onemore",     // 弱点 / 1 MORE 眼神特写的「WEAK」音（SOUND_J SYSTEM.ACB #12）
		CutIn = "cutin",                // 切入（#178）
		EyeCutIn = "battle_cutin",      // 眼神特写出现（用户 2026-11-03 指定；P5R SYSTEM.AWB 第 18 个流，research/p5r/audio/eye_cutin）
		PersonaFlame = "persona_flame", // Persona 蓝焰（#44）
		Curse = "skill_ankoku",         // 暗黑系技能音（SKILLSE/FLOATING/F_ANKOKU，咒怨候选）
		Glass = "glass_burst",          // 玻璃碎裂（P3R ShuffleTime_ArcanaBurst，碎法移植自结城理 mod）
		Select = "cutin_select",        // 拿起牌（#179，很轻的切入提示）
		Cursor = "cursor",              // 选中敌人（#177）
		GunDraw = "battle_gun_draw",    // 掏枪（#7）
		Swish = "swish_02",             // 挥刀风声（#145）
		SwishHeavy = "swish_05",        // 重挥（#151）
		SwishLow = "swish_04",          // 低沉的挥动（#149，缩回 / 转杖）
		Shatter = "shatter_big",        // 眼神特写碎开（用户 2026-11-03：原来的 ArcanaBurst 起音 42ms、不明显、没有一下碎掉的震撼）：
		                                //  P3R 剧情玻璃爆裂（起音 2ms）+ 镜子碎片哗啦（Mirror_Break_01）+ 低频，合成 art/sfx_p5/shatter_big.ogg
		EvokerShatter = "p3_evoker_shatter", // 眼神特写碎开（用户 2026-11-03：「直接用 P3R 之前那个」= P3 mod 召唤器那一枪：闷枪声 + 心灵碎裂玻璃 + 涌起的风 + 垫音；
		                                     //  原来是代码实时合成的，tools 里离线渲成 art/sfx_p5/p3_evoker_shatter.ogg）
		CurseBlast = "curse_blast",     // 毒爆引爆（用户：原来的暴击音太吵太刺耳——damage_critical 2.1s、能量中心 5kHz）：
		                                //  暗黑技能音 + 重拳都滤掉高频 + 低频，art/sfx_p5/curse_blast.ogg
		CurseSoft = "curse_soft",       // 柔和的咒怨闷响（暗黑系技能音滤掉 1.6kHz 以上 + 低频，art/sfx_p5/curse_soft.ogg）：
		                                //  腐蚀波 / 每回合毒伤用（用户：腐蚀波刺耳——continuas_damage 能量中心 7kHz，连抽时一堆叠在一起）
		KnifeRing = "sword_sheath",     // 收刀入鞘的金属「锵」（#48）：转刀落回手里握住的那一下（用户 2026-11-02：转刀声像空转、没分量）
		Hit = "damage_enemy",           // 打中敌人（#22）
		AllOut = "battle_all_out_atk",  // 总攻击斩击（#24，第一刀叠上去）
		Gun = "gun_10",                 // 枪声（用户 2026-11-03：gun_07 像激光枪 → 试听选 D = gun_10，#38）。原来的说明：（#37：起音 0.2ms、带 0.5s 回响的干脆手枪声。原来的 #32 gun_02 起音慢、尾音短，
		                                //  被命中声一盖听起来像打击声——用户 2026-11-02 指出；#33 是 10 连发的机关枪）
		Ricochet = "gun_00",            // 跳弹（#30 gun_00 / talk_ricochet）
		Parry = "encount_parry",        // 格挡（#132）
		Critical = "damage_critical",   // 暴击（#21：召唤高潮那一拍）
		Wing = "wing_large",            // 大翅膀扇动（#64：亚森）
		Slam = "punch_taiman",          // 重拳砸中（#46：咒怨之手砸下去）
		Crack = "crash_object",         // 东西碎开（#176：手从地里钻出来）
		PoisonTick = "continuas_damage",// 持续伤害（#20：每轮毒伤）
		SubBoom = "sub_boom",           // 合成的低频重击（art/sfx_p5/sub_boom.ogg：P5 原声缺低频，垫在冲击层底下）
		OutbreakBurst = "outbreak_burst", // 毒性爆发毒伤结算（用户 2026-11-08：原版受击声太平淡）：P5R SKILLSE 0406（咒怨系候选，起音 0.01s、偏暗），
		                                //  滤掉 6kHz 以上、截 1.5s 淡出、响度归一（art/sfx_p5/outbreak_burst.ogg）
		SinfulThunder = "sinful_thunder", // 大罪穿甲弹：撒旦耶尔登场 / 退场的闪雷（第二版，用户 2026-11-09：旧的太炸裂——现在是干脆的一声脆响 + 低沉滚动的轰鸣，tools/p5/sfx/synth_sinful_thunder.py）
		SinfulCharge = "sinful_charge",   // 蓄力（用户 2026-11-10）：0.95s 越来越密、越来越尖的电流「滋——」，到时停那一刻戛然而止，到时停那一刻被拖慢掉下去（像磁带停转），再接一小段往上提的气声，开枪前 20ms 收住（时停 0.3s 在同一条文件里）；tools/p5/sfx/synth_sinful_blast.py
		SinfulCatch = "sinful_catch",     // 转枪接住：沉重的金属咔嗒
		SinfulShot = "sinful_shot",       // 开枪（第三版，用户 2026-11-10：要特别响亮、特别炸裂）：从静默里直接炸开——三连的爆裂脆响 + 饱和的宽频冲击 + 低频下坠 + 撕开空气的滚动轰鸣 → 远处云层滚回三声 → 耳鸣；冲击在 0.02s
		SinfulFlyby = "sinful_flyby",     // 子弹从远处朝镜头飞过来的多普勒呼啸，最响在 0.35s
		SinfulPierce = "sinful_pierce",   // 贯穿一个敌人（用户 2026-11-09：普通受击声太平淡，配不上这一枪）：脆裂 + 撕开装甲 + 饱和闷击 + 超低频 + 穿出去的撕裂，tools/p5/sfx/synth_sinful_pierce.py
		SinfulPierceLast = "sinful_pierce_last", // 贯穿最后一个：更重、超低频更长、带碎片和滚动的低频尾
		SinfulTwirl = "sinful_twirl",     // 撒旦耶尔转枪（用户 2026-11-09 第三版：干脆、简单、轻——每转半圈一声短促的「咻」，越转越慢越轻，接住时一声很小的「嗒」），tools/p5/sfx/synth_sinful_twirl.py
		SinfulSweepOpen = "sinful_sweep_open",   // 场景圈展开（交接待办 B：轻的「呼——」蓝焰卷过，先响后弱）
		SinfulSweepClose = "sinful_sweep_close", // 场景圈收回：由弱到响收进脚下，最后轻轻一顿（文件末尾 0.25s 是那一顿）
		SinfulFire = "sinful_fire",      // 开枪（用户：不要普通手枪声）：倒吸 → 爆裂 → 超低频重击 → 失真冲击 → 金属余振 → 长尾和回声
		GunHit = "gun_hit",             // 枪命中（演示混音：damage_enemy 升调 1.2、压低、偏右）
		GunHitFinal = "gun_hit_final",  // 收尾那一发的命中：更响的命中 + sub_boom + 47Hz 低频
		Crush = "crush",
		// 技能 / 能力牌特效（Vfx/SkillFx.cs，用户 2026-11-10；全部合成：tools/p5/sfx/synth_skill_fx.py）
		ExposeBreak = "expose_break",   // 暴露：护甲被劈开（两声干裂 + 闷的碎裂 + 碎屑），tools/p5/sfx/synth_skill_fx2.py（card_flick / bullet_load / wail 的第二版也在那里）
		CardDraw = "card_draw",         // 抽牌（用户 2026-11-10：改用 P3 mod 的抽牌音，克制、能听出来就行）
		CardFlick = "card_flick",       // 光牌弹出：干脆的纸牌一弹
		CardBurn = "card_burn",         // 弃牌：一弹 + 烧掉的「嘶」和细碎噼啪
		BulletLoad = "bullet_load",     // 装弹：一发子弹落回手里的金属轻响
		BuffRise = "buff_rise",         // 增益第二版（用户 2026-11-10：克制）：一口往上掠的气 + 很轻的闷底，tools/p5/sfx/synth_skill_fx3.py
		BuffRiseDark = "buff_rise_dark", // 咒怨 / 暗影增益：更暗、更低的气 + 低沉的底
		DebuffPress = "debuff_press",   // 减益：一记往下压的闷响
		// 每张牌的特效（Vfx/CardSkillFx.cs，第一批 2026-11-10；tools/p5/sfx/synth_card_fx1.py，全部噪声合成、克制）
		FootworkSteps = "footwork_steps", // 灵动步法：三下轻快的踏步 + 一口往上的气
		ReticleLock = "reticle_lock",     // 精准 / 跟踪：准星扣上的两下轻的机械咔嗒
		TrackScan = "track_scan",         // 跟踪：很轻的一声细「嗖」
		VenomCoat = "venom_coat",         // 涂毒：黏稠的液体翻涌 + 轻的「滋」
		FumesRoll = "fumes_roll",         // 毒雾：低沉的雾涌过去的气声
		BurstEcho = "burst_echo",         // 爆发：一记闷响紧跟一记回声
		SerpentRise = "serpent_rise",
		SerpentMaw = "serpent_maw",       // 群蛇形态：一条蛇散成烟 → 敌人身上大嘴从画面里冲出来 → 0.46s 咬合（咔 + 闷响），tools/p5/sfx/synth_serpent.py     // 群蛇形态：低沉涌起 + 蛇的嘶声 + 扑出去的一下
		WraithForm = "wraith_form",       // 幽魂形态：往里吸的阴风 + 合上时的闷响
		// 第二批（Vfx/CardSkillFx2.cs；tools/p5/sfx/synth_card_fx2.py）
		ShadowmeldSink = "shadowmeld_sink", // 融入暗影：往下沉的低气 + 两下轻的闷响
		BlurDash = "blur_dash",             // 残影：一闪的短风声
		AfterimageOrbit = "afterimage_orbit", // 余像：绕身的轻风 + 收进来的一下
		ShadowStepCut = "shadow_step_cut",  // 暗影步：一刀切开的「唰」+ 沉下去的气 + 回来时的闷响
		BulletTimeStop = "bullet_time_stop", // 子弹时间：慢下来的风压 + 停住的一下 + 很轻的余响
		FlankCross = "flank_cross",         // 夹击：两刃交叉的双「唰」+ 一声干脆的合
		MalaisePress = "malaise_press",     // 萎靡：低沉的压下来 + 闷响 + 被抽走的气
		SweepLow = "sweep_low",             // 扫腿：贴地的一扫 + 尘土
		// 第三批（Vfx/CardSkillFx3.cs；tools/p5/sfx/synth_card_fx3.py）
		RetainLock = "retain_lock",   // 独门技术：保留扣上的一下很轻的「嗒」
		SmokePuff = "smoke_puff",     // 逃脱计划：烟弹「噗」
		CardCut = "card_cut",         // 生存者：一刀斩开牌的「唰」
		GambleSpin = "gamble_spin",   // 计算下注：轮盘越转越慢的哒哒声 + 停住
		Heartbeat = "heartbeat",      // 肾上腺素：「咚、咚」两下闷的心跳
		EnergyPull = "energy_pull",     // 能量第二版：0.28s 吸气 + 一声软的闷响（没有叮）
		BuffUp = "buff_up",             // 增益：往上盘的气音 + 到顶一声清亮的「叮」
		BuffDark = "buff_dark",         // 咒怨 / 暗影类增益：低沉的上行 + 闷响
		DebuffDown = "debuff_down",     // 减益：往下压的双音 + 落地闷响
		EnergySurge = "energy_surge",   // 能量：吸气（0.28s）+ 清亮的一击
		Wail = "wail",                  // 尖啸：带颤音的下滑嘶喊（压在 5kHz 以下，不刺耳）
		StealthVeil = "stealth_veil",   // 潜行：往下收的风声 + 低气
		TimeSlow = "time_slow",         // 子弹时间：越来越慢、越来越低的三下钟摆 + 像磁带停转的下滑音
		HexGuard = "hex_guard",         // 蜂巢护盾：一串往上爬的玻璃轻响 + 低沉的「嗡」
		ShadowMark = "shadow_mark";     // 夹击标记：两下合拢的轻响 + 锁定

	/// <summary>
	/// Joker 的枪声（用户 2026-11-09：要 Five Cards 演示开头连射里的最终听感）：tools/p5/sfx/five_cards_shots.py 按演示的 mix_audio.py
	/// 原样复刻（同样的层、音量、音高、声像、淡入淡出和软削波），拆成单发：枪声 + 弹壳轻响。连射轮流用演示前四发的四个变体。
	/// 文件已经按演示的音量混好，播放音量用 1.33（抵消 Play 里的 ×0.75）。
	/// </summary>
	public static readonly string[] GunShots = { "gun_shot_1", "gun_shot_2", "gun_shot_3", "gun_shot_4" };
	private static int _gunShot;
	public static string NextGunShot() => GunShots[_gunShot++ % GunShots.Length];
	public const float DemoMixVolume = 1.33f;                // 合成的「捏碎」：几下密集的碎裂咔嚓 + 一记闷响（art/sfx_p5/crush.ogg，手捏爆用）           // 合成的低频重击（art/sfx_p5/sub_boom.ogg：P5 原声缺低频，垫在冲击层底下）

	/// <summary>
	/// 起音提前量（秒）：有的音效开头有一小段空白 / 渐起，主冲击在文件里第几秒（ffmpeg 量的首个过 30% 峰值的点）。
	/// PlayHit 会提前这么多开始播，让主冲击正好落在画面那一帧。
	/// </summary>
	private static readonly Dictionary<string, float> Lead = new()
	{
		[Parry] = 0.068f, [Slam] = 0.1f, [PoisonTick] = 0.078f, [CutIn] = 0.083f, [PersonaFlame] = 0.13f, [Wing] = 0.023f,
		[Crack] = 0.019f, [SwishHeavy] = 0.005f, [KnifeRing] = 0.048f, [SinfulFire] = 0.12f, [SinfulShot] = 0.02f,
		[CardCut] = 0.29f, [CardDraw] = 0.046f, [SmokePuff] = 0.012f, [RetainLock] = 0.013f,   // 游戏音效样本里主冲击的位置（2026-11-12 量的）
	};

	/// <summary>在 delay 秒后的那一帧「打中」：按起音提前量提前开始播。</summary>
	public static void PlayHit(string name, float volume, float delay) =>
		Play(name, volume, Mathf.Max(0f, delay - (Lead.TryGetValue(name, out float l) ? l : 0f)));

	/// <summary>预览工具用：每次真正播放时记下（名字, 音量），渲染后混成带声音的视频（tools/p5/fxpreview/mix.py）。</summary>
	public static Action<string, float>? Recorder;

	private const string Dir = "res://p5joker/sfx/";
	private static readonly Dictionary<string, AudioStreamOggVorbis?> Cache = new();
	private static readonly Dictionary<string, ulong> Last = new();

	private static AudioStreamOggVorbis? Load(string name)
	{
		if (Cache.TryGetValue(name, out var s))
		{
			return s;
		}
		try
		{
			string path = Dir + name + ".ogg";
			byte[] bytes = FileAccess.FileExists(path) ? FileAccess.GetFileAsBytes(path) : Array.Empty<byte>();
			s = bytes.Length > 0 ? AudioStreamOggVorbis.LoadFromBuffer(bytes) : null;
		}
		catch (Exception e)
		{
			Stage.Report("sfx " + name, e);
			s = null;
		}
		Cache[name] = s;
		return s;
	}

	/// <summary>
	/// 预热：把 res://p5joker/sfx/ 里所有音效都读进来解码好（用户 2026-11-03：第一次打出打击都会卡一下——
	/// 预热时音效被静音、一次都没加载过，第一次播放才现读现解码）。每帧解几个，不在一帧里全做完。
	/// </summary>
	public static void PreloadAll(Node anchor)
	{
		var names = new System.Collections.Generic.List<string>();
		foreach (string f in DirAccess.GetFilesAt(Dir))
		{
			string n = f.Replace(".import", "").Replace(".remap", "");
			if (n.EndsWith(".ogg") && !names.Contains(n[..^4]))
			{
				names.Add(n[..^4]);
			}
		}
		for (int i = 0; i < names.Count; i++)
		{
			string n = names[i];
			Fx.After(anchor, 0.05 + 0.03 * (i / 3), () => Load(n));
		}
	}

	/// <summary>预热（Vfx/Warmup.cs）期间静音：特效照常跑一遍，但不出声。只拦当场播放的（预热不排延迟的声音）。</summary>
	/// <summary>
	/// 压低其他声音（用户 2026-11-09：这一枪的震撼感一半要靠声音——开枪前一瞬间周围安静下来）：
	/// 除 Master 和本 mod 用的那条总线以外，所有音频总线降 db，hold 秒后在 release 秒里恢复。游戏走别的音频系统时这里不起作用，也无害。
	/// </summary>
	public static void Duck(float db, float hold, float release) => Stage.Safe("sfx duck", () =>
	{
		if (Engine.GetMainLoop() is not SceneTree tree) return;
		string mine = AudioServer.GetBusIndex("SFX") >= 0 ? "SFX" : "Master";
		for (int i = 0; i < AudioServer.BusCount; i++)
		{
			string name = AudioServer.GetBusName(i);
			if (name == "Master" || name == mine) continue;
			int bus = i;
			float rest = AudioServer.GetBusVolumeDb(bus);
			Tween tw = tree.CreateTween().SetIgnoreTimeScale(true);
			tw.TweenMethod(Callable.From<float>(v => AudioServer.SetBusVolumeDb(bus, v)), rest, rest + db, Mathf.Min(0.12f, hold));
			tw.TweenInterval(Mathf.Max(0f, hold - 0.12f));
			tw.TweenMethod(Callable.From<float>(v => AudioServer.SetBusVolumeDb(bus, v)), rest + db, rest, release);
		}
	});

	public static bool Muted;

	private const string E = "event:/sfx/";

	/// <summary>
	/// 代码合成的技能牌音效 → 游戏自带的 FMOD 事件（2026-11-11 用户：合成的音效有很重的代码感，跟专业音效差远了，换成游戏自带的；
	/// 不换的：攻击牌的打击和命中、亚森召唤 / 眼神特写 / 卡牌燃烧和抽牌、华丽收场整套）。查到就改播游戏的事件（音量 × Gain），
	/// Event 为 null = 不出声（同一个动作里已经有别的声音）。预览工具里没有 FMOD，这些都不响，要进游戏听。
	/// 都挑预览里也能听到的（tools/p5/fxpreview/game_sfx.py 找得到样本的）；能量那组 gain_energy 例外（样本不在 sfx.bank 里，预览里不响）。
	/// 用户在预览 / 游戏里听了哪个不合适就在这里换。
	/// </summary>
	/// <summary>技能牌音效整体再压一档（用户 2026-11-12：音效真的不用太明显，现在太大声）。</summary>
	public const float SkillQuiet = 0.5f;

	private static readonly Dictionary<string, (string? Event, float Gain)> GameSfx = new()
	{
		// 增益 / 减益通用积木
		[BuffRise] = (E + "buff", 0.8f), [BuffUp] = (E + "buff", 0.8f),
		[BuffRiseDark] = (E + "characters/defect/defect_dark_channel", 0.55f), [BuffDark] = (E + "characters/defect/defect_dark_channel", 0.55f),
		[DebuffPress] = (E + "debuff", 0.8f), [DebuffDown] = (E + "debuff", 0.8f), [ShadowMark] = (E + "debuff", 0.7f),
		[EnergyPull] = (E + "ui/gain_energy", 0.8f), [EnergySurge] = (E + "ui/gain_energy", 0.8f), [Heartbeat] = (E + "ui/gain_energy", 0.8f),
		[CardFlick] = (E + "ui/relic_activate_draw", 0.7f),
		[BulletLoad] = (E + "enemy/enemy_attacks/crossbow_ruby_raider/crossbow_ruby_raider_reload", 0.5f),
		[ExposeBreak] = (E + "block_break", 0.7f),
		[HexGuard] = (E + "block_gain", 0.8f),
		// 每张牌的
		[FootworkSteps] = (E + "buff", 0.7f),
		[ReticleLock] = (E + "ui/relic_activate_general", 0.7f), [TrackScan] = (null, 0f),
		[VenomCoat] = (E + "enemy/enemy_attacks/spiny_toad/spiny_toad_lick", 0.5f),
		[FumesRoll] = (E + "enemy/enemy_attacks/living_fog/living_fog_summon", 0.55f),
		[BurstEcho] = (E + "ui/enchant_shimmer", 0.7f),
		[WraithForm] = (E + "enemy/enemy_attacks/spectral_knight/spectral_knight_hex", 0.6f),
		[ShadowmeldSink] = (E + "enemy/enemy_attacks/vantom/vantom_buff", 0.55f), [StealthVeil] = (E + "enemy/enemy_attacks/vantom/vantom_buff", 0.5f),
		[BlurDash] = (E + "block_gain", 0.7f), [AfterimageOrbit] = (E + "buff", 0.7f),
		[ShadowStepCut] = (E + "enemy/enemy_attacks/test_subject/test_subject_slash", 0.45f),
		[BulletTimeStop] = (E + "characters/defect/defect_frost_channel", 0.55f), [TimeSlow] = (E + "characters/defect/defect_frost_channel", 0.55f),
		[FlankCross] = (E + "characters/silent/silent_dagger_spray", 0.45f),
		[MalaisePress] = (E + "debuff", 0.8f), [Wail] = (E + "debuff", 0.8f),
		[SweepLow] = (E + "enemy/enemy_attacks/seapunk/seapunk_kick", 0.5f),
		[RetainLock] = (E + "ui/clicks/ui_click", 0.6f),
		[SmokePuff] = (E + "block_break", 0.5f),   // 起音快（fogmog_summon 前面有 0.73s 空白，和画面对不上）
		[CardCut] = (E + "enemy/enemy_attacks/test_subject/test_subject_slash", 0.45f),
		[GambleSpin] = (E + "ui/gold/gold_2", 0.7f),
		// 群蛇形态（SerpentMantle 已直接播游戏事件，这两个不再被调用；万一调用到也走游戏的）
		[SerpentRise] = (E + "characters/defect/defect_dark_channel", 0.6f),
		[SerpentMaw] = (E + "enemy/enemy_attacks/scroll_of_biting/scroll_of_biting_bite", 0.55f),
	};

	/// <summary>播放；同一音效 45ms 内不重复叠加。volume 0～1.4。</summary>
	public static void Play(string name, float volume = 1f, float delay = 0f) => Stage.Safe("sfx " + name, () =>
	{
		if (Muted || Engine.GetMainLoop() is not SceneTree tree)
		{
			return;
		}
		if (delay > 0f)
		{
			tree.CreateTimer(delay, processAlways: true).Timeout += () => Play(name, volume);
			return;
		}
		ulong now = Time.GetTicksMsec();
		if (Last.TryGetValue(name, out ulong last) && now - last < 45)
		{
			return;
		}
		Last[name] = now;
		if (GameSfx.TryGetValue(name, out var game))
		{
			if (game.Event != null) Stage.PlayEvent?.Invoke(game.Event, Mathf.Clamp(volume, 0f, 1.4f) * game.Gain * SkillQuiet);
			return;
		}
		Recorder?.Invoke(name, volume);
		if (Load(name) is not AudioStreamOggVorbis stream)
		{
			return;
		}
		StringName bus = AudioServer.GetBusIndex("SFX") >= 0 ? new StringName("SFX") : new StringName("Master");
		AudioStreamPlayer p = new() { Stream = stream, Bus = bus, VolumeDb = Mathf.LinearToDb(Mathf.Clamp(volume, 0.0001f, 1.4f) * 0.75f) };
		tree.Root.AddChild(p);
		p.Finished += p.QueueFree;
		p.Play();
		tree.CreateTimer(stream.GetLength() + 2.0, processAlways: true).Timeout += () =>
		{
			if (GodotObject.IsInstanceValid(p))
			{
				p.QueueFree();
			}
		};
	});
}
