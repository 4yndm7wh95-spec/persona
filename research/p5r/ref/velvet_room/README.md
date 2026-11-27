# P5R 天鹅绒房间

## 房间结构与过场

场景编号 **F010_003**。本机 `BASE.CPK / EVENT/E100/E120/E127_001.EVT` 与 `EVENT/E400/E450/E451_003.EVT` 的 Field 对象均指向 10/3。圆形大厅围绕中央的蓝金徽章地毯展开，独立牢房和栅栏沿外圈排列。主人公那一侧的牢房通向外环，两位看守站在铁栏外，伊格尔的桌椅在圆形大厅中央。牢门、铁条和链条形成前景，中心地毯及椅背给出视觉焦点。

`E127_001` 的镜头起在牢房侧，初始事件相机 FOV 为 25。约 4.83 秒切换至中心区方向的机位，角色站位键在同一帧把芮丝汀娜和卡萝莉娜放在主人公牢房外两侧。以游戏坐标表示，主人公约在 `(18.6, -10.2, 906.5)`，两位看守约在 `(-84.8, 0, 658.1)` 和 `(87.2, 0, 665.1)`，桌椅道具放在 `(24.5, 0, 91.1)`。约 12.73 秒有高角度、40 FOV 的镜头键。时间以原事件帧除以 30，实机对白等待会改变总时长。第三学期拉雯妲接过双子的看守和设施服务。第三学期常规入场的相机位置、镜头衔接和进出是否复用同一音效仍不确定。[原版牢房事件对白索引](https://mysh.dev/tools/persona/5r/files/dataR.cpk/event/event_data/message/e400)

常规房间有冷蓝色的软包墙和铁灰色的栏杆；中央蓝金纹样及饱和度较高的椅背从暗背景里突出。离线渲染的取样为软包墙 `#1D2A70`、深蓝过渡 `#222C5F`、栏杆阴影 `#273044`、徽章地毯蓝 `#354595`、椅背 `#2F1599`。这些颜色用于估计原纹理关系。实机融合警报另有红色状态。[融合警报原版教程](https://mysh.dev/tools/persona/5r/view?file=%5CdataR.cpk%5Ctutorial%5CscriptTutorial_combine_009.bf)

## 房间画面

[model_views](model_views/) 有四张 1400 × 1000 的同源模型渲染，分别为全局结构、牢门外侧、牢房内看向大厅、中央桌椅位置。使用 `BASE.CPK / MODEL/FIELD_TEX/F010_003_0.GFS`、`MODEL/FIELD_TEX/TEXTURES/TEX010_003_00_00.BIN`、`MODEL/ITEM/IT6046_000.GMD`。从 GFD 导出静态网格，替换内嵌占位贴图，按 `E127_001.EVT` 放置桌椅道具后，用 Blender 离线渲染。俯视能看到顶盖，桌椅镜头中的物件以导出的静态姿态呈现，未应用原版灯光、可见性动画或角色姿态。

未取得用户所需的 8–12 张无损实机过场截图，房间联系表对应上述四张模型视图。

## 人物与 UI

- [portraits](portraits/) 有 40 张原尺寸透明底图及表情编号。伊格尔 `B201`、卡萝莉娜 `B102`、芮丝汀娜 `B101`、拉雯妲 `B100` 均通过本机场景 BMD 的 `bup` 指令核实。CSV 记录每张图的 `BASE.CPK / BUSTUP/*.BIN → *.dds`。`B100_005_00.BIN` 只有独立口型贴图。独立眨眼和口型图层暂未导出。
- 已检查 [仓库已有 UI](../../ui/velvet/contact_sheet.png) 的 27 张图集和 1 张联系表。下表是按原图集可读内容核对的覆盖范围。菜单应用时的实机画面仍缺。
- [ui](ui/) 新补简中地点标题 `SC.CPK / FIELD/PANEL/PLACE_PICT/PLC_010_003_00.DDS`、转场 `BASE.CPK / FIELD/PANEL/WIPE/COMBINE/W_VELVET.SPD` 的 V 字标、虚化和飞溅图层，以及 `FIELD/PANEL/WIPE/WIPEVELVETPACK.PAC → velvet/wipe_door_tex.dds` 的门纹理。转场 PNG 保留原尺寸，SPD 切图坐标见对应 JSON。门包另有 `WIPE_VELVET.GMD`、`WIPE_VELVET_CHAIN.GMD`、`WIPE_VELVET_CHAIN_CAM.GMD` 及 `WIPE_KEY.PLG`。

| 菜单 | 现有图集中的证据 |
| --- | --- |
| 新人格面具／合体 | `p5_velvet_01–03` 主菜单和合成方式，`p5_velvet_06–09` 选材及结果 |
| 强化／特训 | `p5_velvet_05` 强化标题及选择标签，`p5_velvet_11` 特训、提示与背景 |
| 处刑／道具化 | `p5_velvet_02` 道具化入口，`p5_velvet_09` 材料与结果标签 |
| 图鉴及登记 | `p5_velvet_04`、`p5_velvet_13–14` 登记、阅览、提取，`vel_list_01` 与 `vel_list_03` 列表图集 |
| 警报及网络合体 | `p5_velvet_11` 蓝／红背景，`p5_velvet_12` 网络合体，`p5_velvet_15` 特效 |

## 音频和缺项

[audio](audio/) 收录 9 个 OGG，包括门音、牢房铃、菜单确认和取消、熏香、监禁效果，以及双子的牢房开场语音和拉雯妲恢复身份后的语音。逐流来源见 CSV。常规菜单招呼的 `SOUND_E.CPK / SOUND/FACILITY/FACI_C.AWB` 有 64 流，流名多为数字，未确认具体欢迎台词。购买／图鉴提取专用声音未锁定。BGM 曲名为 *Aria of the Soul*（*Hymn of the Soul*），本机通用音乐库 `BASE.CPK / SOUND/BGM.ACB`、`SOUND/BGM.AWB` 的曲目子流未验证，本轮只记录索引。[P5R 乐曲用途](https://megatenwiki.com/wiki/Thieves_Den_%28Persona_5_Royal%29#Music)

索引检索使用 `velvet`、`igor`、`caroline`、`justine`、`lavenza`、`prison`、`combine`、`BUSTUP`、`F010_003`、`FSCR2500`、`FACI_C`、`SYSTEM`、`door_velvet`、`confirm`、`buy`。缺主菜单各子页真实合成画面和第三学期入场截图。房间模型预览、标题图和角色立绘各自保留原始包内定位。
