你在用户的 Windows 电脑上工作。任务：从杀戮尖塔 2 的游戏 PCK 里**只读地**解出原版的「转场」资源（选人界面点「开始」后那一下过场等），
放进仓库 research/sts2_transitions/，给 Claude 重做 P5 Joker 的开场过场时参照原版的做法和精细程度。不改 mod 代码，不生图，不启动游戏，不编译。

【0. 绝对不要做】
- 不修改仓库里任何已有文件（只新建 research/sts2_transitions/ 下的文件）。
- 不 rebase / reset / amend / stash；不 --force；不改全局 git config、系统环境变量 / PATH；不安装任何东西。
- 不读取或输出 C:\Users\kkk\.p3record\obs.json，不读取或输出任何密码 / 密钥。
- 每条命令单独执行；不要用 Remove-Item -Recurse / -Force，不删除任何目录或文件。
- 游戏目录只读：不改、不删、不往里写任何东西。不启动游戏，不关闭 / 结束任何进程。
- 遇到下面没写到的情况、任何一步报错：停下来，把情况和完整报错写进报告，推送报告，结束。

路径：
- 仓库副本 <repo> = C:\Users\kkk\sts2_tools\p5_build\repo_0920（分支 claude/confident-hypatia-nropgi 检出在这里，不要进 repo）
- gdre = C:\Users\kkk\sts2_tools\gdre\gdre_tools.exe（和解 research/sts2_vfx 时同一个，2.7.3）
- <PCK> = C:\Users\kkk\Desktop\slaythespire\Slay the Spire 2\SlayTheSpire2.pck
- 临时目录：C:\Users\kkk\sts2_tools\tmp\sts2_transitions_<当前日期时间>\（新建）

【1. 拉最新】
在 <repo> 里依次单独运行：
   git -c safe.directory=* branch --show-current   → 必须是 claude/confident-hypatia-nropgi，不是就停下来报告
   git -c safe.directory=* status --short --untracked-files=no   → 有输出就停下来报告
   git -c safe.directory=* -c http.version=HTTP/1.1 fetch origin claude/confident-hypatia-nropgi:refs/remotes/origin/claude/confident-hypatia-nropgi
   git -c safe.directory=* -c http.version=HTTP/1.1 merge --ff-only origin/claude/confident-hypatia-nropgi
网络错误（RPC failed / Connection was reset / early EOF / could not fetch ... from promisor remote）按 10 / 20 / 40 / 60 秒等待后重试同一条命令，最多 4 次；别的错误直接停下来报告。

【2. 要解的范围】
1. 先 `gdre --headless --list-files=<PCK>` 列目录（输出存到临时目录），从里面挑出路径里含 `transition`（不分大小写）的全部文件，
   至少包括：res://images/ui/transitions/ 下全部、res://materials/transitions/ 下全部、NTransition 所在的场景（.tscn）和它用到的着色器 / 材质 / 贴图。
2. 依赖补全：这些 .tscn / .tres 里 ext_resource 引用到的资源递归补全。脚本（.cs / .gd）不解，只在清单里记路径。
3. 用 `--extract=<PCK> --output=<临时raw> --include=res://...`（多个精确 include，分批）解出原始文件，再用 `--recover=<临时raw> --output=<临时recovered> --skip-loading-resource-strings` 还原。

【3. 输出】放 <repo>\research\sts2_transitions\
- recovered/：还原后的 .tscn / .tres 文本、.gdshader 源码、贴图 PNG（保留原色模式和 alpha，不缩放、不改色），路径照 res:// 原样。
- raw/：原始文件（含 .godot/imported 下对应的数据）；单文件超过 20MB 的留本机，在 README 里写本机路径。
- LIST.csv：文件名、原路径、类型、大小、说明。
- steps_<角色>.png：每个角色的 *_transition.png 各做一张「阈值分步」联系表：按原版材质的算法
  （falloff = 1 - 贴图.r；remap = mix(-0.1, 1.1, threshold)；盖住 = step(falloff, remap)），threshold 取 0.1、0.2 … 1.0 十步，
  盖住的地方画黑、没盖住的画浅灰，十格横排，每格宽 384，格下写 threshold 值。用 Python + Pillow 做就行（本机已有的 Python，不要装新东西；没有 Python 就跳过这一项并在报告里说明）。
- README.md（中文）：用的命令、解出的文件和体积、每个 *_transition_mat.tres 引用的是哪张贴图、失败项。

【4. 提交与推送】
只 add research/sts2_transitions；提交信息 "research: Slay the Spire 2 vanilla screen transitions (read-only extract)"。
   git -c safe.directory=* add research/sts2_transitions
   git -c safe.directory=* -c user.name=Codex -c user.email=codex@local commit -m "research: Slay the Spire 2 vanilla screen transitions (read-only extract)"
   git -c safe.directory=* -c http.version=HTTP/1.1 push origin claude/confident-hypatia-nropgi
推送被拒：先 git -c safe.directory=* -c http.version=HTTP/1.1 pull --no-rebase --no-edit origin claude/confident-hypatia-nropgi，再推；有冲突就停下来报告。

【5. 报告】
在对话里中文回复：解出的文件数和体积、各角色 transition 贴图的尺寸、提交号。
