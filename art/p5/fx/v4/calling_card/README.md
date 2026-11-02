# 预告函与烧焦纸边（V4）

## 原版搜索与复用

检索 research/p5r/index.csv 的 YOKOKU、CALLING、CARD、予告，以及 CAMP、EVENT、CUTIN、BATTLE/GUI 下的DDS候选；同时检查本机既有资源库。本轮没有重新整包解P5R。

确认可用的原版预告函是 SC.CPK/CAMP/P5_CARD01.DDS 与 P5_CARD02.DDS，均已在本机解出。直接从原DDS导出原尺寸1024×512 RGBA，保留透明通道，不缩放、不改色。

- calling_card_front.png：P5_CARD01，原版礼帽笑脸和 TAKE YOUR HEART。
- calling_card_front_alt.png：P5_CARD02，另一版礼帽面具和 TAKE YOUR HEART。
- 两张都是正面设计变体，不是可确认的正反面。既然已有可用原版高清图，本轮没有重复生成1024×1460正背面，也没有把不明图误标背面；若需匹配其轮廓的无字背面，仍待指定。
- 两张均附同像素的 *_raw.png 与 *_generation.json，记录包内路径、原文件哈希和导出方式。

## 烧焦纸边

card_edge_burn.png：交付512×512 RGBA；原始输出另存 card_edge_burn_raw.png。内置imagegen生成1次，重画0次。只做尺寸归一化及alpha<=2残点清零，没有抠底。

完整提示词：

```text
Use case: stylized-concept. Asset type: transparent 2D game VFX sprite for a Persona 5 inspired calling card burning away. Create a single irregular rectangular loop of charred paper edge with glowing orange-red embers, centered on a 512 by 512 square canvas. The loop surrounds a large entirely transparent empty center (about 65 percent of canvas width and height). Burnt black and deep crimson paper rim, tiny glowing orange ember chips attached to and near the rim, bright warm cores with soft translucent flame edges. Clean hand-painted cel-shaded comic art with controlled ink contours, high contrast, matching Persona 5 official 2D visual language. No intact paper sheet in the center, no card text, no characters, no mask, no scenery, no cast shadow, no opaque backdrop. Genuine transparent PNG RGBA, alpha exactly zero outside the paper/flame/embers and throughout the central hole. Keep all outer embers inside canvas with transparent margin. One asset only.
```

## 检查

- calling_card_front：1024×512 RGBA，alpha范围(0, 255)。原版透明画布、卡片轮廓保留；原版自带的暗边/旧纸细节未擅自删除。
- calling_card_front_alt：1024×512 RGBA，alpha范围(0, 255)。原版透明画布、卡片轮廓保留；原版自带的暗边/旧纸细节未擅自删除。
- card_edge_burn：512×512 RGBA，alpha范围(0, 255)。四角alpha=[0, 0, 0, 0]，中心alpha=0；中心透明，纸边和余烬周围有透明区，保留柔和半透明边缘。烧焦纹理略有颗粒感，交用户验收，未据此重画。

- check_calling_card.png：深灰底检查图。
- check_calling_card_checker.png：棋盘格检查图。
- 原始游戏文件只读，没有改mod代码或启动游戏。
