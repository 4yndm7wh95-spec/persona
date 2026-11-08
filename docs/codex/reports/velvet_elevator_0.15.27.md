# 0.15.27 天鹅绒房间电梯音效报告

## 音效

- 循环环境声已换成 P3R 原版 `amb_int_velvetroom_01_a`。来源为 `Xrd777/CriData/CueSheet/Field/Dungeon_SE/dungeon_se.acb` 的 Cue ID 5，位于 `dungeon_se.awb` 第 35 条音轨。另核对了同库第 36 条、Cue ID 6 的 B 层。两层循环点一致，A 层的循环电平更稳定，因此采用 A 层。
- 原音频为 48 kHz 立体声 HCA，循环区间为样本 113781–1511513（2.370–31.490 秒）。以原循环段制作从零点重复的 OGG，首尾做 20 ms 交叠。成品 29.099 秒、−16.1 LUFS、真峰值 −2.9 dBTP。编码后首尾样本差为 0.00546，低于接缝附近样本差的第 99 百分位 0.01482。
- 未找到可确认的独立轿厢经过声。核对了日语天鹅绒房间 Facility 语音库、`dungeon_se` 的两条环境层、`daily_se` 的 `amb_prop_velvetdoor_st`（房门循环声）、`system` 的 `Wipe_In_VelvetRoom` 和 `Wipe_Out_VelvetRoom`（进出房间转场），以及本机 CriData 路径与音效目录中的 elevator、lift、passby 条目。两条原版房间环境层没有可辨认的独立经过峰值。保留 2.60 秒的 `velvet_elevator_pass.ogg` 占位音与原来的播放时序。

## 构建和打包

基于 `f120ea40`，0.107 和 0.111 均以 0 警告、0 错误编译。按 BUILD.md 从 `mod/pck_stage` 副本重打 PCK v3，并用 Godot 4.5.1 从成品包实际读取四张格栅/吊灯图层和三条 OGG。包内新环境声与打包源 SHA-256 一致。已删除的 `elevator_front.png`、`elevator_backdrop.png`、`elevator_car.png`、`elevator_car_glow.png` 均未入包。

ZIP 位于 `mod/dist/P3Makoto_0.15.27.zip`，桌面 P3RMOD 有副本。打包后首次安装检查发现游戏仍在运行，安装等游戏退出后进行。

## 成品 PCK 文件清单

```text
p3makoto/shop/back_focus.png
p3makoto/shop/back_idle.png
p3makoto/shop/blink_closed.png
p3makoto/shop/blink_closed.png.import
p3makoto/shop/blink_half.png
p3makoto/shop/blink_half.png.import
p3makoto/shop/equip_word.png
p3makoto/shop/lantern_left.png
p3makoto/shop/lantern_right.png
p3makoto/shop/lattice_mask.png
p3makoto/shop/room_lattice.png
p3makoto/shop/velvet_room.png
p3makoto/shop/velvet_room.png.import
p3makoto/bgm/velvet_room.ogg | 168.716567993164 s
p3makoto/bgm/velvet_room.loop.txt
p3makoto/sfx/velvet_elevator_hum.ogg | 29.0994167327881 s
p3makoto/sfx/velvet_elevator_pass.ogg | 2.59999990463257 s
```

