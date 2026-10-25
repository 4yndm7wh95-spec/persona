你在用户的 Windows 电脑上工作。任务：把 P5R **总攻击（ALL-OUT ATTACK）** 的原版素材和动画数据找出来、导出，给 Claude 照着复刻进杀戮尖塔 2。只读游戏文件，不改代码，不生图，不启动游戏。
**先读**：docs/codex/README.md、research/p5r/README.md（资源库 C:\Users\kkk\sts2_tools\p5r_rip\、P5RAssetTool 按包内路径补解单个文件的方法）。
找仓库、拉取、提交推送、「绝对不要做」清单都和 docs/codex/codex_p5r_model_fix.md 一样；art/cards 下另一个会话的已修改文件原样保留，不提交、不 stash、不还原。

【背景】
P5R 的总攻击分两段：① 全员冲上去、敌人被一团烟尘 / 剪影乱打（3D 场景里的特效）；② 收尾画面：红底 + 发起人的大立绘斜切滑入 + 大字（THE SHOW'S OVER 之类）+ 红黑碎片，定格后散开。
已知：research/p5r/ui/battle/BASE__BKSK_P_BC0001__*.png 就是 Joker 的总攻击收尾立绘（红底、扯手套那张）。BKSK_P_BC0000~0010 是各角色的同类图。

【1. 找】
1. 在 research/p5r/index.csv 里按路径 / 文件名找所有可能相关的：BKSK、ALLOUT、SOUKOU、TOTAL、RUSH、FINISH、BATTLE/EVENT/BCD/ 下的事件（总攻击很可能是一个 BCD 事件 + EPL 特效）、BATTLE/EFFECT/、BATTLE/GUI/、CUTIN/ 下名字带数字的文件。需要的话用 P5RAssetTool 补解到资源库。
2. EPL / BCD / BF / 表格这类文件里如果能搜到 BKSK、ALLOUT、总攻击相关的贴图名或字符串，记下是哪个文件引用了它们（用文件解析器或直接搜字节里的 ASCII 字符串都行）。
3. 列 research/p5r/allout/candidates.csv：包内路径、类型、大小、为什么认为相关（文件名命中 / 字节里搜到哪个字符串）。

【2. 导出】放 research/p5r/allout/：
- 收尾画面用到的**全部贴图**：BKSK 立绘（全部角色，原尺寸 PNG），收尾的大字、红黑碎片、斜切条带、网点、烟尘等（能找到多少导多少），每张原样 PNG + contact_sheet.png；
- 收尾画面 / 总攻击的**动画数据**：EPL / BCD 里能解析出来的关键帧（时间、位置、旋转、缩放、透明度、播放的贴图 / 模型），原样导成 JSON；看不懂的字段原样保留，并在 README 里写清单位（帧 / 秒）和你确认到哪一步；
- 总攻击的**音效和语音**：research/p5r/audio_catalog.csv 里名字或所在路径像总攻击的 ACB / AWB（BATTLE、SKILLSE、SYSTEM 下），以及雨宫莲语音里总攻击开始和收尾的台词（如果能从 cue 名或时长判断）——转 OGG 放 allout/audio/，附 catalog.csv；
- 如果总攻击收尾有对应的 USM 过场（research/p5r/movie_list.csv 里按大小 / 时长判断），挑最多 3 个转 MP4 放资源库，并做逐 0.1 秒截图拼图放 allout/movie_sheets/。

【3. 找不到的】
动画如果写死在程序里（数据文件里找不到时间轴），就写「找不到数据」，不要编。Claude 会改用参考视频逐帧对照（用户提供录屏）。

【4. 提交】
只 add research/p5r/allout；"research: P5R All-Out Attack assets (finish art, effects, animation data, audio)"。仓库新增不超过 80MB。
README：research/p5r/allout/README.md（中文：找了哪些、导出了什么、动画数据解析到哪一步）。报告：中文 + 提交号。
