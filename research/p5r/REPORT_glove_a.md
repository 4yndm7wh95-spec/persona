# 扯手套关键帧 glove_a（2026-10-27）

开始前fetch + merge快进至c3750cd30，保留召唤定稿、既有final及归一化参数。未rebase/reset/强推。

以idle_a3为底图编辑，face_standard为脸部参考；提示词要求站直、腿脚位置和比例照底图，右前臂弯至胸口、手背朝上、手指放松，左手拇指和食指捏住右手套腕口往里扯，头略低但视线仍向画面右方、嘴角微笑。不画特效、真透明底。

内置image_gen只生成一次，未审图重画，也未生成过渡帧。保存1024×1536 RGBA，清理主体外alpha1～2残点208像素；提示词、输入和原始输出路径见glove_a_generation.json。实际姿势和画面交给用户验收，不将提示词约束写成已验收结论。

原图及记录提交465da998e，推送后ls-remote已确认origin收到。新图登记1.0/rear，现有归一化函数仅对本批运行一次，合并normalize.json，不重写其他final、不加位移。

## 一次比例检查

`python tools/p5/pose_consistency.py idle_a3 glove_a`

```text
glove_a            head x0.982  body x1.002  boot height x1.000  boot width x1.005  feet moved [1, 1] px  ok
sheet: art\p5\poses\check_glove_a.png | overall: ok
```

输出原样记录，不根据结果重画。等待用户看过glove_a后才做过渡帧。检查拼图不提交，保存在本机outputs/p5_glove。

art/cards另一会话11个已修改文件原样保留，未提交、未stash、未还原；guard_a及其他无关资源保留。未打开结城理mod图片，未读取obs.json或密码。最终报告与登记推送后核对origin。
