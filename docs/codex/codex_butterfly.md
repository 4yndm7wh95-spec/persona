你在用户的 Windows 电脑上工作。任务：用 imagegen 画 P3 的「蓝色蝴蝶」，拆成左翅 / 右翅 / 身体三张透明贴图，给杀戮尖塔 2 mod「P3Makoto」的结局闭眼场景用（只做美术，不改代码）。

【背景】
伟大封印结局里，玩家点「闭上眼睛」后，一只蓝色蝴蝶从屏幕左下飞入、在中间绕一圈、从右上飞走。代码已经写好：三张贴图叠在同一个位置，左右翅各自绕身体中轴做横向缩放来扇动，所以三张图必须严格同一画布、同一坐标系。没有贴图时代码用多边形画，素材到了 Claude 会接进去。
这只蝴蝶就是《女神异闻录 3》里象征菲列蒙 / 心之海的那只蓝蝶：
- 翅膀是半透明发光的钴蓝 / 天蓝色，翅脉和翅缘是更亮的青白色细线，中间往外由深蓝渐变到亮青，像玻璃 / 光做的；
- 前翅尖而长，后翅圆、略小，没有燕尾；整体剪影干净、对称；
- 身体纤细、深靛蓝色，两根细长触角；
- 整体带一点柔和的自发光感，但边缘清楚，不要糊成一团光。

【找到仓库】
在本机找 remote 指向 github.com/4yndm7wh95-spec/persona、且含 mod\P3Makoto.json 的 git 仓库（C:\Users\kkk\ 下、桌面、Documents；可能有多个副本，用你上次成功推送的那个），下文记作 <repo>。所有 git 命令加 -c safe.directory=<repo>。
先 git status：有未提交改动就停下报告。然后 checkout claude/amazing-darwin-b00arn，
git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/amazing-darwin-b00arn（失败等 5/10/20/40 秒重试，最多 4 次），git merge --ff-only origin/claude/amazing-darwin-b00arn（不能快进就 git merge --no-edit origin/claude/amazing-darwin-b00arn，不要 rebase）。
如果仓库是稀疏检出、需要的目录不在磁盘上：允许执行 git -c safe.directory=<repo> sparse-checkout add <需要的目录>（这次要新建 art/v16/butterfly）；路径过长报错时允许单次命令加 -c core.longpaths=true。不要 sparse-checkout disable / set。

【要画的图】输出到 <repo>\art\v16\butterfly\
1. butterfly_full_a.png / _b.png：两个版本的完整蝴蝶，正俯视（从正上方看、翅膀完全展平），左右对称，头朝上。
   - 画布 1024×1024，**真透明背景（蝴蝶轮廓外 alpha=0）**，不要底色、阴影、文字、水印。
   - 身体竖直，中轴严格在 x=512；头部在上（约 y≈330），腹部末端在下（约 y≈690）。
   - 翼展（左翅尖到右翅尖）约 960px，左右各留约 30px 空白；翅膀上下范围大约 y 200~820。
   - 提示词示例：“a single glowing blue butterfly, top-down view with wings fully spread flat, perfectly symmetrical, translucent cobalt and cyan wings with bright pale-cyan veins and edges, dark indigo slender body, thin antennae, soft inner glow, clean silhouette, Persona 3 style, isolated on transparent background, no text”。
   imagegen 不能直接出透明时：让它画在纯黑底上，再自己抠：alpha 取亮度（发光的半透明翅膀就该是半透明的），颜色做反预乘（color / alpha），身体等不透明部分保持 alpha=1。抠完检查边缘没有黑边、没有残留底色。
2. 从你选定的那一版拆出三张，**和完整图同一张 1024×1024 画布、坐标不动**，只是把其他部分清成透明：
   - butterfly_wing_l.png：只有左边两片翅膀（x<512 那一侧的前翅 + 后翅），身体和右翅全透明。翅根贴着中轴，可以伸到 x≈505，不要越过 x=512。
   - butterfly_wing_r.png：只有右边两片翅膀，同理。
   - butterfly_body.png：只有身体（头、胸、腹、两根触角），翅膀全透明。身体两侧 / 翅根和翅膀交界的地方让身体稍微盖住翅根（身体图层画在最上面），这样翅膀扇动缩放时中间不会裂开。
   三张叠回去要和完整图一模一样（翅根交界处不能有缝或白线）。
3. preview_butterfly.png：
   - 左：三张叠在深色背景（#20242c）上；
   - 中：左右翅横向压到 40% 宽度（以 x=512 为中心缩放）再叠身体，模拟扇动到一半；
   - 右：压到 15% 宽度，模拟翅膀合拢；
   - 再在一块浅灰紫背景（#c8c0cc）上缩到 96px 宽放一只，看小尺寸下剪影是否清楚。

【自检】
- 四张 PNG 都是 1024×1024 RGBA，轮廓外 alpha=0；身体中轴在 x=512、头朝上。
- 左翅图 x>512 全透明、右翅图 x<512 全透明；三张叠回去和完整图一致。
- 压缩到 40% / 15% 时中间没有裂缝，身体完整盖住翅根。
- 颜色是发光的蓝 / 青，不是紫色或绿色；96px 时仍然认得出是蝴蝶。
不满足就重画（每版最多 4 轮），仍不满足就如实报告，把最接近的一张也提交（文件名加 _wip）。

【提交与推送】
- 只 git add art\v16\butterfly\ 下的文件。提交：git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "art: P3 blue butterfly sprites (wings / body split) for the close-eyes ending"
- push：git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 push origin claude/amazing-darwin-b00arn（失败等 5/10/20/40 秒重试）。
  被拒绝是因为别的会话先推送了：先 fetch，再 git merge --no-edit origin/claude/amazing-darwin-b00arn（不要 rebase），然后再 push。
  如果单个文件太大导致推送反复中断（网络在 1.7MB 左右断开），改为把这个提交推到新分支：git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 push origin HEAD:codex/butterfly，推成功就停下报告分支名和提交号。
- 完成后用中文报告做了什么、选了哪一版、结果在哪、提交号、每个文件的大小。

【绝对不要做】
- 不要修改或删除代码和已有资源，只新增上面说的文件。
- 不要改写 git 历史（rebase / reset / amend）；不要用 --force；不要改全局 git config。
- 不要启动游戏（杀戮尖塔 2 和 P3R 都不要启动）。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码。
- 不要修改 P3R 或杀戮尖塔 2 的游戏原文件。
- 不要绕过 imagegen 的安全拒绝或 WindowsApps 的权限保护；被拒绝时换一种中性的描述重试一次，仍被拒就停下报告。
- 不要和编译会话同时操作这个仓库。
- 遇到上面没写到的情况，停下来报告，不要自作主张。
