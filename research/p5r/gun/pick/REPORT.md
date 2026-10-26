# 用户选定50%开枪参考（2026-10-26）

用户从angle_options中选定50%。相机真实方位角25.670095872954956°（atan2(worldY,worldX)，统一水平镜像前），方向约(0.901303,0.433189,0)。保持模型、原版GUN0001/tokarev、h_Motion_hand_R挂载、D方案rotation+Bip01根translation不变。

以选定方向把51～54 / main[53]～main[56]四段各按30fps参考每2帧渲一张，共34张1024×1536透明RGBA。使用同一固定正交相机，从四段联合包围范围一次取景，不逐帧换镜头。取景尺度重新适配完整序列，方位角严格沿用用户选择。完整相机及每帧记录见render_records.json；沿用之前统一镜像展示朝右的约定。GIF宽320、中灰底，时长按源动作近似；PNG单帧真透明。

关键帧：
- key_gun_raise：序号51 / main[53]，第10帧。
- key_gun：序号52 / main[54]，第0帧。
- key_gun_recoil：序号53 / main[55]，第4帧。
- key_gun_lower：序号54 / main[56]，第4帧。

四张关键帧和联系表已查看用于选帧；检查所有PNG的RGBA/尺寸/非空及四边alpha为0。先仅提交pick参考，推送后确认origin，再以pick/key_gun作为编辑底图生成gun_a2，身份服装比例按idle_a3，脸眼按face_standard。用户未确认gun_a2前不做其他立绘过渡帧。

未打开结城理mod图片，未读obs.json或密码；art/cards原样保留不提交。提交号见本报告Git历史。

## gun_a2立绘（待用户验收）

pick原版参考已以9217d210推送并核对origin。随后以pick/key_gun作第一编辑输入，idle_a3作外观服装比例参考，face_standard作脸眼参考，调用内置image_gen一次成功。提示词要求保留底图相机/关节布局、微弯手肘、微翘手腕、枪高、头转向和脚位，不采用旧gun_a；记录见art/p5/poses/gun_a2_generation.json。未做生成后视觉review或重复生成，姿势是否匹配由用户验收，没有把提示词要求当成验收通过。

保存1024×1536 RGBA，按既定规则清理透明残点863像素。按idle_a3来源1.0比例、后脚锚定生成final/gun_a2，仅处理新图、不改旧final图。对比图gun_a2_reference_comparison.png将完整底图和立绘同为50%显示；原版渲染为了四段不出框留白较多，立绘按人物标准填画布，二者没有单独裁剪拉伸。

直接提交推送，推送后核对origin；gun_raise/recoil/lower立绘尚未开始，等待用户确认gun_a2。art/cards原样保留。

## 开枪组三张后续立绘（2026-10-26）

用户确认gun_a2“还可以，继续”，据此记为通过。仅制作gun_raise_a、gun_recoil_a、gun_lower_a，各自以pick对应关键帧为第一编辑输入，gun_a2定外观/画风/枪，idle_a3定身体比例，face_standard定脸眼。各调用内置image_gen一次成功，无安全拒绝或替代姿势；未做生成后视觉review。完整提示词和输入见各自generation.json。

对应参考：拔枪51/main[53]第10帧；后坐53/main[55]第4帧；收枪54/main[56]第4帧；相机均用户选定50% / 方位25.670096度。提示词分别要求手臂举到一半、上挑峰值带衣摆、翻手过脸并开始转身。实际是否匹配由用户看图验收，没有标成已通过。
- gun_raise_a：透明残点清理284像素。
- gun_recoil_a：透明残点清理193像素。
- gun_lower_a：透明残点清理111像素。

原图1024×1536 RGBA，清理仍仅清零主体外扩16px之外的alpha1/2。沿用gun_a2的1.0倍率和后脚锚定生成final版本；只运行本组三张，不改已有定稿final图。各帧有参考/立绘对比图，另有三张同缩放并排图。按用户长期授权直接提交推送并核对origin；本组三张待用户验收。art/cards和其他会话文件原样保留，不暂存、不提交。
