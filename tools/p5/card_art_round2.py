"""P5Joker 卡图第二轮：用户在审批台（https://claude.ai/artifact/Vrxut7p2i4godAwoYjztFG）标的 9 张小改 + 5 张重做 + 新增小刀牌（Shiv）。
生成 docs/codex/codex_p5_card_art_round2.md。用法：python3 tools/p5/card_art_round2.py"""
import glob
from pathlib import Path
import card_art_prompts as base

ROOT = Path(__file__).resolve().parents[2]
COMMON = base.COMMON
FIX_COMMON = ('Use case: precise-object-edit. Image1 is the CURRENT P5 version of this card and the strict EDIT TARGET; Image2 is the original Silent card for reference only. '
              'Change ONLY what this prompt names and keep everything else in Image1 exactly as it is: background, palette, composition, effects and every other element. '
              'Every named character MUST match the attached reference images exactly (face shape, eyes, hairstyle, mask, outfit). '
              'Anatomy must be correct and readable: natural joints, two arms and two legs attached where they should be, hands with five fingers, no extra or floating limbs or heads; if a pose is hard, choose a simpler pose with the same silhouette. '
              'Match the existing hard cel-shading; no blur, no photoreal rendering. Output: opaque, same size as Image1, no text, letters, numbers, frame, UI or watermark.')
FREE_COMMON = ('Use case: new illustration (key card, free design). Image1 is the original Silent card and is shown ONLY for the canvas size, orientation and the bold hard cel-shaded card-art style; do NOT copy its composition or subject. '
               'Design a striking, cinematic Persona 5 Royal illustration that reads instantly at thumbnail size: one strong focal shape, high contrast, a clear silhouette, dramatic lighting, P5 red / black / white palette with the colors this prompt names. '
               'Every named character MUST match the attached reference images exactly (face shape, eyes, hairstyle, mask, outfit, design details); do not invent features. '
               'Anatomy must be correct; no extra limbs. Hard cel-shading: crisp dark outlines, deep near-black shadows, clear lit planes; no blur, no photoreal rendering. '
               'Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, the same size as Image1, no text, letters, numbers, frame, UI or watermark.')

D = base.DESIGNS
# (卡, 中文, 种类 fix/redo/free/new, 用户的意见, 这次怎么改, 角色)
ROWS = [
    ('bullet_time', '子弹时间', 'fix', '脸不对', 'Redraw only JOKER\'s face and hair so they match the JOKER references exactly (white domino mask with black edging, sharp grey eyes behind it, messy black curly hair, calm confident expression). Keep his pose, raised red glove, coat, pistol and all bullets unchanged.', ['JOKER']),
    ('dodge_and_roll', '闪躲翻滚', 'fix', '人体结构、姿势怪异', 'Replace the tangled figure with JOKER doing one clean, readable forward roll in the same place and size: body curled into a ball, head tucked, back rounded toward the viewer, one red-gloved hand touching the ground, knees drawn in, black coat tails wrapping around him following the orange swoosh. Two arms, two legs, correct anatomy. Keep the background, lightning and swoosh.', ['JOKER']),
    ('hand_trick', '手上技法', 'fix', '后面那只缠绷带的灰色手没改', 'The grey bandaged gauntlet hand at the upper left still belongs to the Silent: replace it with JOKER\'s second hand in a red glove and black coat sleeve, open and relaxed, as if about to catch the calling card. Keep the front hand, the calling card, the sparkle and the background.', ['JOKER']),
    ('neutralize', '中和', 'fix', '和原版完全一样，要有一点设计和创意', 'Keep the golden scales, their size, position and the purple background, but make them a palace Treasure with a story: the left pan holds a red-and-black calling card, the right pan holds a cracked white SHADOW mask, and the beam tips toward the card (the calling card outweighs the shadow). Add a faint red glint on the card. No characters.', []),
    ('nightmare', '夜魇', 'fix', '头浮空了', 'Remove the two floating heads of CAROLINE and JUSTINE completely. Keep only the two pairs of black-gloved hands in blue Velvet Room warden cuffs, and make the arms enter cleanly from the top-left and right frame edges (sleeves continue past the edge), clutching the green nightmare blob. Fill where the heads were with the original dark red background shapes.', ['CAROLINE_JUSTINE']),
    ('predator', '猎杀者', 'fix', '持枪那只手姿势怪异', 'Remove the raised pistol and the arm holding it. JOKER\'s other arm hangs naturally at his side or is hidden behind his coat; keep the red-gloved hand gripping the glowing knife in front, his face, coat, the floating shards and the background unchanged.', ['JOKER']),
    ('prepared', '早有准备', 'fix', '人体结构不合理、怪异', 'Simplify to a clean close-up of JOKER\'s torso and hands: his left red-gloved hand pulls his black coat open, his right red-gloved hand grips the knife handle at his belt ready to draw. No pistol, no face (crop at the chin), no shadow monster. Two hands only, correct anatomy. Keep the magenta background.', ['JOKER']),
    ('reflex', '本能反应', 'fix', '人体结构怪异、不合理', 'Redraw JOKER in one clear pose: body turned three-quarters toward the SHADOW at left, right arm extended pointing his pistol at it, left arm drawn back holding his knife low, coat flaring. Correct anatomy, both arms clearly attached. Keep the SHADOW, the cyan lightning between them and the background.', ['JOKER']),
    ('speedster', '速行者', 'fix', '姿势不对', 'Redraw JOKER in the original\'s dynamic pose: leaping down off the rock ledge toward the viewer, front leg stretched forward with the boot sole leading, back leg bent, body leaning forward, coat tails streaming behind him, one red glove reaching forward. No pistol. Correct anatomy. Keep the debris, the rock and the yellow-green speed background.', ['JOKER']),
    ('cloak_and_dagger', '斗篷与匕首', 'fix', '猎人的绿斗篷没有改', 'The big green cloak filling the background is still the Silent\'s: turn it into JOKER\'s black tailcoat seen up close (black fabric folds, a glimpse of the dark red lining, a silver button), same shapes and lighting. Keep the red-gloved hand and the knife.', ['JOKER']),
    ('corrosive_wave', '腐蚀波', 'redo', '只是把原版换了个颜色，没有设计上的重做', 'Redesign the scene (you may change the composition): a Mementos subway tunnel seen in perspective, a huge black-and-crimson cognitive wave crashing through it toward the viewer, its crest breaking into dripping red curse; SHADOWS are swept up inside the wave, and a dark subway train is being swallowed at the far end. Keep the diagonal energy of the original wave. Red, black and a sickly green glow on the wave edge.', []),
    ('grand_finale', '华丽收场', 'free', '主角保持撒旦耶尔，不要谢幕的姿势，可以完全丢掉原版设计，做得帅一点（关键牌）', 'SATANAEL, colossal, rising behind a night-time Tokyo skyline under a blood-red sky, its halo of blades blazing, aiming its gigantic revolver straight down toward the viewer; the muzzle is the focal point, charged with a swirling blue-white flame bullet about to fire. Tiny in the foreground on a rooftop edge, JOKER stands with his back to us, one red-gloved arm raised pointing at the target, coat whipping in the wind. Strong low-angle perspective.', ['SATANAEL', 'JOKER']),
    ('outbreak', '毒性爆发', 'free', '关键牌，可以做一张和原版无关、帅一点的卡图', 'Close-up of JOKER ripping off his white domino mask with one red-gloved hand, blood-red and black curse flame exploding out of the mask; from that flame ARSENE erupts behind him, wings flung wide, chains snapping, red-flame eyes, filling the upper frame. Toxic magenta-purple poison flames spread outward from the burst toward the edges. Dramatic low angle, diagonal composition.', ['JOKER', 'ARSENE']),
    ('wraith_form', '幽魂形态', 'redo', '（重做，没写原因：上一版是红身体加绿翅膀的亚森，不像也不好看）', 'Keep the vertical canvas, the green background and the dark root-like tendrils. In the center, JOKER floats, turning into a wraith: his body becomes translucent green-black smoke from the waist down, the smoke trailing into the tendrils; only his white mask and red gloves stay solid and bright; behind him a faint, huge silhouette of ARSENE\'s wings and top hat made of the same smoke. Calm, eerie, untouchable.', ['JOKER', 'ARSENE']),
    ('shiv', '小刀（衍生牌）', 'new', '小刀牌没做，最好做成子弹或者枪的样子', 'This mod turns Shivs into Joker\'s gunshots. The original knife becomes a single gleaming brass pistol bullet flying in the same direction and at the same size and angle as the knife, with a hot red tracer streak and a small muzzle-flash burst at the trailing end; the tip glints. Keep the background, palette and any sparkle or motion effects. No characters.', []),
]


def main():
    out = ['# P5Joker 卡图第二轮（9 张小改 + 5 张重做 + 小刀牌）\n',
           '来源：用户在审批台 https://claude.ai/artifact/Vrxut7p2i4godAwoYjztFG 的判定（其余 77 张通过）。由 `tools/p5/card_art_round2.py` 生成。\n',
           '## 给 Codex 的执行方式\n',
           '- 一批全做完（15 张），生成后停下等用户审，不自动重试、不自己判断通过。',
           '- **小改**（fix）：Image1 = 第一轮的图 `art/p5/cards/round1/batch_NN/<卡名>.png`（在它上面局部改），Image2 = 原版；附该角色参考图。',
           '- **重做 / 自由设计**（redo / free）：Image1 = 原版（重做时可改构图；自由设计只取尺寸和画风），附参考图。',
           '- **小刀牌**（new）：先从游戏 PCK 导出原版 `res://images/packed/card_portraits/*/shiv.png`（猜是 token 卡池，找不到就在 card_portraits 下全局找 shiv），放 `research/silent_assets/card_portraits/shiv.png`，再当 Image1。',
           '- 输出：`art/p5/cards/round2/<卡名>.png`（和原版同尺寸、不透明），另存 `comparison.png`（原版 | 第一轮 | 第二轮）和 `prompts_and_sources.json`，状态 `awaiting_user_review`。',
           '- 只 add `art/p5/cards/round2` 和 `research/silent_assets/card_portraits/shiv.png`，提交推送到 `claude/confirm-next-tasks-k5wu6t`；被拒先 fetch 再 merge --no-edit（不 rebase）再 push。',
           '- 规矩同其他任务单：不读取或输出 C:\\Users\\kkk\\.p3record\\obs.json 和任何密码 / 密钥；不 rebase / reset / amend / stash / --force；不改全局 git config、环境变量；不安装任何东西；不碰游戏目录（只读导出）。\n']
    kinds = {'fix': '小改', 'redo': '重做', 'free': '自由设计（关键牌）', 'new': '新增'}
    for i, (key, zh, kind, note, edit, chars) in enumerate(ROWS):
        designs = ' '.join(f'[{c}] = {D[c]}.' for c in chars)
        head = FIX_COMMON if kind == 'fix' else FREE_COMMON if kind == 'free' else COMMON
        prompt = head + ' ' + edit + (' Character designs (follow the attached references): ' + designs if designs else '')
        refs = []
        for c in chars:
            for f in base.REF_DIRS.get(c, [c.lower()]):
                if f not in refs: refs.append(f)
        r1 = glob.glob(str(ROOT / f'art/p5/cards/round1/batch_*/{key}.png'))
        orig = f'research/silent_assets/card_portraits/{key}.png'
        out.append(f'### {i + 1}. `{key}` {zh} · {kinds[kind]}\n')
        out.append(f'- 用户意见：{note}')
        if kind == 'fix':
            out.append(f'- Image1（第一轮）：`{Path(r1[0]).relative_to(ROOT)}`；Image2（原版）：`{orig}`')
        else:
            out.append(f'- Image1（原版）：`{orig}`')
        out.append('- 参考图：' + ('、'.join(f'`art/p5/cards/refs/{r}/`' for r in refs) if refs else '无（没有人物）'))
        out.append(f'\n```\n{prompt}\n```\n')
    (ROOT / 'docs/codex/codex_p5_card_art_round2.md').write_text('\n'.join(out), encoding='utf-8')
    print(len(ROWS), 'prompts')


if __name__ == '__main__':
    main()
