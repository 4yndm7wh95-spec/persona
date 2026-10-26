# gun_recoil_a4：全身动感重绘（2026-10-26）

已fetch并快进merge到7f784c65。用户作废Claude脚本gun_recoil_a3（只动手臂、身体僵硬），本次从已定稿gun_a2做图像编辑，没有使用a3或双枪失败图作输入。

提示词要求把原本平伸的同一右臂和同一把枪上举、移除其旧位置；右肩向后带动上身微后倾、重心压后脚，衣摆与发梢向后外扬，头部大小和脸保持，腿脚位置与左侧空手保持。只允许一把枪在右手，食指贴扳机护圈外侧；无火光/目标/血/特效。完整输入、提示词和原始路径见gun_recoil_a4_generation.json。

## 次数和单枪检查

共生成2次，没有安全拒绝；两次都只有右手一把枪、左手为空，没有双枪或第二只持枪手，因此没有因双枪而重试。第1次因一致性脚本头部比例FAIL，按README规则从gun_a2重画一次；没有手工缩放凑数。第2次通过。除了用户明确要求的单枪检查和定量一致性脚本，没有额外视觉review或以自行审美判断重生成；动作动感与脸是否满足由用户验收。

## 实际一致性检查输出

执行命令：python tools/p5/pose_consistency.py gun_a2 gun_recoil_a4。scipy使用此前本任务work目录依赖，无系统环境修改。

第1次：
```text
gun_recoil_a4      head x0.941  boot height x1.015  boot width x1.012  feet moved [9, 46] px  FAIL
sheet: art\p5\poses\check_gun_recoil_a4.png | overall: FAIL
```

第2次：
```text
gun_recoil_a4      head x0.986  boot height x1.033  boot width x1.000  feet moved [3, 1] px  ok
sheet: art\p5\poses\check_gun_recoil_a4.png | overall: ok
```

脚本ok表示头部高度在5%阈值内，不表示腿脚或整张图逐像素不变。第2次脚位仍检测到3px/1px差异，报告如实保留；没有将生成约束当成逐像素验收。原始图保存为1024×1536 RGBA，没有改变图像尺寸或人物尺度。按既定规则清理极淡透明残点893像素（主体alpha>=128外扩16px外，alpha1/2置0）。

归一化遇到未覆盖情况：按现有1.0/mid对齐得到offset=[86,22]，会把右侧甩起衣摆移出1024画布，实测裁掉10288个alpha>128像素。因此该裁切产物仅留work诊断，不放入final、不登记a4，未擅自缩放或挪脚凑数。a3已由用户作废，其归一化登记移除，旧文件留历史；本次仅提交完整a4原图和报告，final接入待解决脚位锚定/画布边界。临时check图按规则不提交且已删除，另提供gun_recoil_a4_comparison.png（底图和a4完整画布同为50%，有横向参考线），供用户验收。

## 推送范围

完整原图、提示词记录、对比、移除作废a3的归一化登记、状态及中文报告；不提交裁切final产物。art/cards另一个会话的11个修改文件与未跟踪资料原样保留、不提交、不stash、不还原。未打开结城理mod图片，未读取obs.json或密码。推送后用ls-remote核对origin，提交号见本报告Git历史与会话。没有续做其他姿势。
