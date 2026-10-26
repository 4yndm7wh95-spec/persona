# gun_recoil_a6：改用原版关键帧底图（2026-10-26）

用户作废gun_recoil_a5（整个人像木板后倒、胯前顶、看天），明确改为和gun_a2一样的制作方法。本次已fetch+merge，使用research/p5r/gun/pick/key_gun_recoil.png作为第一编辑底图；gun_a2、idle_a3仅定外观/服装/比例/头大小，face_standard定脸眼。此为用户明确授权的原版底图例外，没有拿a4/a5作输入。

提示词要求严格保持底图基本直立、右臂上举、枪朝上、衣摆向后大幅飘起的姿势；头视线朝画面右方、不仰天、不整人后倒；一把枪在右手，左手空着垂大腿旁、食指贴扳机护圈外，无火光/血/目标/特效。完整提示词/输入/原始路径见gun_recoil_a6_generation.json。只生成1次成功，无安全拒绝或双枪，无额外视觉review和审美重画。

## 用户指定脚本原样输出

python tools/p5/pose_consistency.py gun_a2 gun_recoil_a6

```text
gun_recoil_a6      head x1.009  boot height x0.997  boot width x0.931  feet moved [22, 11] px  ok
sheet: art\p5\poses\check_gun_recoil_a6.png | overall: ok
```

脚本ok仅代表它的头部高度阈值通过；靴高/宽和脚位差原样保留，不冒充姿势或画风验收。原版底图取景留白多，提示词允许整个人等比取景填画布，但实际生成文件没有再经脚本缩放。画布1024×1536 RGBA，按既定规则清理透明残点363像素。工具原图保留。临时check图不提交、已删除；另有原版底图/gun_a2外观参考/a6同画布50%对比，供用户查看，未据此进行额外review。

本次原图待用户确认，不覆盖任何已定稿图。归一化/final暂不替换旧版本，等用户确认这张动作后接入，避免把待审图当作已定稿。art/cards的11个修改文件及其他会话未跟踪资料原样保留、不提交、不stash、不还原。未打开结城理图片，未读取obs.json或密码。仅提交本次a6原图、生成记录、对比、状态与报告；推送后核对origin，提交号见Git历史及会话。
