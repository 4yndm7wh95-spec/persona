# 第二批Persona草图（待确认）

- arsene/arsene_draft_v5.png：第4版为底图改稿，膝盖并拢弯曲、向右前倾、展翼；没有覆盖第4版。
- arsene/check_arsene_v5.png：左第4版、中第5版、右官方参考，深灰底。
- satanael/satanael_draft.png：腰部以上与巨枪，2560×1536，透明底。
- satanael/check_satanael.png：草图与本机原模型参考并排。
- 两张各生成1次，重画0次。均保留raw与完整generation.json；交付尺寸归一化，不声称原生分辨率。四角透明；未烘焙额外烟雾背景。

## 撒旦耶尔设计来源

BASE.CPK/MODEL/CHARACTER/PERSONA/0170/PS0170.GMD；ps_satanael_t01.dds、ps_satanael_t01_sp.dds、ps_satanael_t01_shadow.dds。模型含m_gun03、m_head、m_wing、m_body、m_eye等网格。通过既有GFDExport、fix_gfd_glb.py、Blender4.5.9导出satanael_ref.png，只作为设计参考；GLB只留本机库。另找到PSZ0170和事件PS0199含同名贴图，未混用。

检索了Satanael官方原画：网上结果有P5模型渲染、第三方转贴及P3R画廊，但未找到可核验的P5官网独立原画，故没有把其他版本或第三方图冒充官方输入。生图使用本机原版模型参考与Joker画风参考。

## 限制与待验收

都是草图，不是最终局内分层成品。撒旦耶尔的巨枪/手臂与身体仍有遮挡，翅膀靠近边缘；亚森翼尖也较接近边界。设计、姿势和后续拆层友好程度仍需用户/Claude确认，不自动根据自检重画。八岐大蛇及其他第一批图原样保留，不重画、不删除。不改mod代码、不编译、不启动游戏。
