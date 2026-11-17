# P5R 原版鬼脸与蓝焰资源

本目录只打包原版资源与直接提取件，不包含试作动画、自定义改色或粒子参数。

## 原始完整包

- original_packages/0066.BED：BATTLE/SKILL/0066.BED，含红色火焰鬼脸、底部光层、烟雾、模型粒子和原始时间轴。按技能编号对应Eigaon。
- original_packages/BE_SYOLOOP.EPL：BATTLE/EFFECT/BE_SYOLOOP.EPL，人格面具召唤循环蓝焰，含原始模型、颜色曲线、烟雾、火星和光照配置。

这两份来自本机P5R原始BASE.CPK。蓝焰属于P5R召唤资源；此打包不表示P5R原版把这两包作为同一招一起播放。用户参考视频已确认来自P5X，本任务只提供可复用的P5R原始资源。

## 已拆出的原始子资源

- ghost_subresources：鬼脸主体 be0cmn_mud_face_up.GFS、背光、底部光层、上升光线、环绕气流、消散模型及烟雾EPD。
- blueflame_subresources：bes_syo_fkoma12kbend.GMD 包含12个逐帧切换的火焰模型及原始材质动画（速度字段3）；另含卷曲焰片EPT、烟雾EPD、火星EPL和蓝光GFS。
- textures_dds：从原始包直接截取的DDS字节，未经重编码。
- textures_png：上述DDS直接转成PNG，供查看和导入；颜色保持素材原值。

原始模型、贴图均不包含本次生成的几何图形或绘制贴图。颜色曲线、发射配置等以原始完整包为准；拆出贴图不等于还原完整原版材质。

texture_index.json记录每张DDS来源、原包偏移、尺寸与校验值。file_manifest.json记录整个资源集的SHA256。
