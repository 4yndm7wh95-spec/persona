# 1e 局部修改：按用户要求保存当前进度

用户选择标准脸候选 2，已保存到 art/p5/ref/face_standard.png。后续每个姿势只做一个版本，已有 B 图保留、不再逐个加工。

当前状态：idle_a 已经用户确认；idle_b 展示后用户允许继续；guard_a 已完成脚本修改并展示，尚待明确确认。summon_a、cast_a 尚未继续脚本处理。未开始 down、victory、小刀、枪、撕面具后续图。

idle_a 可见虹膜右移 1px，idle_b 和 guard_a 各回移 1px，使位置接近中间略偏右；不进行新的整图生图。三张脚本修改的蒙版外差异、alpha 差异和 y≥300 身体区域差异均为 0。记录见 *_script_verification.json，修改前后原尺寸图和蒙版见同名前缀 PNG。远侧眼睛部分遮挡，未强行重画。此前已经做过眼周/下颌贴回，这三次脚本操作的对比基线是对应 *_script_before.png。

head_src、head_edit、head_pasted 和 *_verification.json 是此前头部局部编辑的中间记录；superseded_corner_rule 是已作废的贴右眼角版本，仅供追溯，不是选定标准脸。候选和标准脸选择记录见 art/p5/ref/face_candidates 与 face_standard.md。

尚未执行透明残点清理，待保留姿势确认后处理；当前提交是用户明确要求的进度保存，不表示全部验收通过。没有继续生图，没有打开结城理 mod 的图片，art/cards 修改未纳入提交。后续执行优先脚本平移虹膜；必须生图时使用 head_src + face_standard，按眼周及必要下颌蒙版贴回。
