# summon_grip_a：毒性爆发，攥紧自己的脸

生图任务（底图、蒙版、提示词、检查、提交）统一写在 `docs/codex/codex_p5_summon_grip.md`。

要点：底图用 `art/p5/poses/summon_hold_a.png`，只局部重画两只红手套（盖脸的手攥成爪、垂着的手攥成拳），其他像素 0 改动。
**我们的 Joker 姿势图脸上没有实体面具**（面具是代码画的蓝焰特效），提示词里不要出现 mask。
第 1 版的错误示例：`art/p5/poses/ref_grip_v1_wrong.png`。

图进了 `art/p5/poses/final/summon_grip_a.png` 之后：

```bash
python tools/p5/build_rig_p5.py cast_outbreak_from_mask cast_outbreak
python tools/p5/stage_p5.py
```
