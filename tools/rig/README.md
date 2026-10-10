# tools/rig
- `build_rig_v4.py`：**当前使用**。近侧右臂 + 姿势手绘手臂（见 docs/RIG.md 第 9 节）。
- `build_rig.py`：v1~v3（远侧左臂，已废弃）；保留其中的通用工具函数（Pose / Spring / 抠图 / 图集 / stage_pck），v4 从这里导入。
- `spine_eval.py`：最小 Spine 求值与渲染器，用于校验和 GIF 预览。
