# 伊邪那岐大神：模型确认与动作候选

## 已完成
- 原包 BASE.CPK，本机只读提取；模型 ID **PS0360**，原路径 MODEL/CHARACTER/PERSONA/0360/PS0360.GMD（11,097,047 字节）。
- 模型内贴图明确名为 ps_izanaginoookami_t01_sh.dds、ps_izanaginoookami_t01_sp.dds、ps_izanaginoookami_t02.dds。白金配色、环形双端长兵器，联系表可见。由内部纹理名和外观确认是伊邪那岐大神，未使用普通伊邪那岐模型，也未选择 PSZ0360 异色模型。
- 动作包 MODEL/CHARACTER/PERSONA/0360/BPS0360_000.GAP 与 GT0360.GAP。14 个条目中10个有骨骼PRS轨道、4个无轨道；全部有轨道条目已各渲等间隔12帧，见 *_contact.png，具体采样帧和相机参数见 animation_catalog.json。
- 复用亚森流程：GFDExport → tools/p5r/fix_gfd_glb.py → attach_animation_by_name 精确骨骼名挂载 → 保留原始旋转、跳过root旋转 → Blender。
- 联系表采用384×512透明单帧缩略图、6×2排布，联系表底色浅灰用于查看透明轮廓。相机方向沿用亚森 cast 的 (-0.8,-1,0)，正交、每段固定机位并以12帧包围盒统一取景。没有逐帧移动相机。
- 完整本机中间产物：C:/Users/kkk/sts2_tools/p5r_rip/fx_work/izanagi_okami/。原模型、GLB和动作未重复提交仓库。

## 动作选择仍需用户确认，暂不导出整段
- **候选 main[5]（BPS0360_000_main_0005）**：3.133秒；环形长兵器从上举到横向、大幅绕身转动，再回到起始姿态，最接近大幅转杖。
- **候选 main[6]（BPS0360_000_main_0006）**：1.667秒；长杖转为竖直、双手立于胸前，中段横向翻转，随后保持竖持。也可能是用户指的技能前摇。
- main[7]、[8]、[9]、[10] 更像上述竖持段的保持/过渡/收回，单看采样无法证明哪一段接在发动技能前。
- main[0] 与 GT 的三个条目为循环/表演候选，不能仅凭编号确认登场和待机语义。
- **按任务的“不确定就只交联系表”规则，本轮只交候选联系表与证据，未猜选attack/appear/idle，未创建这些成品目录。** 待选定动作后再完整导出30fps并记录裁切及foot/body_center锚点。
- 模型导出最初使用了旧流程raw路径，工具提示目录不存在；改为提取器实际输出的model/CHARACTER/PERSONA/0360路径后，10段动作导出正常。此项是路径修正，没有改动工具或原始游戏文件。

## 工具与限制
- Blender4.5.9、内置GFDExport、fix_gfd_glb.py。
- 未启动游戏、未使用Computer Use、未读取obs.json/密码、未修改游戏原文件；art/cards等其他会话文件不动。

