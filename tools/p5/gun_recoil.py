"""Make the P5 Joker gun recoil frame from the approved aim frame, without image generation.

Image generation kept failing on this frame (blocked as violence once, then drew a second gun), so
the recoil is made from gun_a2 directly: the hand + gun snap up at the wrist and the whole arm
lifts at the shoulder (the "kick"); body, head, legs and the other hand are untouched pixel for pixel.
In game the frame is only on screen ~0.08 s under the muzzle flash.
Usage: python tools/p5/gun_recoil.py art/p5/poses/gun_a2.png art/p5/poses/gun_recoil_a3.png [arm_deg=14] [wrist_deg=18]
Geometry is measured on gun_a2.png (1024x1536).
"""
import sys
from PIL import Image, ImageChops, ImageDraw, ImageFilter

ARM = [(548, 245), (900, 205), (1022, 60), (1022, 345), (905, 345), (835, 442), (715, 447), (620, 376), (548, 372)]
SHOULDER = (520, 295)
HAND = [(872, 150), (1022, 140), (1022, 345), (872, 345)]
WRIST = (880, 300)


def cut(im, poly):
    mask = Image.new('L', im.size, 0)
    ImageDraw.Draw(mask).polygon(poly, fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(1))
    piece = im.copy()
    piece.putalpha(ImageChops.multiply(im.getchannel('A'), mask))
    rest = im.copy()
    rest.putalpha(ImageChops.multiply(im.getchannel('A'), ImageChops.invert(mask)))
    return piece, rest


def swing(piece, deg, center, joint_r):
    """Rotate piece by deg; fill the wedge the rotation opens at the joint by sweeping the part of the
    piece within joint_r of the pivot through the intermediate angles (underneath the final piece)."""
    near = Image.new('L', piece.size, 0)
    cx, cy = center
    ImageDraw.Draw(near).ellipse([cx - joint_r, cy - joint_r, cx + joint_r, cy + joint_r], fill=255)
    joint = piece.copy()
    joint.putalpha(ImageChops.multiply(piece.getchannel('A'), near))
    out = Image.new('RGBA', piece.size, (0, 0, 0, 0))
    steps = max(2, int(abs(deg) / 1.5))
    for i in range(steps + 1):
        out.alpha_composite(joint.rotate(deg * i / steps, resample=Image.BICUBIC, center=center))
    out.alpha_composite(piece.rotate(deg, resample=Image.BICUBIC, center=center))
    return out


def main():
    src, out = sys.argv[1], sys.argv[2]
    arm_deg = float(sys.argv[3]) if len(sys.argv) > 3 else 14
    wrist_deg = float(sys.argv[4]) if len(sys.argv) > 4 else 18
    im = Image.open(src).convert('RGBA')
    # positive angle = counter-clockwise on screen = up. 1) wrist snaps up, 2) whole arm lifts at the shoulder
    hand, rest = cut(im, HAND)
    rest.alpha_composite(swing(hand, wrist_deg, WRIST, 60))
    arm, body = cut(rest, ARM)
    body.alpha_composite(swing(arm, arm_deg, SHOULDER, 110))
    body.save(out)


if __name__ == '__main__':
    main()
