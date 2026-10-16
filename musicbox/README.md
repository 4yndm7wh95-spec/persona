# P3 音乐盒（P3MusicBox）

P3Makoto 的扩展 mod，也可以单独使用。纯界面 + 本地播放，不影响玩法（`affects_gameplay: false`），联机时只装在自己这边也可以。

## 用法
- 右上角设置（齿轮）按钮正下方有一个小圆按钮（深蓝底、青色描边、♪）。主菜单没有顶栏时放在屏幕右上角。
- **默认不播放。**
- **点击按钮**：打开音乐盒面板（再点按钮或点面板外关闭）。播放时外圈虚线慢转、偶尔飘出小音符；鼠标悬停时左侧显示当前歌名。
- 面板里（1.1.0 起不再用右键）：
  - 「◀ 上一首 / ▶ 播放 · ‖ 暂停 / 下一首 ▶」；
  - 点歌名切歌（当前曲目白底深蓝字，P3R 菜单风格）；
  - 「顺序播放 / 单曲循环 / 随机播放」切换；
  - 「歌词：开 / 关」；
  - 音量滑条。
- 播放时原版背景音乐**淡出到静音**，暂停后**淡回**原来的音量；播放、暂停、切歌都有淡入淡出。
- 有同名 `.lrc` 时，按钮下方右对齐显示当前一句歌词（原文 + 翻译时显示两行），换句时交叉淡入淡出。
- 暂停菜单、设置、牌组等全屏界面打开时按钮自动藏起来。
- 游戏设置里开了「后台静音」时，切出游戏窗口音乐盒也会淡出。
- 音量、播放模式、歌词开关、上次播放的歌保存在 `%APPDATA%\SlayTheSpire2\p3musicbox.json`。

## 放歌
```
<游戏目录>\mods\P3MusicBox\
    P3MusicBox.dll
    P3MusicBox.json
    music\
        playlist.txt       （可选，内容是 JSON：顺序、标题、歌手；没有就按文件名排序、文件名当标题）
        01_xxx.ogg / .mp3
        01_xxx.lrc         （可选，和歌曲同名，UTF-8）
```
`playlist.txt` 格式（JSON）：`[{"file": "01_xxx.ogg", "lrc": "01_xxx.lrc", "title": "歌名", "artist": "歌手"}, …]`

1.1.0 起歌单文件叫 `playlist.txt`：游戏会把 mod 文件夹里所有 `.json` 都当成 mod 清单去读，`playlist.json` 每次启动都会报一条红字错误（不影响使用）。旧的 `playlist.json` 仍然认，改个名就不报错了。

发布时把 `music\` 文件夹一起打包即可（歌曲不进 git 仓库，只是因为体积大）。

## 编译
`dotnet build -c Release -p:GameDir="<游戏安装目录>"`（musicbox 目录），把 `bin\Release\net9.0\P3MusicBox.dll` 和 `P3MusicBox.json` 放进 `<游戏目录>\mods\P3MusicBox\`。
