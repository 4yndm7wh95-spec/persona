# P3 蓝蝶透明分件

日期：2026-10-15。只新增美术及说明，没有修改代码、既有资源或游戏文件，没有启动游戏。

## 选定版本和交付状态

选用 **B版第三轮** 拆分。B版较简洁、玻璃感更强，钴蓝/天蓝/青白色脉络，前翅尖长、后翅圆、没有燕尾；头朝上、身体深靛蓝，保留两根细触角。

两版均各生成三轮，均使用内置imagegen，无安全拒绝。由于布局仍有偏差，完整图使用 **butterfly_full_a_wip.png / butterfly_full_b_wip.png**，不声称严格满足全部绘画坐标要求。B版头部中心约(512,390)，低于目标约(512,330)约60像素；腹部末端约(512,672)，接近目标约690。B版翼展横向包围框为x32～991（右边界不包含992），宽960像素；翅膀大约y226～817。A版上翅尖偏高，整体y145～841，故不推荐它作为动画分件来源。

## 文件

- butterfly_full_a_wip.png：A版完整透明图，仅作比较。
- butterfly_full_b_wip.png：B版完整透明图，拆分验证的基准。
- butterfly_wing_l.png：B版左侧前翅+后翅，严格x<512；身体和右翅清为透明。
- butterfly_wing_r.png：B版右侧前翅+后翅，严格x≥512；身体和左翅清为透明。
- butterfly_body.png：头、胸、腹及两根触角，身体层在最上。身体/翅根边界保留少量接缝覆盖范围，防止横向压缩后露缝。
- preview_butterfly.png：深底100%、40%、15%翼宽的三种叠层状态，以及浅灰紫底96px小图。
- README.md：本说明、提示词、自检和文件大小。

完整图两张和分件三张都是 **1024×1024 RGBA**；预览为3072×1240 RGB。三张分件没有各自裁框、移动、居中或单独缩放，坐标完全一致。中轴/缩放固定点为画布边界坐标x=512。绘制顺序：左翅→右翅→身体。

## 导出和透明处理

imagegen实际输出为1254×1254，未直接满足所需像素大小。作为透明贴图导出处理，先将完整图统一采样到1024方形，再按约960像素翼展整体等比适配；所有零件都从这个最终完整图拆出，绝未分别改变位置。

复用生成图原有alpha，移除alpha<8及与主体不连通的零散背景像素；透明区域RGBA全部归零。不是把黑色预览底烘焙到PNG里。为形成发光玻璃翅膀，使用黑底预乘RGB的**最大通道亮度（HSV明度）**作为翅膀alpha，再做颜色反预乘；采用最大通道而非加权灰度，是为了避免饱和钴蓝被低灰度alpha截断变暗。保留身体原色，身体主体原alpha≥240的像素提升为255，边缘保留抗锯齿。所有这类处理都发生在拆分前，完整图和三层以同一结果作为基准。

未对某一边做镜像重绘，双侧由imagegen绘制，视觉对称但不是逐像素镜像。未用代码代替imagegen画翅膀；后处理仅做透明度、画布导出、分区和预览。

## 自检

|项目|结果|
|---|---|
|完整图/三分件尺寸、RGBA|通过：五张均1024×1024 RGBA|
|轮廓外透明|处理后的主体matte外alpha=0，alpha=0处RGB也全部为0|
|左右翅跨轴|左翅x≥512非零alpha像素0个；右翅x<512为0个|
|三层叠回完整B图|通过：所有RGBA通道最大差值0，不同像素0|
|压到40%、15%后翅根|已在深底检查，没有看到裂缝/浮动翅片；身体始终保持原位置和尺寸，盖住翅根|
|96px剪影|仍可辨认蝴蝶，蓝青色翅膀和细长身体可见，细脉络自然缩为亮线|
|颜色|以钴蓝/天蓝/青白为主，没有改成紫色或绿色|
|头/腹目标坐标|未完全达到，B头约y390而非约330，腹部约672；完整图保留_wip|
|双侧对称|视觉轮廓近似对称；不是逐像素完全镜像|

B版全图alpha包围框：(32,226,992,818)。
左翅：(32,226,511,818)；右翅：(514,226,992,818)。
身体含触角：(402,229,621,672)。
包围框的右/下界不包含。身体横截面中点在x512附近（个别行约0.5像素绘画不对称）；动画的固定缩放轴严格是x512，没有针对左右翅分别估计中心。

本次透明与叠回检查基于**最终导出的B版完整图**，不声称与1254像素原始生成图逐字节一致。没有运行游戏或验证代码接入。

## 最终采用提示词

### A版第三轮

```text
A perfectly symmetric flat top-down glowing blue butterfly sprite for Persona3 inspired game art, transparent square1024x1024 canvas. Slender dark indigo body starts with a SMALL HEAD at exactly32% image height, torso immediately below it at40%, narrow abdomen ends at67% height. Exactly centered on x512. Delicate antennae curve from head up to18%height. Do NOT place the head down at40%height. Four translucent glasslike wings, cobalt blue near body, luminous sky blue cyan toward outer edges, fine cyan-white veins and outline. Upper pair long sharply pointed wings extend laterally to x3% and97%, at y20%. Rounded smaller lower pair stop at y80%. No tail extensions. Body and wings proportioned as a graceful stylized2D butterfly, no giant bulging eyes or hairy realistic insect texture. Clean silhouette and internal luminosity, no external blur or halo, no shadow, no text, no extra objects. Transparent background alpha zero outside butterfly. All body parts and wings lie in one flat plane.
```

### B版第三轮（编辑B版第二轮）

```text
Refine this blue butterfly sprite, keep same wings and blue/cyan transparent glass style. SHORTEN and RAISE the body: head centered at32% of canvas height (currently about38%), abdomen ends at67% (currently70%). The thorax may be between head and wing roots, natural narrow body. Keep wingtop at20% and wingbottom80%, widest tips x3% and97%. Make body a thin straight DARK INDIGO vertical line centered at50% width, no oversized head, antennae thin and delicate. Perfectly symmetric straight-overhead fully spread pose, no perspective.1024x1024 transparent RGBA, no exterior glow haze, shadow, writing or background. No tail extensions, round hindwings. Keep finely defined pale cyan veins.
```



## 文件大小

|文件|字节|
|---|---:|
|butterfly_body.png|38565|
|butterfly_full_a_wip.png|831125|
|butterfly_full_b_wip.png|683209|
|butterfly_wing_l.png|345363|
|butterfly_wing_r.png|341892|
|preview_butterfly.png|967238|
