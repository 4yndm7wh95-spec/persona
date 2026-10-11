"""rig v6 的全部动画：关键姿势 + 缓动（见 build_rig_v6.py 的 Anim / export_anim）。
坐标约定：手腕目标、脚位移都是基准图像素（y 向下）；手腕目标在"胸部装配坐标系"里——身体怎么倾，手相对身体的位置不变。
角度（Spine 度）：正 = 逆时针。躯干 / 头：正 = 后仰，负 = 前倾。

动作逻辑（"不像木偶"的几条规矩）：
- 先动重心：任何大动作都由胯（重心）先起，腰 → 胸 → 肩 → 肘 → 手依次滞后（LAG 表），末端最后到位并带过冲。
- 脚钉在地上（IK），身体下压膝盖自然弯；需要移动的脚单独打"抬起 → 落下"关键帧，抬脚时重心先移到另一只脚。
- 发力有预备（反向小动作）→ 爆发（快）→ 定格（长）→ 收招（慢）。
- 换手（握剑 / 召唤器）发生在手被身体或特效遮住、或运动最快的那一帧。"""
import math

EVENTS = ["death_particles_start", "death_particles_end", "explode_dead", "explode_end",
          "weapon_draw", "weapon_windup", "weapon_slash", "weapon_sheath", "evoker_draw", "evoker_fire", "evoker_sheath"]


def make(data):
    import build_rig_v6 as R
    from build_rig_v6 import Anim, add, arm, body, feet, P

    bones = {b["name"]: b for b in data["bones"]}
    blade_rel = math.degrees(math.atan2(bones["sword_tip"]["y"], bones["sword_tip"]["x"]))     # 剑身相对手骨骼的角度
    barrel_rel = math.degrees(math.atan2(bones["muzzle"]["y"] - bones["grip"]["y"], bones["muzzle"]["x"] - bones["grip"]["x"]))

    def sword(side_wrist, blade_dir, bend=1, frame=None):
        """近侧手握剑：手腕位置 + 剑身方向（Spine 角度）。frame 给出时两者都是世界坐标，否则是胸部坐标系。"""
        return arm("n", side_wrist, hand_dir=blade_dir - blade_rel, bend=bend, frame=frame)

    def lean(hx, hy, spine, chest, head_world, sy=0.0):
        """躯干姿势 + 它的坐标系；头按"世界角度"给（身体怎么倾，眼睛都盯着前方）。"""
        return body(hx=hx, hy=hy, spine=spine, chest=chest, head=head_world - spine - chest, sy=sy), (hx, hy, spine, chest)

    def evoker(wrist, barrel_dir, bend=1, fore_scale=1.0):
        return arm("n", wrist, hand_dir=barrel_dir - barrel_rel, bend=bend, fore_scale=fore_scale)

    mx, my = bones["muzzle"]["x"], bones["muzzle"]["y"]

    def evoker_at(muzzle, barrel_dir, fore_scale=0.6):
        """枪口顶在 muzzle（底图像素），枪管朝 barrel_dir：反推手腕位置。前臂按透视缩短（肘朝镜头抬起）。"""
        hd = math.radians(barrel_dir - barrel_rel)
        ox = mx * math.cos(hd) - my * math.sin(hd)
        oy = mx * math.sin(hd) + my * math.cos(hd)
        return evoker((muzzle[0] - ox, muzzle[1] + oy), barrel_dir, bend=1, fore_scale=fore_scale)

    anims = []

    # ───────────── 站姿 ─────────────
    # 待机：重心略沉、膝盖放松，两臂自然下垂贴近身体（不再是 A 字），肘部微屈
    STANCE = feet(n=(12, 0), f=(-10, 0))
    # 远侧手插兜（v5 起用户喜欢的待机），近侧手自然下垂、肘微屈
    FAR_POCKET = arm("f", (622, 668), bend=1, hand_rel=0)
    IDLE = add(STANCE, body(hx=-4, hy=6, chest=0.5, head=-1), arm("n", (330, 700), bend=-1, hand_rel=6), FAR_POCKET)
    INHALE = add(STANCE, body(hx=-4, hy=3, chest=-0.2, head=-1.8, sy=0.012), arm("n", (327, 697), bend=-1, hand_rel=8),
                 arm("f", (621, 665), bend=1, hand_rel=0))

    def blink(a, t):
        a.att(0, "blink", "blink")
        keys = a.colors.setdefault("blink", [(0.0, 0.0)])
        keys += [(t, 0.0), (t + 0.04, 1.0), (t + 0.1, 1.0), (t + 0.16, 0.0)]

    a = Anim("idle_loop", 3.2, loop=True).k(0, IDLE).k(1.5, INHALE).k(3.2, IDLE)
    blink(a, 1.9)
    anims.append(a)

    # 放松：经典的双手插兜，重心压在后脚，头微低
    POCKET = add(STANCE, body(hx=-8, hy=8, spine=1, chest=1.5, head=-4),
                 arm("n", (446, 668), bend=-1, hand_rel=0), arm("f", (622, 668), bend=1, hand_rel=0))
    POCKET_IN = add(POCKET, body(hy=-3, chest=-0.6, head=-0.8, sy=0.01))
    a = Anim("relaxed_loop", 4.0, loop=True).k(0, POCKET).k(2.0, add(POCKET, POCKET_IN, P(hip={"y": -6}))).k(4.0, POCKET)
    a.hand(0, "n", "pocket").hand(0, "f", "pocket")
    blink(a, 1.1)
    blink(a, 3.3)
    anims.append(a)

    # 低血：不做专属姿势（用户要求），和待机一样
    a = Anim("low_health_loop", 3.2, loop=True).k(0, IDLE).k(1.5, INHALE).k(3.2, IDLE)
    blink(a, 1.9)
    anims.append(a)

    # ───────────── 召唤器 ─────────────
    # 枪套在近侧胯部：伸手 → 握住（换成持枪手）→ 抬到太阳穴：肘抬到肩高、往外张，前臂横着，枪口抵住太阳穴
    TEMPLE = (505, 108)

    def to_temple(wrist):
        return math.degrees(math.atan2(-(TEMPLE[1] - wrist[1]), TEMPLE[0] - wrist[0]))
    FAR_TENSE = arm("f", (624, 664), bend=1, hand_rel=0)                # 召唤器时远侧手仍插兜
    DIP = add(IDLE, body(hy=14, chest=-1, head=-3))
    REACH = add(STANCE, body(hx=-4, hy=12, spine=-1, chest=-3, head=-8), arm("n", (452, 602), bend=-1, hand_rel=-20), FAR_TENSE)
    # 肘抬高往外张、手在头的左上方，枪管斜向下抵住太阳穴（枪口离手腕约 180px）
    # 经典的召唤器姿势：肘抬起朝向镜头（前臂透视缩短），枪管水平抵住太阳穴
    EVOKER = add(STANCE, body(hx=-3, hy=8, chest=1.5, head=3), evoker_at(TEMPLE, -4), FAR_TENSE)
    TENSE = add(STANCE, body(hx=-1, hy=11, chest=0.5, head=4.5, sy=-0.01), evoker_at((TEMPLE[0] + 3, TEMPLE[1] + 2), -7), FAR_TENSE)
    SQUEEZE = add(TENSE, body(hy=4, head=1.5, sy=-0.006))
    # 后坐：头被"打"得甩向另一侧，胸口一挺，枪口跳起

    KICK = add(STANCE, body(hx=-14, hy=4, spine=-2, chest=-5, head=-17, sy=0.015),
               evoker_at((TEMPLE[0] - 22, TEMPLE[1] - 26), 16, fore_scale=0.66), arm("f", (628, 668), bend=1, hand_rel=0))
    W_L = (372, 500)
    LOWER = add(STANCE, body(hy=8, chest=0.5, head=-2), evoker(W_L, -95, bend=-1), FAR_POCKET)
    HOLSTER = add(STANCE, body(hy=10, chest=-2, head=-5), arm("n", (450, 606), bend=-1, hand_rel=-20), FAR_POCKET)

    def eyes(a, keys):
        a.att(0, "eye_glow", "eye_glow")
        a.alpha("eye_glow", keys)

    a = Anim("aim_in", 0.42).k(0, IDLE).k(0.07, DIP, "out").k(0.17, REACH, "io").k(0.33, EVOKER, "back").k(0.42, EVOKER, "io")
    a.hand(0.15, "n", "evoker")
    eyes(a, [(0, 0), (0.28, 0), (0.42, 0.7)])
    anims.append(a)

    EVOKER_IN = add(EVOKER, body(hy=-2, chest=0.8, head=0.6, sy=0.008))
    a = Anim("aim_loop", 2.0, loop=True).k(0, EVOKER).k(1.0, EVOKER_IN).k(2.0, EVOKER).hand(0, "n", "evoker")
    eyes(a, [(0, 0.7), (1.0, 0.95), (2.0, 0.7)])
    anims.append(a)

    a = (Anim("aim_out", 0.5).k(0, EVOKER).k(0.08, add(EVOKER, body(hy=3, head=-1)), "out").k(0.22, LOWER, "io")
         .k(0.34, HOLSTER, "io").k(0.5, IDLE, "io"))
    a.hand(0, "n", "evoker").hand(0.33, "n", "hang").ev(0.3, "evoker_sheath")
    eyes(a, [(0, 0.7), (0.16, 0)])
    anims.append(a)

    def evoker_cast(name, dur, keys, fire, sheath, swap_in, swap_out, eye, draw=True):
        a = Anim(name, dur)
        for k in keys:
            a.k(*k)
        if swap_in is not None:
            a.hand(swap_in, "n", "evoker")
        else:
            a.hand(0, "n", "evoker")
        a.hand(swap_out, "n", "hang")
        if draw:
            a.ev(0.0, "evoker_draw")
        a.ev(fire, "evoker_fire").ev(sheath, "evoker_sheath")
        eyes(a, eye)
        a.att(0, "glare", "glare")
        a.alpha("glare", [(0, 0), (fire - 0.03, 0), (fire, 1), (fire + 0.3, 1), (fire + 0.45, 0)])
        anims.append(a)

    # 普通施法（召唤器，不召唤 Persona）：0.27s 扣扳机
    evoker_cast("cast", 0.95, [(0, IDLE), (0.07, REACH, "out"), (0.2, EVOKER, "back"), (0.255, TENSE, "io"), (0.272, KICK, "snap"),
                               (0.36, KICK, "soft"), (0.54, EVOKER, "io"), (0.66, LOWER, "io"), (0.79, HOLSTER, "io"), (0.95, IDLE, "io")],
                0.27, 0.82, 0.06, 0.78, [(0, 0), (0.17, 0), (0.24, 0.7), (0.27, 1.0), (0.42, 0.8), (0.64, 0)])
    # 召唤 Persona（君王之刃 + 召唤牌）：枪抵太阳穴停一拍（切入横幅展开）→ 眼中蓝光变亮、身体收紧 → 0.43s 扣扳机 → 后坐 → 慢放下
    evoker_cast("attack_sovereign", 1.1, [(0, IDLE), (0.06, REACH, "out"), (0.17, EVOKER, "back"), (0.3, EVOKER, "soft"),
                                          (0.38, TENSE, "io"), (0.425, SQUEEZE, "io"), (0.437, KICK, "snap"), (0.53, KICK, "soft"),
                                          (0.72, EVOKER, "io"), (0.84, LOWER, "io"), (0.96, HOLSTER, "io"), (1.1, IDLE, "io")],
                0.43, 0.96, 0.05, 0.95, [(0, 0), (0.13, 0), (0.2, 0.6), (0.4, 0.9), (0.43, 1.0), (0.6, 0.8), (0.8, 0)])
    evoker_cast("sovereign_from_aim", 1.06, [(0, EVOKER), (0.14, EVOKER, "soft"), (0.34, TENSE, "io"), (0.392, SQUEEZE, "io"),
                                             (0.404, KICK, "snap"), (0.5, KICK, "soft"), (0.68, EVOKER, "io"), (0.8, LOWER, "io"),
                                             (0.92, HOLSTER, "io"), (1.06, IDLE, "io")],
                0.40, 0.9, None, 0.91, [(0, 0.7), (0.3, 0.9), (0.4, 1.0), (0.56, 0.8), (0.76, 0)], draw=False)
    evoker_cast("cast_from_aim", 0.72, [(0, EVOKER), (0.045, TENSE, "io"), (0.062, KICK, "snap"), (0.15, KICK, "soft"),
                                        (0.3, EVOKER, "io"), (0.42, LOWER, "io"), (0.56, HOLSTER, "io"), (0.72, IDLE, "io")],
                0.06, 0.56, None, 0.55, [(0, 0.7), (0.06, 1.0), (0.2, 0.8), (0.4, 0)], draw=False)

    # ───────────── 剑：扛刀弓步架势（选中剑牌时）→ 斜劈 ─────────────
    # 参照 P3R：右脚（近侧）向前送、屈膝承重；左脚（远侧）撤后伸直蹬地、脚跟微抬；胯前压下沉，上身顺着后腿的斜线前倾；
    # 右手把刀扛到耳后（肘向后张），剑身从后脑后面压过、剑尖朝前上；左手（远侧）伸到胸前、张掌指向敌人；头抬起盯住前方。
    # 前脚 = 远侧腿（往前送、屈膝），后脚 = 近侧腿（撤后蹬直、脚跟微抬）。两腿不交叉（镜像参考图的轮廓）
    LUNGE = feet(n=(-170, -8), f=(230, 0), rn=6)
    W_CARRY = (400, 150)
    B, F = lean(40, 82, -8, -14, -2)
    READY = add(LUNGE, B, sword(W_CARRY, 52), arm("f", (868, 404), bend=-1, hand_rel=-35))
    B, F = lean(38, 78, -7.5, -13, -1.5, sy=0.01)
    READY_IN = add(LUNGE, B, sword((402, 146), 55), arm("f", (872, 398), bend=-1, hand_rel=-31))
    # 劈：后脚再蹬 → 胯前送带上身前倒 → 肩带臂、臂带剑（手腕领先、剑尖滞后，最后一甩）；前膝被压得更弯吸收冲击；
    # 左手猛地收回腰侧（反向扭力，身体才转得过来）
    B, F = lean(38, 92, -2, -4, 6)
    COCK = add(LUNGE, B, sword((385, 136), 72), arm("f", (884, 400), bend=-1, hand_rel=-28))
    B, F = lean(72, 106, -10, -17, -6)
    MID = add(LUNGE, B, sword((906, 275), 40, bend=-1, frame=F), arm("f", (760, 660), bend=-1, hand_rel=0, frame=F))
    B, F = lean(82, 118, -12, -22, -4, sy=-0.02)
    FINISH = add(LUNGE, B, sword((868, 630), -38, bend=-1, frame=F), arm("f", (705, 700), bend=-1, hand_rel=0, frame=F))
    B, F = lean(80, 114, -11.5, -20.5, -3)
    SETTLE = add(LUNGE, B, sword((862, 622), -34, bend=-1, frame=F), arm("f", (708, 698), bend=-1, hand_rel=0, frame=F))
    B, F = lean(79, 112, -11, -19.5, -2.5)
    HOLD = add(LUNGE, B, sword((858, 620), -33, bend=-1, frame=F), arm("f", (710, 698), bend=-1, hand_rel=0, frame=F))
    # 收招：甩一下剑 → 起身，前脚撤回（重心先移到后脚），后脚跟上
    B, F = lean(55, 80, -6, -10, -2)
    FLICK = add(LUNGE, B, sword((760, 690), -100, bend=-1, frame=F), arm("f", (730, 700), bend=-1, hand_rel=-4, frame=F))
    BACK_STEP = add(feet(n=(-120, -6), f=(110, 0), rn=3), body(hx=-10, hy=40, spine=-2, chest=-4, head=1),
                    arm("n", (420, 640), bend=-1, hand_rel=0), arm("f", (690, 680), bend=-1, hand_rel=-4))

    def lunge_feet_in(a, t0, t1):
        """从站姿迈成弓步：右脚抬起向前跨，左脚向后滑（重心先压到左脚）。"""
        tm = t0 + (t1 - t0) * 0.5
        a.kb(tm, {"foot_f": {"x": 120, "y": 52, "r": 8}}, "out")
        a.kb(t0 + (t1 - t0) * 0.6, {"foot_n": {"x": -100, "y": 14, "r": 4}}, "io")

    # 拔剑上肩的路线走身前：手从胯边经过胸前举起，再绕到耳后（不是从身后抡一大圈）
    RAISE = add(feet(n=(-90, 0), f=(140, 34), rf=6), body(hx=20, hy=50, spine=-3, chest=-5, head=4),
                sword((520, 335), 108), arm("f", (800, 470), bend=-1, hand_rel=-30))
    a = (Anim("sword_in", 0.3).k(0, IDLE).k(0.06, add(DIP, body(hx=-8, hy=6, chest=1)), "out")
         .k(0.13, RAISE, "io").k(0.22, add(READY, body(hy=8, chest=-2, head=1)), "out").k(0.3, READY, "io"))
    lunge_feet_in(a, 0.04, 0.22)
    a.hand(0.07, "n", "sword", back=True).far(0.12, kind="palm", front=True)
    anims.append(a)

    a = Anim("ready_sword_loop", 2.0, loop=True).k(0, READY).k(1.0, READY_IN).k(2.0, READY)
    a.hand(0, "n", "sword", back=True).far(0, kind="palm", front=True)
    blink(a, 1.4)
    anims.append(a)

    a = (Anim("sword_out", 0.5).k(0, READY).k(0.08, add(READY, body(hy=6, chest=-1)), "out")
         .k(0.3, BACK_STEP, "io").k(0.5, IDLE, "io"))
    a.kb(0.2, {"foot_f": {"x": 140, "y": 44, "r": 6}}, "io")
    a.kb(0.4, {"foot_n": {"x": -60, "y": 18, "r": 3}}, "io")
    a.hand(0.2, "n", "hang").far(0.3, kind="pocket", front=False).ev(0.2, "weapon_sheath")
    anims.append(a)

    def slash(name, keys, foot_in, events, swap_front, recover_at, from_idle=False):
        # 挥砍时滞后要小：接触帧必须准时（游戏按时结算伤害），只保留一点点"胯先到、剑最后到"
        a = Anim(name, 0.95, lag=0.1)
        for k in keys:
            a.k(*k)
        if foot_in:
            foot_in(a)
        if from_idle:                         # 直接出招：剑在手里出现（weapon_draw），左手边抬边张开
            a.hand(0, "n", "sword").far(0.02, kind="palm", front=True)
        else:
            a.hand(0, "n", "sword", back=True).far(0, kind="palm", front=True)
        a.hand(swap_front, "n", "sword", back=False)
        a.far(swap_front + 0.01, kind="fist", front=False)
        # 收招：前脚撤回（抬起），后脚跟上
        a.kb(recover_at, {"foot_f": {"x": 150, "y": 44, "r": 6}}, "io")
        a.kb(recover_at + 0.16, {"foot_n": {"x": -70, "y": 18, "r": 3}}, "io")
        a.hand(0.7, "n", "hang").far(0.8, kind="pocket")
        for t, e in events:
            a.ev(t, e)
        a.att(0, "glare", "glare")
        a.alpha("glare", [(0, 0), (keys[1][0], 0), (keys[1][0] + 0.03, 1), (0.55, 1), (0.7, 0)])
        anims.append(a)

    rec = [(0.45, HOLD, "soft"), (0.58, FLICK, "io"), (0.76, BACK_STEP, "io"), (0.95, IDLE, "io")]
    # 从架势出招（选中剑牌后打出）：0.065s 接触，0.1s 定格
    slash("attack_from_ready", [(0, READY), (0.026, COCK, "out"), (0.056, MID, "in"), (0.084, FINISH, "out"), (0.14, SETTLE, "back")] + rec,
          None, [(0.025, "weapon_windup"), (0.065, "weapon_slash"), (0.68, "weapon_sheath")], 0.045, 0.62)
    # 直接出招（没有预备）：一步踏进弓步、同时扛刀，0.08s 接触
    STEP = add(feet(n=(-110, -4), f=(160, 0), rn=3), body(hx=24, hy=55, spine=-3, chest=-4, head=6),
               sword((395, 170), 64), arm("f", (820, 430), bend=-1, hand_rel=-30))
    slash("attack", [(0, IDLE), (0.032, STEP, "out"), (0.048, COCK, "out"), (0.07, MID, "in"), (0.098, FINISH, "out"),
                     (0.15, SETTLE, "back")] + rec,
          lambda a: (a.kb(0.018, {"foot_f": {"x": 90, "y": 40, "r": 6}}, "out"), a.kb(0.02, {"foot_n": {"x": -60, "y": 10, "r": 2}}, "out")),
          [(0.0, "weapon_draw"), (0.04, "weapon_windup"), (0.08, "weapon_slash"), (0.7, "weapon_sheath")], 0.06, 0.62, from_idle=True)

    # ───────────── 托掌施法（不召唤）：手心向上托出，低头看手 ─────────────
    GATHER = add(IDLE, body(hy=14, chest=1, head=-3), arm("n", (380, 575), bend=-1, hand_rel=-15))
    OFFER = add(STANCE, body(hx=6, hy=4, spine=-1, chest=-3, head=-7), arm("n", (640, 452), bend=-1, hand_dir=14))
    FLOAT = add(STANCE, body(hx=5, hy=6, spine=-1, chest=-2.5, head=-9), arm("n", (648, 438), bend=-1, hand_dir=18))
    LET_GO = add(STANCE, body(hy=8, chest=-1, head=-4), arm("n", (470, 565), bend=-1, hand_rel=10))
    a = (Anim("cast_light", 0.9).k(0, IDLE).k(0.08, GATHER, "out").k(0.18, OFFER, "back").k(0.44, FLOAT, "soft")
         .k(0.64, LET_GO, "io").k(0.9, IDLE, "io"))
    a.hand(0.09, "n", "palmup").hand(0.62, "n", "hang")
    anims.append(a)

    # ───────────── 受击 / 死亡 ─────────────
    # 受击：被打的是胸口——胸先被推后、胯被带着后移、头最后甩出；两臂向前甩（惯性）；脚不动，膝盖缓冲
    HIT = add(STANCE, body(hx=-34, hy=16, spine=5, chest=12, head=17, sy=-0.02),
              arm("n", (360, 660), bend=-1, hand_rel=14), arm("f", (790, 650), bend=-1, hand_rel=10))
    HIT2 = add(STANCE, body(hx=-40, hy=20, spine=6, chest=14, head=21),
               arm("n", (372, 650), bend=-1, hand_rel=18), arm("f", (800, 645), bend=-1, hand_rel=14))
    REBOUND = add(STANCE, body(hx=6, hy=8, spine=-1, chest=-3, head=-4), arm("n", (318, 700), bend=-1, hand_rel=4),
                  arm("f", (730, 702), bend=-1, hand_rel=-8))
    a = Anim("hurt", 0.7).k(0, IDLE).k(0.05, HIT, "snap").k(0.13, HIT2, "out").k(0.34, REBOUND, "io").k(0.56, IDLE, "io").k(0.7, IDLE, "io")
    a.far(0.02, kind="hang").far(0.5, kind="pocket")                 # 被打时插兜的手甩出来，站稳后插回去
    anims.append(a)

    KNEEL = add(STANCE, body(hx=-20, hy=330, spine=-6, chest=-12, head=-22),
                arm("n", (360, 690), bend=-1, hand_rel=14), arm("f", (730, 690), bend=-1, hand_rel=-4))
    DOWN = add(STANCE, body(hx=30, hy=400, spine=-14, chest=-28, head=-14),
               arm("n", (420, 680), bend=-1, hand_rel=20), arm("f", (690, 690), bend=-1, hand_rel=0))
    a = (Anim("die", 1.5).k(0, IDLE).k(0.06, HIT, "snap").k(0.2, HIT2, "out").k(0.55, KNEEL, "in").k(0.62, add(KNEEL, body(hy=-12)), "out")
         .k(0.95, DOWN, "in").k(1.02, add(DOWN, body(hy=-8, chest=3)), "out").k(1.5, DOWN, "io"))
    a.far(0.03, kind="hang")
    a.ev(0.1, "death_particles_start").ev(1.1, "death_particles_end").ev(1.2, "explode_dead").ev(1.35, "explode_end")
    a.att(0, "blink", "blink")
    a.alpha("blink", [(0, 0), (0.5, 0), (0.6, 1), (1.5, 1)])
    anims.append(a)
    return anims
