# 阶段 11 全部生成原图

共15张，全部1024×1536，未裁剪、抠图或后处理。包含失败候选；收集交付不代表通过验收。

用户选择首轮第一张作为底图：01_portrait_a_original.png，对应 ../portrait_a.png。底图不得重画。

**去手层仍不合格**：../portrait_a_nohand.png 对应13_nohand_fix2.png，用户明确拒绝脸型。15号也被拒绝。Claude 请勿直接采用这些去手图作为合格分层。

| 文件 | 状态 |
|---|---|
| 01_portrait_a_original.png | 首轮 A；用户选定底图，保持不动 |
| 02_portrait_b_original.png | 首轮 B |
| 03_portrait_c_original.png | 首轮 C |
| 04_portrait_a_retry1.png | A 第一次重试，未选用 |
| 05_portrait_b_retry1.png | B 第一次重试，未选用 |
| 06_portrait_c_retry1.png | C 第一次重试，未选用 |
| 07_portrait_retry2_interrupted.png | 第二次重试中断时留存，未选用 |
| 08_hand_initial.png | 初始手臂层；镜片内部两处错误绿洞 |
| 09_nohand_initial.png | 初始去手层；用户指出脸型不对 |
| 10_blink.png | 闭眼层；真实眼睛和镜中眼睛闭合 |
| 11_nohand_fix1.png | 去手修正1；仍改变五官位置 |
| 12_hand_glass_fixed.png | 手臂层修正；两处绿洞填为镜片反射 |
| 13_nohand_fix2.png | 去手修正2；用户明确指出下巴和原图不同，不合格 |
| 14_nohand_jaw_attempt.png | 下颌恢复尝试；仍未恢复原脸 |
| 15_nohand_remove_from_original.png | 从原底图重新去手；用户指出更畸形，不合格 |

建议交接：以01原图作为固定底稿，保留所有已经可见的下颌、鼻嘴、眼睛和脸部像素；只在手和镜片遮挡区域内做局部补画。整图 imagegen 去手反复移动五官、改变头部轮廓，不能当作简单配准误差。12号镜片修正可用于手臂层。10号为闭眼候选。未发生审核拒绝。

