P5Joker 0.9.20 编译安装报告

结果：DLL 编译、PCK 打包、备份和安装完成，安装的三个文件 SHA256 均与源文件一致。

构建提交（git log -1 --oneline）：
bc7992fb 0.9.20：升版本号，Codex 本地编译安装任务单
分支：claude/confident-hypatia-nropgi

执行过程和异常：
最初在指定旧副本完成 fetch，并从远端读取任务单。
游戏进程检查无输出。
首次切换分支失败，退出码 1，完整报错：
fatal: cannot set up tracking information; starting point 'origin/claude/confident-hypatia-nropgi' is not a branch
当时已停止构建并在旧副本写下失败报告。
随后用户明确授权“你自己解决就行”，继续处理 Git 问题并构建。
查明 remote.origin.fetch 只登记两个旧分支。临时补齐跟踪规则后再次切换，退出码 1，完整报错：
fatal: Unable to create 'C:/Users/kkk/sts2_tools/p5_build/repo/.git/index.lock': File exists.

Another git process seems to be running in this repository, or the lock file may be stale

旧副本的状态查询还返回：
fatal: unable to read 8a06efa4e9816dc2f568110b2a84804e380f1879
因此建立独立 worktree，从目标远端提交检出同名目标分支。
实际构建目录为 C:\Users\kkk\sts2_tools\p5_build\repo_0920。
原先失败报告的临时索引 write-tree 命令仍在等待，尚未向远端推送失败报告，也未结束该进程。
旧副本原有删除状态及未跟踪美术文件均未加入本次提交。

版本检查：
P5Joker.json 和 ModEntry.cs 的版本均为 0.9.20。

编译结果：
dotnet build -c Release
Build succeeded.
3 Warning(s)
0 Error(s)
输出 p5mod\bin\Release\net9.0\P5Joker.dll，1297408 字节。

PCK：
新建 C:\Users\kkk\sts2_tools\tmp\p5_stage_0.9.20。
复制 pck_stage 全部内容（含隐藏文件）后按任务单运行 gdre，退出码 0。
输出 C:\Users\kkk\sts2_tools\tmp\P5Joker_0.9.20.pck。
大小 67864132 字节，大于 5 MB。

旧版安装备份：
C:\Users\kkk\sts2_tools\backup\P5Joker_20261113_021537_502

安装目录：
C:\Users\kkk\Desktop\slaythespire\Slay the Spire 2\mods\P5Joker

安装后文件列表（America/Chicago）：
P5Joker.dll   1297408 字节，2026-11-13 02:15:02
P5Joker.json      682 字节，2026-11-13 02:14:40
P5Joker.pck  67864132 字节，2026-11-13 02:15:26

源文件与安装文件 SHA256 一致：
JSON 4D841F7FB182B310CD6FB201676D9F74A406552DC8BD646DA1C7991CF981D7C5
DLL  B33D90DC960AF9594B76E09C25FC4BF63C25A8AAC1170F8F8CC5096070D83EB2
PCK  58913E19733AB2D349D99510D397789BF3AA702A348D3DBACDCEC04C258E8D5A

源码及已有资源未修改，只提交本报告。未生图、未启动游戏、未结束进程、未使用 Computer Use。游戏内验收由用户进行。