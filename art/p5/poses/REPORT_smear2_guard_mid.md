# 残影重绘与防御过渡帧（2026-10-26）

已fetch并merge到5d46fa0e。用户作废旧slash_smear_a；保留旧图历史，不作为新图输入。slash_smear_a2以knife_ready_a、slash_a3、grip_reverse、face_standard和新基准idle_a3输入，要求中间姿势、一只右手一把刀，身后一道连续白灰色半透明速度弧。guard_mid_a以idle_a3输入，guard_a2作终点、face_standard定脸，要求右前臂抬到胸前一半、重心开始后移。

两张各生图一次成功，未做视觉review或重试。后续新姿势统一以idle_a3为基准。完整提示词和输入路径见各generation.json。
- slash_smear_a2：按既定规则清理透明残点734像素。
- guard_mid_a：按既定规则清理透明残点402像素。

按最新版规划登记normalize_poses.py，沿用来源比例：残影0.96、两脚中点锚定（同slash_a3）；防御过渡1.0、后脚锚定（同idle_a3），不做视觉判断重新调比例。输出final/用于游戏，原生成图不改。按用户授权直接推送，待用户验收。art/cards未改动、不提交。

归一化脚本首次缺scipy；已仅在本任务work/pose_normalize_deps安装并运行成功，没有修改系统Python环境。两张final输出和normalize.json随本次提交。
