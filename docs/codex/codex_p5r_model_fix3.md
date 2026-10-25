你在用户的 Windows 电脑上工作。任务：接着 research/p5r/model/diag/README_diag.md 那一轮。Claude 已经找到模型外观问题的原因并更新了修复脚本；这一轮重新生成修好的模型、查清动作为什么出框、按贴图名找亚森。不改 mod 代码，不改修复脚本，不生图，不启动游戏。

**先读**：docs/codex/README.md（合并规则）、research/p5r/README.md、research/p5r/model/README_fix.md、research/p5r/model/diag/README_diag.md。
找仓库、拉取（第 1 节）、提交推送（第 7 节）、「绝对不要做」清单都和 docs/codex/codex_p5r_model_fix.md 一样，照那份执行。
art/cards 下另一个会话的已修改文件：原样保留，不提交、不 stash、不还原。

【Claude 的结论（上一轮诊断）】
1. **外观问题已确认是贴图上下颠倒**：GFD Studio 导出的 UV 和导出的 PNG 上下是反的（材质和贴图本身都对）。裤子的 UV 落在贴图右上角的肤色条上、脸下半部落在头发的黑色上，都是这个原因。修复脚本新加了 `--flip-v`，Claude 在云端验证过：怪盗服无面具、学生服都完全正常（棕色靴子、胸袋白巾、格子裤都对），对照图 research\p5r\model\joker\fixed_flipv_preview.jpg。不需要隐藏任何网格。Blur 开头的网格是动态模糊用的替身，渲染时**隐藏**它们（节点名以 Blur 开头的）。
2. 动作出框：要查（第 3 步）。
3. 亚森：按编号猜不靠谱，改成按贴图名搜（第 4 步）。

【1. 重新生成修好的模型】
在 <repo> 下（输出到本机资源库，GLB 不提交）：
- python tools\p5r\fix_gfd_glb.py research\p5r\model\joker\joker_thief.glb <资源库>\model\joker\fixed2\joker_thief_nomask.glb --drop kamen,Blur --flip-v
- python tools\p5r\fix_gfd_glb.py research\p5r\model\joker\joker_thief.glb <资源库>\model\joker\fixed2\joker_thief.glb --drop Blur --flip-v
- python tools\p5r\fix_gfd_glb.py research\p5r\model\joker\joker_student.glb <资源库>\model\joker\fixed2\joker_student.glb --drop Blur --flip-v
用上一轮的检查渲染脚本各渲正面、3/4 一张，拼成 research\p5r\model\joker\fixed2_check.png（缩到每张 512×768）。应该和对照图一致；不一致就停下来报告。

【2. 动作：先取数据】
对 BB0001_051_main_0000（本机动作 GLB）和 joker_thief_nomask.glb（fixed2），出 research\p5r\model\diag\anim_compare.csv，每行一个节点，列：
节点名、父节点名（模型里）、父节点名（动作文件里）、模型静止 translation / rotation / scale / matrix（原样）、动作第一帧 translation / rotation / scale（有通道才填）、动作里这个节点有哪些通道。
至少包括：root、根下面第一层的所有节点、名字里含 hip / waist / spine / neck / head / shoulder / arm / leg / foot（不分大小写）的节点，以及动作里有 translation 通道的**全部**节点。
另外写一行汇总：动作文件里有没有网格、动作 GLB 的场景根下面的层级和模型是不是一样（列出两边根下面前三层的节点名）。

【3. 动作：试三种套法，各渲 6 帧】
和上一轮同样的 6 个时间点，3/4 朝右，取景按**T 姿势全身**固定（不要跟着动作移动），每种一条横排，拼成 research\p5r\model\diag\anim_test2.png，每条标清楚是哪种：
- A：原样套全部通道（同上一轮，作对照）；
- C：只套 rotation 通道，所有 translation 和 scale 通道都不套（骨骼长度用模型自己的）；
- D：只套 rotation 通道，再加上「根下面那个带 translation 通道、名字像 hip / center / 腰 的那一个节点」的 translation 通道。如果第 2 步看出动作和模型的 translation 差一个固定倍数（比如 100 倍、0.01 倍），把这个倍数乘上去，在图上写明倍数；看不出来就原样套。
到这里停，不要批量渲 64 段。

【4. 亚森：按贴图名搜】
1. 不导出模型，只用 BlenderToolsForGFS 的文件解析器读 GMD 里的贴图名：
   - index.csv 里 MODEL/CHARACTER/PERSONA/ 下的**所有** PS*.GMD（按需用 P5RAssetTool 补解，补解的东西放资源库）；
   - 以及 BATTLE/EVENT/BCD/0282/EPL/ARSENE.EPL（亚森觉醒那段战斗事件，里面可能嵌着模型；解析不了就写一句）。
2. 列 research\p5r\model\diag\persona_texture_names.csv：包内路径、编号、贴图名（全部，分号分隔）。
3. 在贴图名里找 arsene / arsenu / arsen / ars_ / 0001 之类字样，找到候选后再用 GFD Studio 导出 GLB、跑一遍 `fix_gfd_glb.py <in> <out> --flip-v`，正面 + 3/4 渲两张，放 research\p5r\model\diag\arsene_found_front.png / arsene_found_threequarter.png。找不到就写清楚搜了多少个文件、用了哪些关键词。

【5. 提交】
只 add research/p5r/model；提交信息 "research: P5R model fix round 3 (flip-v rebuild check, anim retarget tests, Arsene texture search)"。仓库新增不超过 40MB。
README：research\p5r\model\diag\README_round3.md（中文，每步做了什么、命令、结果、没做成的原因）。

【6. 报告】
中文：fixed2 检查图是否和对照图一致；anim_compare 里看出的现象（只描述）；A / C / D 三条各自看起来怎样；亚森找到没有、在哪个文件；提交号。
