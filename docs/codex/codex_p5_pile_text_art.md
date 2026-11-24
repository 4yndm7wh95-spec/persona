你在用户的 Windows 电脑上工作。任务：给 P5Joker 的牌堆界面准备 **P5 风格的文字图**（UI 上固定的字全部换成 P5R 那种剪报 / 菜单字，不用普通字体）。**原版有的就用原版，原版没有的再生图。**不改代码，不启动游戏。

**先读 docs/codex/README.md，按里面的长期规则来**（遇到问题不要停下，按自己理解做完；生图真透明底）。

路径：仓库副本 <repo> = C:\Users\kkk\sts2_tools\p5_build\repo，分支 **claude/p5-pile-ui**。拉取、提交、推送的命令和重试方式同 docs/codex/codex_p5_menu3d_render.md 第 1、4 步。
需要的目录：git -c safe.directory=* sparse-checkout add research/p5r art/p5 docs（可加 -c core.longpaths=true）。

## 要的字（每一条一张图）
| 名字（文件名） | 内容 |
|---|---|
| txt_attack / txt_skill / txt_power / txt_status / txt_curse | ATTACK / SKILL / POWER / STATUS / CURSE |
| txt_attack_zh / txt_skill_zh / txt_power_zh / txt_status_zh / txt_curse_zh | 攻击 / 技能 / 能力 / 状态 / 诅咒 |
| txt_draw / txt_exhaust / txt_discard / txt_deck | DRAW / EXHAUST / DISCARD / DECK |
| txt_draw_zh / txt_exhaust_zh / txt_discard_zh / txt_deck_zh | 抽牌堆 / 消耗堆 / 弃牌堆 / 牌组 |
| txt_order_zh / txt_bytype_zh | 原版顺序 / 按类型 |
| txt_order / txt_bytype | ORDER / BY TYPE |
| txt_back_zh / txt_back | 点击任意处返回 / CLICK ANYWHERE TO RETURN |
| txt_upgrade_zh / txt_upgrade | 查看升级 / VIEW UPGRADE |
| txt_upbase_zh / txt_upbase | 查看升级前 / VIEW BASE |
| txt_upgraded_zh / txt_upgraded | 已升级 / UPGRADED |
| txt_inpile_zh / txt_inpile | 牌堆中 / IN PILE |
| txt_cost | COST |
| txt_cards_zh | 张 |
| txt_empty | EMPTY |
| txt_menu_exhaust / txt_menu_discard / txt_menu_deck | 标题牌里接在 MeNu& 后面的词：EXHAUST / DISCARD / DECK（和原版 SKiLL、iTEM、EQUiP 菜单字同一种字形） |

数字 0–9、乘号 ×、SKILL / ITEM / EQUIP 等菜单字、COMMAND、MeNu& 原版已经有了（research/p5r/ui/camp_main/sprites/ 的 00033–00042、00096–00105、00107、00226–00233、00201、00048），**不用做**。卡牌名字是游戏实时给的，也不用做。

## 1. 先找原版（写报告）
在 research/p5r/ui/ 下全部已解出的 PNG（camp_main/sprites 的 961 张 + ui/* 其余 4249 张，P5R 是简体中文版，菜单里有现成的中文字图，例如 camp_main/sprites/00065–00069 的「近战武器」「远程武器」「防具」「饰品」「服装」）里找：
- 整词完全一样的（例如原版就有「技能」「状态」「消耗」「返回」之类）→ 直接用；
- 整词没有、但能用原版的单字拼出来的（同一字形、同一大小的字图逐个拼）→ 拼出来用；
- 都没有 → 记下来，第 2 步生图。
结果拷 / 拼到 <repo>\art\p5\ui\pile_text\，文件名用上表的名字；附 <repo>\art\p5\ui\pile_text\SOURCES.md：每张图来自哪个原版文件（拼的写清楚每个字来自哪里），哪些是生图。原版图保持原尺寸、原样（白字 + 透明底）。

## 2. 原版没有的生图（这部分生成完直接提交推送，不检查、不重画、不写报告）
风格**完全照原版菜单字**：参考 research/p5r/ui/camp_main/sprites/00226.png（SKILL）、00227（ITEM）、00228（EQUIP）、00065–00069（中文装备分类字）、00071–00075（WEAPON / GUN …），以及 00048（MeNu&）、00201（COMMAND）。
- 和原版一样：**纯白字形 + 真透明底**（RGBA，字外 alpha = 0，不要底板、不要投影、不要描边、不要颜色；游戏里代码自己上色 / 加描边）；
- 英文：原版菜单那种大小写混排、每个字母字体不一样、轻微上下错落、个别字母里有星星（像 00048 的 MeNu&、00226 的 SKiLL）；
- 中文：原版 00065–00069 那种粗黑体、略压扁、笔画边缘有一点磨损；长一点的句子（点击任意处返回 / CLICK ANYWHERE TO RETURN）可以朴素一些，但字形风格一致；
- 尺寸：字高约 112 像素（同 00065），四周留约 6 像素透明边，宽度按字数；
- 一张图只放一个词 / 一句话，文件名照上表，放 art\p5\ui\pile_text\。
生成完在 SOURCES.md 里补上「生图」标记，直接提交推送。

## 3. 提交推送
- 只 add art/p5/ui/pile_text；提交信息 "art: P5 pile screen text images (original menu glyphs where available, generated otherwise)"。
- 推送到 claude/p5-pile-ui（命令和重试同上）。

## 4. 报告（只报第 1 步）
中文：哪些词用了原版整词、哪些是原版单字拼的（来源文件）、哪些是生图；提交号。

【绝对不要做】
- 不要修改或删除仓库里已有的代码和资源，只新增 art/p5/ui/pile_text 下的文件。
- 不要 rebase / reset / amend / stash；不要 --force；不要改全局 git config、系统环境变量 / PATH；不要安装或下载东西。
- 不要启动游戏，不要用 Computer Use，不碰游戏目录和 CPK。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，不要读取或输出任何密码。
- 不要进入旧仓库 C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload。
- 不要绕过 imagegen 的安全拒绝。

遇到问题不要停下：没写到的情况、命令出错、结果拿不准，都按你自己的理解选最合理的做法继续，把任务做完，最后在报告里写清楚。
