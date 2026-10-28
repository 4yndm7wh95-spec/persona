"""P5 Joker combat rig (replaces the Silent): every pose is a whole picture, swapped on a timeline.

Each animation is a list of (pose, seconds[, fade]) taken from docs/p5/ART_SPEC.md. Two slots carry the
pictures: `pose` holds the current one, `fade` draws the next one on top fading in over `fade` seconds,
so a switch never pops. The standing pictures of the loops (idle_a3, relaxed_a) are weighted meshes
(tools/rig/mesh_rig.py) so they visibly breathe: chest and shoulders rise, the head sways slowly, and each
coat tail swings as a two-joint chain with the lower joint lagging, so the cloth ripples instead of
bending stiffly (user 2026-10-28: clearly visible, but fine and smooth - the P3 mod's motion was too faint).
The idle eye patch (art/p5/poses/idle_blink_patch.png) rides on the idle head bone, and the idle loop
ends with one glove tug, so it plays about 8 s after the last action (the user's "tug the glove when idle").
Pictures come from art/p5/poses/final/ (1536x1632, feet aligned to idle_a3), are cropped to their alpha
and stored at TEX of that size; attachments scale them back up to K skeleton units per final pixel.
Output: art/p5/rig/joker.{spine-json,atlas,png} + GIF previews in art/p5/rig/preview/.
    python tools/p5/build_rig_p5.py [--no-preview] [animation names to preview, default all]
"""
import json
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools/rig"))
from spine_eval import Skeleton  # noqa: E402
from mesh_rig import MeshRig  # noqa: E402

FINAL = ROOT / "art/p5/poses/final"
POSES = ROOT / "art/p5/poses"
OUT = ROOT / "art/p5/rig"
K = 0.87           # skeleton units per final-canvas pixel. 0.6 matched the Silent's size but read small next to the
                   # approved P3 Makoto in game (2026-10-28: ~260 vs ~380 px tall at 1080p), so x1.45
TEX = 0.42         # stored texture size per final-canvas pixel (sharp up to ~4K)
ORIGIN = (815, 1622)   # final-canvas pixel that is the skeleton root: between idle_a3's feet, on the ground
FADE = 0.05
GHOST_IOU = 0.9
PAGE = 4096

# ── animations (ART_SPEC.md, user-approved timings) ──
GLOVE = [("glove_mid_a", .09), ("glove_reach_a", .12), ("glove_a", .16), ("glove_b", .3), ("glove_flex_a", .45),
         ("idle_a3", .3, .12)]
SUMMON_UP = ["summon_lift1_a", "summon_lift2_a", "summon_touch_mid_a", "summon_touch2_a"]
# gun (user 2026-10-28, 3rd round): the gun appears out of thin air spinning on the index finger (gun_flick_c, empty
# hand + the separate pistol on the `gun` bone, GUN_PROP below), is caught aimed (gun_aim_b), kicks up on the shot
# (gun_recoil_b, held as a beat), spins on the finger again and vanishes (gun_twirl_out_c). The left hand stays in
# the pocket throughout. aim_in is exactly the first 0.18 s of shiv, so a Shiv played mid-twirl carries on from
# the same point (p5mod/src/Aim.cs)
# 4th round: the finger the gun spins on points at the camera (gun_flick_d / gun_twirl_out_d), so the in-plane spin
# around the fingertip is physically right; pocket_in_d eases the left hand into / out of the pocket
# 5th round (user's own hand photo, art/p5/ref/spin_hand_ref.jpg): the arm stays out at the firing reach and the gun
# spins on the relaxed index finger (gun_spin_g = Codex's gun_spin_f with the finger joint bent so it reads,
# tools/p5/bend_finger.py), then is caught in gun_aim_b's grip; pocket_in_e eases the left hand in and out
# gun_arm_mid_h (arm half raised) between the hanging arm and the spin, both ways (user: the arm jumped up / dropped)
GUN_IN = [("idle_a3", .01), ("pocket_in_e", .03, .015), ("gun_arm_mid_h", .03, .015), ("gun_spin_g", .05, .015),
          ("gun_aim_b", .06, 0)]
# (user: the hand left the pocket too fast - pocket_in_e and the fade into idle are held longer)
GUN_OUT = [("gun_spin_g", .3, 0), ("gun_arm_mid_h", .1, .02), ("pocket_in_e", .1, .04), ("idle_a3", .14, .08)]
GUN_SHOT = [("gun_recoil_b", .2, 0)] + GUN_OUT
KNIFE_IN = [("idle_a3", .01), ("knife_spin_e", .08, 0), ("knife_ready_b", .05, 0)]
KNIFE_OUT = [("knife_spin_e", .26, 0), ("idle_a3", .12, .05)]
IDLE_LOOP = 11.0   # 8 s idle (+ blinks), then the glove tug, ending back on idle_a3
ANIMS = {
    "idle_loop": [("idle_a3", 8.6)] + GLOVE + [("idle_a3", IDLE_LOOP - 8.6 - sum(f[1] for f in GLOVE))],
    "low_health_loop": [("idle_a3", 6.0)],
    "relaxed_loop": [("relaxed_a", 6.0)],
    # the game deals the damage AttackAnimDelay (0.15 s for the Silent) after the attack starts: the cut lands at
    # 0.14 s and the whole strike is ~0.6 s (user 2026-10-28: the first, ~0.85 s version lagged behind the damage)
    # 2026-10-28: the left hand no longer hangs idle - it comes up to the chest on the wind-up (knife_ready_b)
    # and is pulled back behind the waist on the cut (slash_a10); the cut is a snap (no smear: the blade trail
    # will be a code effect), the hit-stop below sells it
    # 2026-10-28: the knife is spun at the hip on the draw (KNIFE_PROP; cut still at 0.14 s) and again after the strike
    "attack": KNIFE_IN + [("slash_a10", .16, 0), ("slash_follow_a", .06), ("knife_return_a", .06)] + KNIFE_OUT,
    # picking up an attack card: spin the knife out and hold it ready (like aim_in for the gun); knife_in is exactly
    # the first 0.14 s of attack, so a card played mid-spin carries on from the same point (p5mod/src/Aim.cs)
    "knife_in": KNIFE_IN,
    "knife_loop": [("knife_ready_b", 4.8)],
    "knife_out": [("knife_ready_b", .01)] + KNIFE_OUT,
    "attack_from_ready": [("knife_ready_b", .14), ("slash_a10", .16, 0), ("slash_follow_a", .06), ("knife_return_a", .06)]
                         + KNIFE_OUT,
    # the Shiv card deals its damage 0.2 s after the anim starts (Shiv.cs WithAttackerAnim("Shiv", 0.2f)): fire at 0.18 s
    "shiv": GUN_IN + GUN_SHOT,
    # attack_chain: an attack played while the previous strike is still recovering (user 2026-10-28: fast play
    # restarted from the draw and visibly broke) - re-cock straight from the knife in hand, cut still at 0.14 s
    "attack_chain": [("knife_return_a", .03), ("knife_ready_b", .11, .03), ("slash_a10", .16, 0), ("slash_follow_a", .06),
                     ("knife_return_a", .06), ("slash_recover_a", .08), ("idle_a3", .1, .06)],
    # aim: picking up a Shiv card raises the gun and holds it (p5mod/src/Aim.cs); playing it fires from the
    # aim (shiv_from_aim, shot at 0.18 s like "shiv"); dropping the card lowers it (aim_out)
    "aim_in": GUN_IN,
    "aim_loop": [("gun_aim_b", 4.8)],
    "aim_out": [("gun_aim_b", .01)] + GUN_OUT,
    "shiv_from_aim": [("gun_aim_b", .18)] + GUN_SHOT,
    # cast variants picked per card by p5mod/src (Aim.Rename): guard (block cards), glove tug (card tricks /
    # buffs), the big summon (mask ripped off) for the large Persona cards
    "cast_guard": [("idle_a3", .02), ("guard_mid_a", .06), ("guard_a2", .45), ("guard_mid_a", .08), ("idle_a3", .12, .07)],
    "cast_glove": [("idle_a3", .02)] + GLOVE,
    "cast_big": [("idle_a3", .02)] + [(n, .07) for n in SUMMON_UP] + [("summon_hold_a", .35), ("summon_rip_a2", .12),
                 ("summon_open1_b", .07), ("summon_open2_b", .07), ("summon_release_b", .8), ("summon_settle_b", .1),
                 ("summon_down1_e", .1), ("summon_down05_c", .1), ("summon_down25_c", .1), ("idle_a3", .15, .08)],
    "cast": [("idle_a3", .02)] + [(n, .08) for n in SUMMON_UP] + [("summon_hold_a", .9)]
            + [(n, .08) for n in SUMMON_UP[::-1]] + [("idle_a3", .15, .08)],
    "hurt": [("hurt_flinch_a", .05, 0), ("hurt_a4", .25), ("hurt_recover_b1", .1), ("hurt_recover_b2", .1),
             ("idle_a3", .2, .12)],
    "die": [("die_buckle_a", .12, .06), ("die_kneel_a2", .6), ("die_fall_a", .15), ("die_down_b", 1.0)],
}
LOOPS = {"idle_loop", "low_health_loop", "relaxed_loop"}
# Impact on the `body` bone (pivot at the feet; user 2026-10-28: the strike felt weak, no impact). Keys are
# (time, dx, dy, scaleX, scaleY, rotation): anticipation (pull back and crouch), a snap forward with a stretch,
# a squash and a short shaking hit-stop on the cut, then an eased recovery. The gun kicks back on the recoil
# frame; a hit knocks him back. "@pose" times are relative to that pose's start in the animation.
IMPACT = {
    "attack": [(0, 0, 0, 1, 1, 0), (.06, -10, 0, 1.01, .985, 1), (.12, -24, -4, 1.025, .965, 2),
               (.14, 95, 0, .97, 1.035, -3), (.16, 112, 0, 1.035, .965, -2.2), (.18, 120, 0, 1.03, .97, -2.2),
               (.20, 104, 0, 1.03, .97, -2), (.22, 116, 0, 1.025, .975, -2), (.24, 107, 0, 1.02, .98, -1.8),
               (.27, 111, 0, 1.015, .985, -1.6), (.40, 72, 0, 1.0, 1.0, -.6), (.56, 4, 0, 1, 1, 0), (.62, 0, 0, 1, 1, 0)],
    # holding the aim: a slow breath on the whole body (the aim frame has no soft mesh)
    "aim_loop": [(0, 0, 0, 1, 1, 0), (1.2, 0, 2, .997, 1.006, -.15), (2.4, 0, 0, 1, 1, 0), (3.6, 0, 2, .997, 1.006, -.15),
                 (4.8, 0, 0, 1, 1, 0)],
    "knife_loop": [(0, 0, 0, 1, 1, 0), (1.2, 0, 2, .997, 1.006, -.15), (2.4, 0, 0, 1, 1, 0), (3.6, 0, 2, .997, 1.006, -.15),
                   (4.8, 0, 0, 1, 1, 0)],
    # from the held guard: a short wind-up into the same cut as "attack"
    "attack_from_ready": [(0, 0, 0, 1, 1, 0), (.08, -10, 0, 1.01, .985, 1), (.12, -24, -4, 1.025, .965, 2),
                          (.14, 95, 0, .97, 1.035, -3), (.16, 112, 0, 1.035, .965, -2.2), (.18, 120, 0, 1.03, .97, -2.2),
                          (.20, 104, 0, 1.03, .97, -2), (.22, 116, 0, 1.025, .975, -2), (.24, 107, 0, 1.02, .98, -1.8),
                          (.27, 111, 0, 1.015, .985, -1.6), (.40, 72, 0, 1.0, 1.0, -.6), (.56, 4, 0, 1, 1, 0), (.62, 0, 0, 1, 1, 0)],
    "shiv": [("@gun_recoil_b", 0, 0, 0, 1, 1, 0), ("@gun_recoil_b", .02, -30, 0, .985, 1.01, 2.5),
             ("@gun_recoil_b", .04, -21, 0, .99, 1.005, 2), ("@gun_recoil_b", .06, -27, 0, .99, 1.005, 2),
             ("@gun_recoil_b", .12, -12, 0, 1, 1, .8), ("@gun_recoil_b", .26, 0, 0, 1, 1, 0)],
    "attack_chain": [(0, 30, 0, 1, 1, 0), (.07, -10, 0, 1.01, .985, 1), (.12, -24, -4, 1.025, .965, 2),
                     (.14, 95, 0, .97, 1.035, -3), (.16, 112, 0, 1.035, .965, -2.2), (.18, 120, 0, 1.03, .97, -2.2),
                     (.20, 104, 0, 1.03, .97, -2), (.22, 116, 0, 1.025, .975, -2), (.24, 107, 0, 1.02, .98, -1.8),
                     (.27, 111, 0, 1.015, .985, -1.6), (.40, 72, 0, 1.0, 1.0, -.6), (.56, 4, 0, 1, 1, 0), (.60, 0, 0, 1, 1, 0)],
    "shiv_from_aim": [("@gun_recoil_b", 0, 0, 0, 1, 1, 0), ("@gun_recoil_b", .02, -30, 0, .985, 1.01, 2.5),
                      ("@gun_recoil_b", .04, -21, 0, .99, 1.005, 2), ("@gun_recoil_b", .06, -27, 0, .99, 1.005, 2),
                      ("@gun_recoil_b", .12, -12, 0, 1, 1, .8), ("@gun_recoil_b", .26, 0, 0, 1, 1, 0)],
    "hurt": [(0, 0, 0, 1, 1, 0), (.03, -55, 0, .975, 1.01, 3), (.06, -64, 0, .975, 1.01, 3.2), (.09, -54, 0, .98, 1.01, 3),
             (.12, -60, 0, .985, 1.005, 2.8), (.28, -34, 0, 1, 1, 1.5), (.48, -6, 0, 1, 1, .2), (.62, 0, 0, 1, 1, 0)],
}

# the separate pistol (art/p5/poses/pistol_b.png) on the `gun` bone: keys are (time, x, y, rotation, scale, gun alpha,
# spin-trail alpha) with x, y on the final canvas (the finger joint it spins on) and rotation in degrees (0 = aimed
# right, Spine's counter-clockwise positive; it spins clockwise, so the rotation counts down). "@pose" times are
# relative to that pose's start ("@@pose": its last start, for a pose used twice). The gun is only shown between the
# first and the last key.
PISTOL = POSES / "pistol_b.png"
PISTOL_GUARD = (518.6, 501.8)      # trigger-guard hole on pistol_b.png: the spin pivot (on the finger)
PISTOL_MUZZLE = (984, 300)
PISTOL_K = 134 / 940               # pistol_b pixels -> final canvas (the gun in gun_aim_b is ~134 px long)
SPIN, AIM_GUARD = (1285, 344), (1275, 340)      # the spinning finger (measured on the final) / aim_b's trigger guard
# (user: the gun floated - it spun >100 deg a frame and hid the finger. The way in only turns ~half a turn, slowing
# into the catch; the way out has time, so it turns 1.8 turns over 0.3 s, easing out; and the fingertip is drawn over
# the gun (FINGER_TIP) so the finger visibly goes through the trigger guard)
# (user: some spin frames had a tiny / washed-out gun - it no longer scales in from 0.4 or out to 0.05: it stays a
# touch bigger than in the hand while spinning and only pops in / fades out over the first / last frame; the trail
# is a thin half-transparent line so the gun reads)
SPIN_IN = [("@gun_spin_g", 0, *SPIN, 230, 1.1, 0, 0), ("@gun_spin_g", .012, *SPIN, 160, 1.1, 1, .5),
           ("@gun_spin_g", .028, *SPIN, 80, 1.1, 1, .45), ("@gun_spin_g", .042, *SPIN, 15, 1.05, 1, .15),
           ("@gun_aim_b", 0, *AIM_GUARD, 0, 1, 1, 0)]
SPIN_OUT = [("@@gun_spin_g", 0, *SPIN, 60, 1.05, 1, 0), ("@@gun_spin_g", .04, *SPIN, -60, 1.1, 1, .45),
            ("@@gun_spin_g", .09, *SPIN, -220, 1.1, 1, .5), ("@@gun_spin_g", .15, *SPIN, -390, 1.1, 1, .45),
            ("@@gun_spin_g", .21, *SPIN, -530, 1.1, 1, .35), ("@@gun_spin_g", .26, *SPIN, -620, 1.1, 1, .2),
            ("@@gun_spin_g", .29, *SPIN, -660, 1.1, 0, 0)]
FINGER_TIP = ("gun_spin_g", (1279, 316, 1322, 370))   # the index finger from the trigger guard to the tip (final canvas)
# each animation lists the prop's spin segments; the loose prop (and its trail / the fingertip) is only shown
# within a segment - between them the prop drawn in the pose picture takes over
GUN_PROP = {"aim_in": [SPIN_IN], "shiv": [SPIN_IN, SPIN_OUT], "shiv_from_aim": [SPIN_OUT], "aim_out": [SPIN_OUT]}

# the knife (user 2026-10-28: spin it the same way - on the draw at the hip, then cut upwards; and again at the hip
# after the strike). art/p5/props/knife_c.png is drawn in code (tools/p5/draw_knife.py), pivot at the middle of
# the grip; knife_spin_e is the empty hand at the hip (Codex). Rotation 0 = pointing right; knife_ready_b holds it
# pointing down-left (~150 deg on screen = -150 here)
KNIFE = POSES.parent / "props/knife_c.png"
KNIFE_PIVOT, KNIFE_TIP = (53, 30), (306, 31)
KNIFE_HAND = (712, 688)                     # palm of the empty hand in knife_spin_e (final canvas)
KSPIN_IN = [("@knife_spin_e", 0, *KNIFE_HAND, 200, 1, 0, 0), ("@knife_spin_e", .02, *KNIFE_HAND, 80, 1, 1, .6),
            ("@knife_spin_e", .045, *KNIFE_HAND, -40, 1, 1, .6), ("@knife_spin_e", .065, *KNIFE_HAND, -120, 1, 1, .3),
            ("@knife_ready_b", 0, 720, 725, -150, 1, 1, 0)]
KSPIN_OUT = [("@@knife_spin_e", 0, *KNIFE_HAND, -90, 1, 1, 0), ("@@knife_spin_e", .04, *KNIFE_HAND, -210, 1, 1, .6),
             ("@@knife_spin_e", .09, *KNIFE_HAND, -370, 1, 1, .6), ("@@knife_spin_e", .15, *KNIFE_HAND, -510, 1, 1, .4),
             ("@@knife_spin_e", .2, *KNIFE_HAND, -600, 1, 1, .2), ("@@knife_spin_e", .24, *KNIFE_HAND, -640, 1, 0, 0)]
KNIFE_PROP = {"attack": [KSPIN_IN, KSPIN_OUT], "knife_in": [KSPIN_IN], "knife_out": [KSPIN_OUT],
              "attack_from_ready": [KSPIN_OUT]}

# soft-mesh pictures: bone -> (parent, pivot on the final canvas, influence polys / circles, feather)
MESHES = {
    "idle_a3": {
        "chest": ("root", (800, 900), [[(560, 330), (1100, 330), (1120, 760), (800, 800), (560, 760)]], None, 120),
        "head": ("chest", (790, 300), [[(700, 240), (900, 240), (890, 320), (710, 320)]], [(805, 210, 125)], 50),
        "coat_l1": ("root", (540, 860), [[(290, 880), (580, 880), (620, 1420), (420, 1440), (270, 1270)]], None, 70),
        "coat_l2": ("coat_l1", (440, 1120), [[(260, 1060), (560, 1060), (600, 1440), (400, 1460), (250, 1280)]], None, 70),
        "coat_r1": ("root", (1000, 860), [[(980, 860), (1160, 860), (1260, 1310), (1180, 1410), (990, 1390)]], None, 60),
        "coat_r2": ("coat_r1", (1110, 1110), [[(1040, 1060), (1190, 1060), (1270, 1320), (1190, 1420), (1040, 1400)]], None, 60),
    },
    "relaxed_a": {
        "chest": ("root", (720, 880), [[(500, 330), (930, 330), (940, 720), (720, 780), (500, 720)]], None, 120),
        "head": ("chest", (750, 300), [[(660, 230), (860, 230), (850, 320), (670, 320)]], [(745, 200, 125)], 50),
        "coat_l1": ("root", (560, 860), [[(280, 880), (600, 880), (620, 1420), (420, 1440), (270, 1300)]], None, 70),
        "coat_l2": ("coat_l1", (450, 1110), [[(260, 1060), (580, 1060), (610, 1440), (400, 1460), (250, 1300)]], None, 70),
        "coat_r1": ("root", (920, 820), [[(900, 760), (1140, 760), (1160, 1400), (1040, 1420), (930, 1300)]], None, 60),
        "coat_r2": ("coat_r1", (1020, 1100), [[(950, 1040), (1150, 1040), (1170, 1410), (1040, 1430), (960, 1300)]], None, 60),
    },
}
MESH_PREFIX = {"idle_a3": "i_", "relaxed_a": "r_"}
BLINKS = {"idle_loop": [2.6, 6.1, 6.35], "low_health_loop": [2.4]}   # blink starts (s); a blink lasts 0.12 s
BLINK_LEN = 0.12


_MASKS = {}


def silhouette_iou(a, b):
    """overlap of two poses' silhouettes (final canvas, 1/8 size)."""
    for n in (a, b):
        if n not in _MASKS:
            im = Image.open(FINAL / f"{n}.png").getchannel("A")
            _MASKS[n] = np.array(im.resize((im.width // 8, im.height // 8))) > 40
    ma, mb = _MASKS[a], _MASKS[b]
    return (ma & mb).sum() / max(1, (ma | mb).sum())


def frames(anim):
    """[(pose, start, dur, fade)]. A crossfade shows both pictures at once, so (user 2026-10-28: "two frames at
    once") it is only kept between near-identical poses (silhouette overlap >= GHOST_IOU), capped at 0.03 s;
    anything else is a hard cut."""
    out, t = [], 0.0
    for i, f in enumerate(ANIMS[anim]):
        fade = f[2] if len(f) > 2 else (0.0 if i == 0 else FADE)
        if i > 0 and fade > 0:
            prev = ANIMS[anim][i - 1][0]
            fade = 0.0 if prev != f[0] and silhouette_iou(prev, f[0]) < GHOST_IOU else min(fade, 0.03)
        out.append((f[0], t, f[1], fade))
        t += f[1]
    return out


def crop(name):
    im = Image.open(FINAL / f"{name}.png").convert("RGBA")
    box = im.getchannel("A").point(lambda v: 255 if v > 3 else 0).getbbox()
    box = (max(0, box[0] - 2), max(0, box[1] - 2), min(im.width, box[2] + 2), min(im.height, box[3] + 2))
    c = im.crop(box)
    return c.resize((max(1, round(c.width * TEX)), max(1, round(c.height * TEX))), Image.LANCZOS), box


def blink_patch():
    """idle eye patch on the final canvas (idle_a3's normalize offset)."""
    meta = json.loads((POSES / "idle_blink_patch.json").read_text())
    off = json.loads((FINAL / "normalize.json").read_text())["idle_a3"]["offset"]
    im = Image.open(POSES / "idle_blink_patch.png").convert("RGBA")
    x0, y0 = meta["x"] + off[0], meta["y"] + off[1]
    box = (x0, y0, x0 + im.width, y0 + im.height)
    return im.resize((max(1, round(im.width * TEX)), max(1, round(im.height * TEX))), Image.LANCZOS), box


def shadow():
    w, h = 520, 90
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    ImageDraw.Draw(im).ellipse([20, 15, w - 20, h - 15], fill=(0, 0, 0, 110))
    return im.filter(ImageFilter.GaussianBlur(12))


def trail(radius, a0):
    """white spin trail: a crescent on a circle of `radius` (texture px) behind angle a0, 150 deg long, thinning."""
    size = int(2 * radius + 30)
    o = size / 2
    big = Image.new("RGBA", (size * 2, size * 2), (0, 0, 0, 0))
    d = ImageDraw.Draw(big)
    n = 90
    for k in range(n):
        u = k / n
        a = a0 - math.radians(150) * u
        w = 2 * (5 * (1 - u) ** 1.3 + 1.2)
        x, y = 2 * (o + radius * math.cos(a)), 2 * (o + radius * math.sin(a))
        d.ellipse([x - w, y - w, x + w, y + w], fill=(255, 255, 255, round(255 * (1 - u) ** 0.9)))
    return big.resize((size, size), Image.LANCZOS)


def knife_textures():
    im = Image.open(KNIFE).convert("RGBA")
    c = im.resize((round(im.width * TEX), round(im.height * TEX)), Image.LANCZOS)
    cx, cy = im.width / 2, im.height / 2
    knife = {"tex": c, "att": {"x": round((cx - KNIFE_PIVOT[0]) * K, 2), "y": round((KNIFE_PIVOT[1] - cy) * K, 2),
                               "scaleX": round(K / TEX, 4), "scaleY": round(K / TEX, 4), "width": c.width, "height": c.height}}
    t = trail((KNIFE_TIP[0] - KNIFE_PIVOT[0]) * TEX, 0.0)
    smear = {"tex": t, "att": {"x": 0, "y": 0, "scaleX": round(K / TEX, 4), "scaleY": round(K / TEX, 4),
                               "width": t.width, "height": t.height}}
    return knife, smear


def gun_textures():
    """the pistol (pivot on its trigger guard) and a white spin trail behind its muzzle, stored at TEX like the poses
    (a bigger texture shrunk on screen sparkles while it spins)."""
    scale = TEX * PISTOL_K
    im = Image.open(PISTOL).convert("RGBA")
    box = im.getchannel("A").point(lambda v: 255 if v > 3 else 0).getbbox()
    c = im.crop(box)
    c = c.resize((round(c.width * scale), round(c.height * scale)), Image.LANCZOS)
    cx, cy = (box[0] + box[2]) / 2, (box[1] + box[3]) / 2
    pistol = {"tex": c, "att": {"x": round((cx - PISTOL_GUARD[0]) * PISTOL_K * K, 2), "y": round((PISTOL_GUARD[1] - cy) * PISTOL_K * K, 2),
                                "scaleX": round(K / TEX, 4), "scaleY": round(K / TEX, 4), "width": c.width, "height": c.height}}
    # trail: a crescent on the muzzle's circle, thickest at the muzzle, fading and thinning over 150 degrees behind it
    # (the gun spins clockwise on screen, so "behind" is counter-clockwise: above the muzzle when aimed)
    dx, dy = (PISTOL_MUZZLE[0] - PISTOL_GUARD[0]) * scale, (PISTOL_MUZZLE[1] - PISTOL_GUARD[1]) * scale
    r, a0 = math.hypot(dx, dy), math.atan2(dy, dx)
    size = int(2 * r + 30)
    o = size / 2
    big = Image.new("RGBA", (size * 2, size * 2), (0, 0, 0, 0))
    d = ImageDraw.Draw(big)
    n = 90
    for k in range(n):
        u = k / n
        a = a0 - math.radians(150) * u
        w = 2 * (5 * (1 - u) ** 1.3 + 1.2)
        x, y = 2 * (o + r * math.cos(a)), 2 * (o + r * math.sin(a))
        d.ellipse([x - w, y - w, x + w, y + w], fill=(255, 255, 255, round(255 * (1 - u) ** 0.9)))
    trail = big.resize((size, size), Image.LANCZOS)
    smear = {"tex": trail, "att": {"x": 0, "y": 0, "scaleX": round(K / TEX, 4), "scaleY": round(K / TEX, 4),
                                   "width": size, "height": size}}
    return pistol, smear


def finger_tip():
    """the spinning finger's tip as its own picture, drawn over the gun; feathered on the knuckle side."""
    name, box = FINGER_TIP
    im = Image.open(FINAL / f"{name}.png").convert("RGBA").crop(box)
    a = np.array(im).astype(float)
    ramp = np.clip((np.arange(a.shape[1]) - 1) / 4.0, 0, 1)          # 4 px soft start where it enters the guard
    a[..., 3] *= ramp[None, :]
    im = Image.fromarray(a.astype(np.uint8))
    return im.resize((max(1, round(im.width * TEX)), max(1, round(im.height * TEX))), Image.LANCZOS), box


def region(box, tex):
    """region attachment for a final-canvas box, root at ORIGIN, y up."""
    cx, cy = (box[0] + box[2]) / 2, (box[1] + box[3]) / 2
    return {"x": round((cx - ORIGIN[0]) * K, 2), "y": round((ORIGIN[1] - cy) * K, 2),
            "scaleX": round(K * (box[2] - box[0]) / tex.width, 4),
            "scaleY": round(K * (box[3] - box[1]) / tex.height, 4), "width": tex.width, "height": tex.height}


def pack(images):
    items = sorted(images.items(), key=lambda kv: -kv[1].height)
    x = y = shelf = 0
    pos = {}
    for name, im in items:
        if x + im.width + 2 > PAGE:
            x, y, shelf = 0, y + shelf + 2, 0
        pos[name] = (x, y)
        x += im.width + 2
        shelf = max(shelf, im.height)
    h = 1
    while h < y + shelf:
        h *= 2
    if h > PAGE:
        raise SystemExit(f"atlas needs {PAGE}x{h}: lower TEX")
    sheet = Image.new("RGBA", (PAGE, h), (0, 0, 0, 0))
    lines = ["joker.png", f"size:{PAGE},{h}", "filter:Linear,Linear", "pma:false"]
    for name, im in items:
        sheet.paste(im, pos[name])
        lines += [name, f"bounds:{pos[name][0]},{pos[name][1]},{im.width},{im.height}"]
    return sheet, "\n".join(lines) + "\n"


def hexa(a):
    return f"ffffff{max(0, min(255, round(a * 255))):02x}"


def soft_mesh(pose, bone_index):
    """weighted mesh for a loop picture; its bones get the pose's prefix. Returns (bones, mesh, texture)."""
    pre = MESH_PREFIX[pose]
    rig = MeshRig(Image.open(FINAL / f"{pose}.png"), origin=ORIGIN, unit=K, step=24, tex_scale=TEX, name=pose)
    for b, (parent, pivot, polys, circles, feather) in MESHES[pose].items():
        rig.bone(b, parent, pivot, polys=polys, circles=circles, feather=feather)
    rig.priority = ["head", "coat_l2", "coat_r2", "coat_l1", "coat_r1", "chest"]
    setup = rig.setup()
    bones = []
    for b in setup["bones"][1:]:
        nb = dict(b, name=pre + b["name"])
        nb["parent"] = "body" if b["parent"] == "root" else pre + b["parent"]     # follow the impact moves
        bones.append(nb)
    for k, b in enumerate(bones):
        bone_index[b["name"]] = len(bone_index)
    local = {n: k for k, (n, _, _) in enumerate(rig.bones)}       # mesh_rig's own indices -> ours
    remap = {k: (bone_index["body"] if n == "root" else bone_index[pre + n]) for n, k in local.items()}
    mesh = setup["skins"][0]["attachments"][pose][pose]
    v, out, i = mesh["vertices"], [], 0
    while i < len(v):
        n = v[i]
        out.append(n)
        for k in range(n):
            bi, x, y, w = v[i + 1 + 4 * k:i + 5 + 4 * k]
            out += [remap[bi], x, y, w]
        i += 1 + 4 * n
    mesh = dict(mesh, vertices=out)
    img = rig.images()[pose]
    img = img.resize((max(1, round(img.width * TEX)), max(1, round(img.height * TEX))), Image.LANCZOS)
    return bones, mesh, img, {n: rig.sp(p) for n, _, p in rig.bones}


def motion(pre, t, length):
    """bone timelines for one soft-mesh picture at time t of a loop `length` s long (whole cycles, so it loops)."""
    breath = 2 * math.pi * t / (length / max(1, round(length / 2.8)))
    sway = 2 * math.pi * t / (length / max(1, round(length / 5.5)))
    wind = 2 * math.pi * t / (length / max(1, round(length / 3.6)))
    b = math.sin(breath)
    return {
        pre + "chest": {"sy": 1 + 0.013 * b, "sx": 1 - 0.004 * b, "r": 0.45 * math.sin(sway)},
        pre + "head": {"r": 1.3 * math.sin(sway - 0.6) - 0.5 * b},
        pre + "coat_l1": {"r": 1.8 * math.sin(wind)},
        pre + "coat_l2": {"r": 2.8 * math.sin(wind - 0.9)},
        pre + "coat_r1": {"r": -1.5 * math.sin(wind + 0.4)},
        pre + "coat_r2": {"r": -2.4 * math.sin(wind - 0.5)},
    }


def bake_motion(pre, length):
    tl = {}
    n = round(length * 15)
    for k in range(n + 1):
        t = round(length * k / n, 4)
        for bone, ch in motion(pre, 0.0 if k == n else t, length).items():
            e = tl.setdefault(bone, {"rotate": [], "scale": []})
            e["rotate"].append({"time": t, "value": round(ch.get("r", 0.0), 3)})
            e["scale"].append({"time": t, "x": round(ch.get("sx", 1.0), 5), "y": round(ch.get("sy", 1.0), 5)})
    return tl


def prop_timelines(fr, segments, slot_defs):
    """slot + bone timelines for a loose prop: keys are (time, x, y, rotation, scale, prop alpha, trail alpha) with
    "@pose" / "@@pose" times (that pose's first / last start); each segment shows the prop from its first to its
    last key."""
    first_at, last_at = {}, {}
    for p, t0, dur, fade in fr:
        first_at.setdefault(p, t0)
        last_at[p] = t0
    segs = [[(round((last_at[k[0][2:]] if k[0].startswith("@@") else first_at[k[0][1:]]) + k[1], 4), *k[2:]) for k in seg]
            for seg in segments]
    keys = [k for seg in segs for k in seg]
    slots = {}
    for slot, att_name, col in slot_defs:
        shown = [{"time": 0, "name": None}]
        for seg in segs:
            shown += [{"time": seg[0][0], "name": att_name}, {"time": seg[-1][0], "name": None}]
        slots[slot] = {"attachment": shown}
        if col is not None:
            slots[slot]["rgba"] = [{"time": k[0], "color": hexa(k[col])} for k in keys]
    bone = {"translate": [{"time": k[0], "x": round((k[1] - ORIGIN[0]) * K, 2), "y": round((ORIGIN[1] - k[2]) * K, 2)}
                          for k in keys],
            "rotate": [{"time": k[0], "value": k[3]} for k in keys],
            "scale": [{"time": k[0], "x": k[4], "y": k[4]} for k in keys]}
    return slots, bone


def build():
    used = sorted({f[0] for a in ANIMS.values() for f in a})
    tex, att = {}, {"pose": {}, "fade": {}}
    bone_index = {"root": 0, "body": 1}
    bones = [{"name": "root"}, {"name": "body", "parent": "root"}]
    heads = {}
    for n in used:
        if n in MESHES:
            bs, mesh, img, world = soft_mesh(n, bone_index)
            bones += bs
            heads[n] = world["head"]
            tex[n] = img
            att["pose"][n] = att["fade"][n] = mesh
            continue
        t, box = crop(n)
        tex[n] = t
        att["pose"][n] = att["fade"][n] = region(box, t)
    bt, bbox = blink_patch()
    tex["idle_blink"] = bt
    r = region(bbox, bt)
    hx, hy = heads["idle_a3"]
    att["blink"] = {"idle_blink": dict(r, x=round(r["x"] - hx, 2), y=round(r["y"] - hy, 2))}   # on the idle head bone
    pistol, smear = gun_textures()
    ft, fbox = finger_tip()
    tex["finger_tip"] = ft
    att["finger_tip"] = {"finger_tip": region(fbox, ft)}
    tex["pistol"], tex["gun_smear"] = pistol["tex"], smear["tex"]
    knife, ksmear = knife_textures()
    tex["knife"], tex["knife_smear"] = knife["tex"], ksmear["tex"]
    att["knife"] = {"knife": knife["att"]}
    att["knife_smear"] = {"knife_smear": ksmear["att"]}
    att["gun"] = {"pistol": pistol["att"]}
    att["gun_smear"] = {"gun_smear": smear["att"]}
    sh = shadow()
    tex["shadow"] = sh
    att["shadow"] = {"shadow": {"x": 0, "y": 0, "width": sh.width, "height": sh.height}}

    anims = {}
    for name in ANIMS:
        fr = frames(name)
        pose_keys, fade_keys, fade_col = [], [], []
        for i, (p, t0, dur, fade) in enumerate(fr):
            t0 = round(t0, 4)
            if i == 0 or fade <= 0:
                pose_keys.append({"time": t0, "name": p})
                continue
            fade_keys += [{"time": t0, "name": p}, {"time": round(t0 + fade, 4), "name": None}]
            fade_col += [{"time": t0, "color": hexa(0)}, {"time": round(t0 + fade, 4), "color": hexa(1)}]
            pose_keys.append({"time": round(t0 + fade, 4), "name": p})
        end = round(sum(f[2] for f in fr), 4)
        slots = {"pose": {"attachment": pose_keys}}
        if fade_keys:
            slots["fade"] = {"attachment": [{"time": 0, "name": None}] + fade_keys, "rgba": fade_col}
        blink = []
        for b in BLINKS.get(name, []):
            blink += [{"time": b, "name": "idle_blink"}, {"time": round(b + BLINK_LEN, 4), "name": None}]
        slots["blink"] = {"attachment": [{"time": 0, "name": None}] + blink}
        tl = {}
        if name in LOOPS:
            for pose, pre in MESH_PREFIX.items():
                if any(f[0] == pose for f in fr):
                    tl.update(bake_motion(pre, end))
        if name in IMPACT:
            starts = {}
            for p, t0, dur, fade in fr:
                starts.setdefault(p, t0)
            keys = [((starts[k[0][1:]] + k[1]), *k[2:]) if isinstance(k[0], str) else k for k in IMPACT[name]]
            if keys[0][0] > 0:
                keys = [(0, 0, 0, 1, 1, 0)] + keys
            tl["body"] = {"translate": [{"time": round(k[0], 4), "x": k[1], "y": k[2]} for k in keys],
                          "scale": [{"time": round(k[0], 4), "x": k[3], "y": k[4]} for k in keys],
                          "rotate": [{"time": round(k[0], 4), "value": k[5]} for k in keys]}
        for prop, bone, extra in ((GUN_PROP, "gun", (("gun", "pistol", 5), ("gun_smear", "gun_smear", 6), ("finger_tip", "finger_tip", None))),
                                  (KNIFE_PROP, "knife", (("knife", "knife", 5), ("knife_smear", "knife_smear", 6)))):
            if name in prop:
                slots_, bone_tl = prop_timelines(fr, prop[name], extra)
                slots.update(slots_)
                tl[bone] = bone_tl
        anims[name] = {"slots": slots, "bones": tl}

    data = {
        "skeleton": {"spine": "4.2.43", "x": -700, "y": -90, "width": 1400, "height": 1450, "fps": 30, "images": "./images/"},
        "bones": bones + [{"name": "gun", "parent": "body"}, {"name": "knife", "parent": "body"}],
        "slots": [{"name": "shadow", "bone": "root", "attachment": "shadow"},
                  {"name": "pose", "bone": "body", "attachment": "idle_a3"},
                  {"name": "fade", "bone": "body"},
                  {"name": "gun_smear", "bone": "gun"},
                  {"name": "gun", "bone": "gun"},
                  {"name": "finger_tip", "bone": "body"},
                  {"name": "knife_smear", "bone": "knife"},
                  {"name": "knife", "bone": "knife"},
                  {"name": "blink", "bone": "i_head"}],
        "skins": [{"name": "default", "attachments": att}],
        "animations": anims,
    }
    return data, tex


def export(data, tex):
    OUT.mkdir(parents=True, exist_ok=True)
    sheet, atlas = pack(tex)
    sheet.save(OUT / "joker.png")
    (OUT / "joker.atlas").write_text(atlas)
    (OUT / "joker.spine-json").write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")))
    print("atlas", sheet.size, len(tex), "regions")


def previews(data, tex):
    out = OUT / "preview"
    out.mkdir(parents=True, exist_ok=True)
    sk = Skeleton(data, tex)
    only = [a for a in sys.argv[1:] if a in ANIMS]
    for name in only or ANIMS:
        end = sum(f[2] for f in frames(name))
        fps = 15 if name in LOOPS else 30          # the soft meshes render slowly; 15 fps shows the motion
        n = max(2, round(end * fps))
        imgs = [sk.render(name, i / fps, canvas=(1100, 1100), origin=(520, 1020), bg=(48, 52, 70, 255))
                .convert("RGB").resize((550, 550), Image.LANCZOS) for i in range(n)]
        imgs = [f.convert("P", palette=Image.ADAPTIVE, colors=200) for f in imgs]
        imgs[0].save(out / f"{name}.gif", save_all=True, append_images=imgs[1:], duration=round(1000 / fps), loop=0,
                     optimize=True)
    print("previews ->", out.relative_to(ROOT))


if __name__ == "__main__":
    d, t = build()
    export(d, t)
    if "--no-preview" not in sys.argv:
        previews(d, t)
