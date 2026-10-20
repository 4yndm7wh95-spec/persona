你在用户的 Windows 电脑上工作，是这个项目的"手"：按这份说明**写代码**（Claude 定方案、审代码，你实现）。
任务：抽牌堆界面的人物立绘换成 P3R 原版营地菜单 COMMU 页 —— 原版 3D 模型 + 原版动作 + 原版塔罗牌道具，在游戏里实时渲染；加上牌浮空自转和光标扫卡时加速转动。版本号 0.14.90。

【0. 规则】
- 只改 / 新增下面【3】列出的代码文件；资源 Claude 已经放好（mod/pck_stage/p3makoto/hero3d/），不要改资源。
- 不要启动游戏（杀戮尖塔 2 和 P3R 都不要），不要用 Computer Use；用户自己实机验收。
- 拿不准的地方按这份文档的"默认值"做，在报告里列出来；遇到文档没覆盖、且会影响设计的情况，停下来报告。

【1. 仓库与 git】
1. 找到上次用的仓库 <repo>（remote 指向 github.com/4yndm7wh95-spec/persona，有 mod\P3Makoto.json）。所有 git 命令都加 -c safe.directory=<repo>。
2. git status -sb：有进行中的合并或已跟踪文件的未提交改动就停下来报告；未跟踪的 art/treasure_hands/、docs/qa/v0.14.69/、mod/_build/、mod_build/、art/p3menu_system/ref/menu_transition_frames_020-107/frames/ 原样保留、忽略。
3. fetch（失败等 5/10/20/40 秒重试，最多 4 次）：
   git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/amazing-darwin-b00arn
4. git merge --ff-only origin/claude/amazing-darwin-b00arn；不能快进时用 -c user.name=Codex -c user.email=codex@local merge --no-edit；冲突就停下来报告。
5. 缺目录时允许 sparse-checkout add mod tools/p3r research/libs_0.107.1（可加 -c core.longpaths=true）。不要 sparse-checkout disable / set。

【2. 已经准备好的东西（先读懂）】
- 资源 mod/pck_stage/p3makoto/hero3d/（tools/p3r/stage_hero3d.py 生成，文件头注释有格式说明）：
  - SK_PC0051_C002.glb（身体 + 衣服 + 道具）、SK_PC0051_F000.glb（脸）、SK_PC0051_H000.glb（头发）、SK_Pp2001_000.glb（塔罗牌）；
  - tex/：T_PC0051_C002_00_Col.png、T_PC0051_F000_00_Col.png、T_PC0051_E000_00_Col.png（眼睛）、T_Pp2001_030_0017.png（牌正面）、T_Pp2001_Backside_00.png（牌背面）；
  - anim/<名>.json + <名>.bin：主角动作 CommuStart / CommuLoop / CommuListStart / CommuListLoop / CommuListCancel；
    bin = float32，帧 × 骨骼 × [px py pz qx qy qz qw]（已是 glTF / Godot 坐标，直接 set_bone_pose_position / rotation）；json 有 bones（骨骼名，和 bin 顺序一致）、frames、fps（= 30）；
  - anim/<名>.scale.bin：float32，帧 × scale_bones × [sx sy sz]（已换好轴），json 里的 scale_bones 是骨骼名。**原版就是靠把召唤器、椅子等道具的骨骼缩到 0.001 来藏的，必须应用，否则会多出一把枪和一张椅子**；
  - anim/Card_<名>.json/.bin：塔罗牌自己的同名五段动作（34 根骨骼），格式同上，没有缩放。
- 参考实现：tools/p3r/hero_render/render.gd（Claude 的离线渲染器，已用它对过原版截图）。它演示了：GLB 加载、按材质名换材质（FLAT / RAMP / OUTLINE 三个 shader 和 FILLS / TEXMAP 表）、骨骼姿势、缩放轨、牌道具。**材质、颜色、描边宽度照抄它**。
- Claude 已验证的事实：
  1. 镜头：Camera3D 放在原点、看 −Z（不旋转），keep_aspect = KEEP_WIDTH，fov = 37.35（原版 UE 的水平视角）。主角和牌的根节点都放在原点、不加任何变换，动作自带位置。
  2. 牌和主角用**同一时间轴**播放同名动作：主角放 CommuListStart 第 n 帧时，牌也放 Card_CommuListStart 第 n 帧。这样牌在 CommuLoop 里正好在手上，CommuListStart（0.5 秒）里松手、翻面、浮到胸前，CommuListLoop 里浮在空中轻轻摆动（原版骨骼 Jnt_C_Center_00 的摆动，±11°）。
  3. 牌模型的面是单面且绕序反了：牌的材质要 cull_disabled（否则松手后看不到牌）。牌材质 = unshaded + 贴图（名字含 Backside 的面用背面贴图，其余用正面贴图），alpha scissor。
  4. 牌的**自转轴**：骨骼 Jnt_C_Center_01 的局部 +Y（牌的长边方向）。这根骨骼原版动作里一直是单位旋转，所以我们的自转直接写在它上面（set_bone_pose_rotation(Center_01, Quaternion(Vector3.Up, 角度))），和原版 Center_00 的翻面 / 摆动叠加，互不干扰。

【3. 要写的代码】
A. 新文件 mod/src/Vfx/HeroStage.cs（命名空间、风格照 PileFx.cs / IdleWarmup.cs）：
  1. 一个缓存的实例（static，第一次用时创建，之后每次开牌堆复用，不要每次重建）。结构：
     SubViewport（OwnWorld3D = true，TransparentBg = true，Msaa3D = 2x，UpdateMode：牌堆开着时 Always，关掉后 Disabled）
       └ Node3D 根：Camera3D（见上）+ 4 个 GLB 生成的场景。
     加载 GLB：FileAccess.GetFileAsBytes("res://p3makoto/hero3d/xxx.glb") → GLTFDocument.AppendFromBuffer(bytes, "res://p3makoto/hero3d/", state) → GenerateScene(state)。
     贴图：FileAccess.GetFileAsBytes + Image.LoadPngFromBuffer + ImageTexture.CreateFromImage（不要用 ResourceLoader，这些 png 没有 .import）。
  2. 材质：照 render.gd 的 make_mat。三个 shader 放进 mod/src/Vfx/Shaders.cs（照现有写法加常量，名字 HeroFlat / HeroRamp / HeroOutline）；去掉 mask_pass 参数。
     CaRtOp（原版"这里显示水"的材质）：画成**色键颜色**（默认 Color(0, 1, 0)，常量 HeroKey）；
     然后显示 SubViewport 的 TextureRect 套一个 CanvasItem shader（Shaders.cs 加 HeroKeyOut）：和色键越接近 alpha 越低（smoothstep，阈值常量化，默认 0.25~0.45 的距离），让牌堆背景的水透出来 —— 原版就是外套、裤子、大部分头发透出后面的水，只有脸、手、衬衫、描边是画出来的。
     找不到对应材质名的面：透明不画（同 render.gd）。描边 OUTLINE 作为 next_pass 挂在 FLAT / RAMP / CaRtOp 上（同 render.gd）。
  3. 骨骼：每个 Skeleton3D 预先把"动作骨骼名 → 骨骼索引"建好表（加载时一次，别每帧 FindBone）。
     播放：时间 t（秒）→ 帧 f = t × fps；在 floor(f) 和下一帧之间插值（位置 Lerp，旋转 Slerp），显示帧率高于 30 时才顺滑。缩放轨同样插值后 SetBonePoseScale。
  4. 状态机（主角和牌同步）：
     - Open()：从 CommuStart 第 0 帧开始 → 放完（约 0.67 秒）接 CommuListStart（0.5 秒，松手）→ 接 CommuListLoop 循环（6.67 秒一圈，循环时首尾帧直接接上）。
       这就是用户要的"点开抽牌堆 → 从拿着牌（图1）过渡到松手、牌浮空（图2）"。CommuLoop 和 CommuListCancel 暂时不用（资源保留）。
     - Close()：停止更新（UpdateMode = Disabled），把 TextureRect 从牌堆界面移走（不要 Free 缓存的 SubViewport）。
  5. 自转（只在松手之后，即进入 CommuListStart 起算）：
     - 基础转速 BaseSpinDegPerSec = 36（10 秒一圈，慢但看得出），从松手开始用 0.6 秒 smoothstep 从 0 升到 36；
     - 额外角度 extra 用**临界阻尼弹簧**追目标 target：每帧 a = ω²(target − extra) − 2ω·v；v += a·dt；extra += v·dt；ω = KickOmega = 9（rad/s）。dt 每帧最多取 1/30（防卡顿时跳变）；
     - Kick()：target += KickDeg（默认 120°，不满一圈）。连续 Kick 只是继续往 target 上加，弹簧速度不归零 —— 光标快速扫过一排牌时会**连续加速、不会停顿**；起步和收尾的缓动由弹簧自然产生；
     - target 和 extra 同时减去 360 的整数倍防止数值变大（两者一起减，不影响运动）；
     - 最终 Center_01 旋转角 = base + extra。松手前调用 Kick() 无效（直接忽略）。
     这三个数做成 public const，便于之后调。
  6. 预热：IdleWarmup.cs 加一个 job "@hero3d"（放在 "@card frame" 之后）：创建缓存实例、加载 4 个 GLB 和贴图、建骨骼索引表、在离屏状态下渲染 2 帧（UpdateMode = Once，两次）让 shader 编译，然后 Disabled。目标：第一次打开抽牌堆不卡。
  7. 出错（任何一个 GLB / 贴图 / 动作文件读不到或解析失败）：GD.PushWarning 一次，返回 false，调用方退回旧立绘。

B. mod/src/Vfx/PileFx.cs：
  1. default 分支（抽牌堆）里，原来 Portrait(right, k, rx) 改成：先试 HeroStage（把 TextureRect 加到 right 里，层级位置和原来 Portrait 一样 —— 蓝蝶前后景顺序不变），失败才调 Portrait。
     TextureRect：尺寸 = view，SubViewport.Size = view 尺寸（宽最多 1920，按比例），StretchMode 拉伸，MouseFilter = Ignore。
     位置：原版里主角在画面正中；牌堆界面卡牌在左侧，所以整块向立绘那一侧平移 HeroShiftX × view.X × L.Side（public const HeroShiftX = 0.22，和原来立绘的位置接近即可，用户验收后 Claude 再调）。
  2. 卡牌悬停：h.Hitbox.MouseEntered 里 `if (hovered != it)` 分支内（就是播放 Sys_Cursor 音效的地方），kind == Kind.Draw 时调 HeroStage.Kick()。
  3. 牌堆关闭的地方调 HeroStage.Close()（找到现有关闭 / 清理 root 的代码，在那里调）。
  4. "perf pile … build" 的分段计时里加一段 lap("hero3d")。
  5. 其他三个牌堆（卡组 / 弃牌 / 消耗）这次不动。

C. 版本号：mod/P3Makoto.json 和 ModEntry.Version 改成 0.14.90。
D. docs/ART_SPEC.md 里 "## 待办（用户实机反馈，先记录）" 之前加一小节（中文，5~10 行）：抽牌堆立绘改为原版 COMMU 3D 实时渲染、牌自转参数、色键透水、v0.14.90 卡框整体提亮（tools/ui/frame_vivid.py，C 方案）。

【4. 自检（必须做，不启动游戏）】
1. dotnet build（照 docs/codex/codex_build_v0.14.89.md 第 3 步的写法，只编 mod 那一个）通过，0 个新警告。
2. 自己通读一遍 diff，对照这份文档逐条打勾，写进报告；特别检查：每帧没有分配大数组 / 没有 FindBone；关掉牌堆后 SubViewport 不再更新；Kick 在松手前无效；dt 上限 1/30。
3. 不需要打包 pck、不需要安装（Claude 审完代码后会另给编译安装文档）。

【5. 提交与推送】
1. 只 add 改动的代码和文档：
   git -c safe.directory=<repo> add mod/src mod/P3Makoto.json docs/ART_SPEC.md
2. 提交：git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "v0.14.90: draw pile portrait = P3R COMMU hero in real-time 3D (original model, anims, tarot card prop) + card spin on hover"
3. push（失败等 5/10/20/40 秒重试，最多 4 次；被拒先 fetch 再 merge --no-edit 再 push，不要 rebase / --force）：
   git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 push origin claude/amazing-darwin-b00arn
4. 用中文简短报告：改了哪些文件、自检勾选结果、按默认值处理的地方、提交号。

【绝对不要做】
- 不要改上面没列出的文件；不要改 / 删资源；不要 rebase / reset / amend；不要 --force；不要改全局 git config。
- 不要启动游戏，不要用 Computer Use。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码。
- 不要修改 P3R 或杀戮尖塔 2 的游戏原文件。
- 不要绕过 imagegen 的安全拒绝或 WindowsApps 的权限保护。
- 不要 sparse-checkout disable / set。
- 不要和其他会话同时操作这个仓库。
- 遇到上面没写到、又会影响设计的情况，停下来报告。
