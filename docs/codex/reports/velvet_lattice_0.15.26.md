# 0.15.26 天鹅绒房间桌边修订报告

基于 `78cb50c1`，沿桌面银边和桌布实际轮廓缩小保护范围，使用上次生成原图的格栅和地面倒影补齐桌子两侧。把轮廓内像素从底图原样还原，并按原暗色阈值同步重算新区域的透光遮罩。美术目录与 PCK 源目录的背景和遮罩分别为同一份文件。

- 桌子保护轮廓内像素差异为 0，遮罩全白。相对于修订前，背景更新 19,440 个像素，遮罩更新 8,411 个像素。局部对比见 `art/velvet_elevator/fix_table_band.png`。
- 0.107 与 0.111 均编译通过，无警告或错误。按 BUILD.md 从 pck_stage 副本生成 PCK v3，目标 Godot 4.5.1。
- 用 Godot 4.5.1 打开成品 PCK，六张必需商店图层均可解码，音乐及循环点文件均可读取。包内背景和遮罩的 SHA-256 与打包源一致。
- 包中不存在 `elevator_front.png` 和 `elevator_backdrop.png`。
- 确认游戏退出后，备份两套游戏原有的三份模组文件，安装 0.15.26 并比对安装文件哈希。ZIP 位于 `mod/dist/P3Makoto_0.15.26.zip`，桌面 P3RMOD 有副本。未启动游戏。

## 成品 PCK 文件清单

```text
p3makoto/shop/back_focus.png
p3makoto/shop/back_idle.png
p3makoto/shop/blink_closed.png
p3makoto/shop/blink_closed.png.import
p3makoto/shop/blink_half.png
p3makoto/shop/blink_half.png.import
p3makoto/shop/elevator_car.png
p3makoto/shop/elevator_car_glow.png
p3makoto/shop/equip_word.png
p3makoto/shop/lantern_left.png
p3makoto/shop/lantern_right.png
p3makoto/shop/lattice_mask.png
p3makoto/shop/room_lattice.png
p3makoto/shop/velvet_room.png
p3makoto/shop/velvet_room.png.import
p3makoto/bgm/velvet_room.loop.txt
p3makoto/bgm/velvet_room.ogg
```

