# 天鹅绒房间格栅墙

左右两侧改为深紫蓝的落地格栅墙，使用竖条、菱格、百合圆章和卷草花饰。成图沿用底图的 1672×941 坐标。两盏灯沿用现有独立图层。

| 文件 | 内容 |
|---|---|
| room_lattice.png | 1672×941 不透明 RGB 房间背景 |
| lattice_mask.png | 1672×941 灰度遮罩。白为遮挡，黑为透光 |
| preview_lattice.png | 房间背景叠回两盏原图吊灯 |
| preview_flash.png | 约 450×500 的左侧冷蓝光模拟，灯具在最上层 |
| compare_lattice.png | 原底图、常态预览、同高原版参考三联 |

## 制作与检查

- 使用内置 imagegen 编辑 1 次。输入分别为编辑底图、区域标注和 P3R 格栅墙参考。生成图按原尺寸配准后，只在允许区域内合成，内侧窗柱与地面反光带的接缝向编辑区内收边。
- 编辑区域外像素变化 **0**。遮罩在允许区域外均为白色。
- 从成图取 sRGB 加权亮度 Y′。在实际格栅墙范围内，Y′ ≤ 13.5 为缝隙，Y′ ≥ 20.5 为实心，之间平滑过渡。顶部弧线以上封白，墙底从 y=550 起渐退，并在底座处封白。
- 以遮罩灰度小于 128 计，格栅墙范围内缝隙占 **46.9%**。按灰度透光量折算为 **46.2%**。模拟蓝光只作用于遮罩透光像素，纯白遮挡像素变化 **0**。
- 已目视检查常态、透光预览和遮罩叠图。桌旁贴着编辑区边缘的窄带属于保护区，能看到少量原来的蓝色反光和淡纹。很暗的细铁艺边缘可能仍有少量半透灰像素。

## imagegen 提示词

> Use case precise-object-edit. Make one game environment plate, landscape EXACT composition 1672x941, full scene same framing as input 1. Input 1 BASE is the edit target. Input 2 GUIDE is only an edit mask reference: red zones are the allowed regions, yellow boxes mark lanterns to REMOVE, do not copy any guide coloring, boxes or text. Input 3 P3R screenshot is ONLY the style reference for the original tall metal walls behind Igor. Edit just the left and right wall regions. Preserve the center character, face, chair, table including its edges, main round rose window AND narrow stained glass windows next to it, and foreground carpet exactly at the same image coordinates. Do not rescale/crop/reframe the composition.
> Replace all bright silver gazebo-like arches, waist-high rails, and padded velvet on BOTH sides with floor-to-ceiling cage WALL panels like the P3R reference. These are full uninterrupted walls with tightly spaced straight vertical matte iron bars, repeated three-part panels, upper round fleur-de-lis crests in narrow pointed frame sections, middle fine diamond trellis, lower vegetal scroll ornament. Straight square structural vertical posts, narrower panels at outside edges to show perspective. Frame tops follow the existing receding curved roof contour, bars run to the floor, and thin restrained reflections on the existing floor just under the wall. Near the inner edge of each edit (x565 and x1106), put a continuous narrow square column to visually join the existing slim blue window piers. Keep wall tops, floor line and perspective faithful to base scene. Matte dark navy violet metal #1E2148–#3A3F78 with sparing muted silver purple #8A8FC0 edge highlights, painted dimensional anime background, no thick cartoon outlines or glossy chrome. Darker than the source side walls but keep crisp readable details. Important for extracting a light-transmission mask: empty gaps consistently nearly black #04050F–#0B0E26, little variation, zero bright things behind the grate; the metal contours should be clearly separable from the gaps by luminance, with about 40 to 45 percent visible gaps. No haze or bloom on the side walls. REMOVE the two hanging lanterns and their chains entirely from the scene so complete continuous lattice appears behind their former positions. No industrial beams, pipes, rivets, modern doors, writing, UI, or new objects. Exact full frame scene without margins.

