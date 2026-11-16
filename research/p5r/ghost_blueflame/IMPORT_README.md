# 导入说明：P5R鬼脸与召唤蓝焰

## 这是什么

用户提供的P5R_original_ghost_blueflame.zip（539236字节），包含47个文件。原ZIP原样保存，解包内容保留原字节，原README没有改写。另加contact_sheet.png和IMPORT_CHECKS.json。

按包内README说明，两份完整资源来自BASE.CPK：
- BATTLE/SKILL/0066.BED：鬼脸、光层、烟雾等；包内说明将其对应为Eigaon。这里没有重新解码技能表独立验证该技能名。
- BATTLE/EFFECT/BE_SYOLOOP.EPL：人格面具召唤循环蓝焰。

目录包括original_packages（2份完整包）、ghost_subresources（10份GFS/EPD）、blueflame_subresources（6份GFS/GMD/EPT/EPD/EPL）、textures_dds和textures_png（各13张）、索引与清单。

## 看到了什么、怎么用

联系表可见：红白鬼脸、上升光线、烟雾遮罩、灰度噪声、发光/气流遮罩及火焰序列图。大部分蓝焰相关贴图本身是灰白遮罩，蓝色来自材质/颜色曲线，不应直接把PNG当最终蓝色动画。带黑底的噪声或加法用贴图也不能一律白底/黑底抠掉。

可给Claude用作P5 V4的原版贴图、噪声、序列图和材质/时间轴研究输入。Godot不能直接把BED/EPL/GFS/EPD当场景运行，PNG能作为贴图导入，完整效果仍需解析模型/时间轴/曲线或在游戏引擎里重建。

原包README提到12个切换火焰模型、材质动画速度字段3等结构，这些描述原样保留；本次没有重新解析二进制验证帧数/速度，也没有渲染完整动画。不能据此宣布完整特效已经还原。鬼脸与召唤蓝焰是两份不同资源，不代表原版同一招会一起播放。

## 已做校验

- file_manifest.json的46个记录全部通过文件大小与SHA256校验。
- texture_index.json的13张DDS全部与记录的原包偏移处字节一致。
- 13张PNG均可打开，尺寸与texture_index一致。
- 原ZIP的SHA256和检查计数见IMPORT_CHECKS.json。
- 这些校验确认压缩包内部一致性；尚未与本机BASE.CPK独立比对，因此资源出处以原包README声明为准。

未执行包内任何文件或指令，未修改游戏、mod代码，未启动游戏。全部内容作为研究资料提交，未安装进mod。
