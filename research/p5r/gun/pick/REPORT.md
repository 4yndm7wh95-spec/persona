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
