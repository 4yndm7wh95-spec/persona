# 牌堆文字图来源

本批交付 38 张独立的 RGBA PNG。所有输出为原创排字生成，用本机已有的字形逐字排版并栅格化。英文通过混排、字宽和基线变化做出剪报感，标题比分类标签更错落。中文使用粗黑字形、轻微横向压扁和边缘缺口。输出统一为白色字形，字高 112 px、四周 6 px 透明边。透明度直接从字形蒙版写入。

## 原版素材排查

在本分支的 `research/p5r/ui/` 中扫描了 5,626 张有可见像素的 PNG，用本机简体中文 OCR 筛选，再对候选查看原图。带底板、描边、其他文字或不同字形的候选不符合原样复用要求。OCR 可能漏掉艺术字，因此下表记录的是确认过的候选及取舍。

| 备选内容 | 原版路径（位于 `research/p5r/ui/`） | 取舍 |
| --- | --- | --- |
| SKILL | `camp_main/sprites/00226.png` | 整词匹配，带黑色底板和反白字块。 |
| STATUS | `camp_main/sprites/00231.png` | 整词匹配，带黑色底板和描边。 |
| 技能 | `camp_main/sprites/00287.png`、`velvet/sprites/p5_velvet_list_01.spd_unpacked__8a9e9b91c2/00143.png` | 分别属于双语提示和带底板的「技能说明」。 |
| 状态 | `camp_main/sprites/00500.png`、`camp_main/sprites/00647.png` | 分别属于「进度状态」和「保存目前状态」，两处字形不同。 |
| 能力 | `velvet/sprites/p5_velvet_list_01.spd_unpacked__8a9e9b91c2/00132.png` | 属于带底板的「道具能力」。 |
| 消耗 | `camp_main/sprites/00875.png` | 「消耗品」为黑字白描边，也缺少同字形的「堆」。 |
| 返回 | `network/sprites/audience_PS4.spd_unpacked__a0bac71e19/00025.png`、`camp_main/sprites/00420.png` | 前者为带黑色底板的按钮，后者为长句，均缺少同字形的其余目标字。 |

原版整词采用 0 张，原版单字拼接 0 张。参考了任务列出的字形方向，各张图的字形均独立排版，没有带入原版图像像素。

## 逐文件来源

| 文件 | 文案 | 来源 |
| --- | --- | --- |
| `txt_attack.png` | ATTACK | 原创排字生成 |
| `txt_skill.png` | SKILL | 原创排字生成 |
| `txt_power.png` | POWER | 原创排字生成 |
| `txt_status.png` | STATUS | 原创排字生成 |
| `txt_curse.png` | CURSE | 原创排字生成 |
| `txt_attack_zh.png` | 攻击 | 原创排字生成 |
| `txt_skill_zh.png` | 技能 | 原创排字生成 |
| `txt_power_zh.png` | 能力 | 原创排字生成 |
| `txt_status_zh.png` | 状态 | 原创排字生成 |
| `txt_curse_zh.png` | 诅咒 | 原创排字生成 |
| `txt_draw.png` | DRAW | 原创排字生成 |
| `txt_exhaust.png` | EXHAUST | 原创排字生成 |
| `txt_discard.png` | DISCARD | 原创排字生成 |
| `txt_deck.png` | DECK | 原创排字生成 |
| `txt_draw_zh.png` | 抽牌堆 | 原创排字生成 |
| `txt_exhaust_zh.png` | 消耗堆 | 原创排字生成 |
| `txt_discard_zh.png` | 弃牌堆 | 原创排字生成 |
| `txt_deck_zh.png` | 牌组 | 原创排字生成 |
| `txt_order_zh.png` | 原版顺序 | 原创排字生成 |
| `txt_bytype_zh.png` | 按类型 | 原创排字生成 |
| `txt_order.png` | ORDER | 原创排字生成 |
| `txt_bytype.png` | BY TYPE | 原创排字生成 |
| `txt_back_zh.png` | 点击任意处返回 | 原创排字生成 |
| `txt_back.png` | CLICK ANYWHERE TO RETURN | 原创排字生成 |
| `txt_upgrade_zh.png` | 查看升级 | 原创排字生成 |
| `txt_upgrade.png` | VIEW UPGRADE | 原创排字生成 |
| `txt_upbase_zh.png` | 查看升级前 | 原创排字生成 |
| `txt_upbase.png` | VIEW BASE | 原创排字生成 |
| `txt_upgraded_zh.png` | 已升级 | 原创排字生成 |
| `txt_upgraded.png` | UPGRADED | 原创排字生成 |
| `txt_inpile_zh.png` | 牌堆中 | 原创排字生成 |
| `txt_inpile.png` | IN PILE | 原创排字生成 |
| `txt_cost.png` | COST | 原创排字生成 |
| `txt_cards_zh.png` | 张 | 原创排字生成 |
| `txt_empty.png` | EMPTY | 原创排字生成 |
| `txt_menu_exhaust.png` | EXHAUST | 原创排字生成 |
| `txt_menu_discard.png` | DISCARD | 原创排字生成 |
| `txt_menu_deck.png` | DECK | 原创排字生成 |
