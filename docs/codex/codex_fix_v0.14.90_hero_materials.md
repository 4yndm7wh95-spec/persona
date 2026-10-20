你是这个项目的"手"。Claude 审完了你推的 v0.14.90（8b64869），代码结构很好，类型检查、shader 编译、两个版本的 API 检查都过了。下面是要改的地方（还是 0.14.90，不改版本号）。仓库 / git / 绝对不要做 的规则同 docs/codex/codex_impl_v0.14.90_pile_hero3d.md。不启动游戏。

【背景】用户对照原版截图指出：我们的立绘描边太粗、手上线条粗、嘴糊，而且原版人物有不少部分是和后面的水融在一起的。Claude 已在 tools/p3r/hero_render/render.gd 里调好并对过截图，这次把它搬进游戏。

【1. Shaders.cs】
1. HeroRamp：照 render.gd 新的 RAMP 改（vertex 里算世界法线 wn；固定光方向 (-0.4, 0.6, 0.7)；lit = smoothstep(-0.3, -0.1, nl)；c *= mix(shade, 1, lit)；边缘光 rim = pow(1 − clamp(dot(NORMAL, VIEW)), 4) × 0.6 混 (0.80, 0.94, 1.0)；最后 c = mix(c, 纯绿, 0.5 × (1 − lit))）。c0..c3 / shade 的数值照抄 render.gd。去掉 mask_pass。
2. HeroOutline：col = (0.04, 0.10, 0.30)，width = 0.0012。
3. HeroKeyOut：改成"去溢色"算法（不再用距离阈值）：
   s = clamp(t.g − max(t.r, t.b), 0, 1); alpha = 1 − s; rgb = (t.rgb − vec3(0, s, 0)) / max(alpha, 0.001);
   COLOR = vec4(rgb, t.a × alpha) × hero_modulate。
   （主角所有颜色都满足 g ≤ max(r, b)，所以只有色键会被扣掉；MSAA 边缘的半绿像素会还原成正确颜色 + 半透明，不会有绿边。）
   HeroStage 里 KeyDistanceLow / KeyDistanceHigh 和对应的 SetShaderParameter 删掉。

【2. HeroStage.cs MaterialFor】
1. CaRtOp（名字以 CaRtOp 结尾）：col = HeroKey（纯色键 = 完全透明，直接透出后面的水；用户确认：不要自己填色），**不挂描边**。
2. 描边只挂在 FILLS / 贴图材质 / 眼睛上（现在是所有 m != null 都挂）。

【3. PileFx.cs】
退场开始时（exiting = true 那里）不要 HeroStage.Close(right) —— 现在会让人物在其他元素滑出时突然消失。删掉这一行；right 被销毁时 TreeExiting 已经会 Close（HeroStage.Open 里挂了 owner.TreeExiting += Close）。Rollback 里那处保留。

【4. 自检、提交、推送】
1. dotnet build 通过，0 个新警告；对照上面逐条打勾。
2. 只 add mod/src；提交信息："v0.14.90: hero materials matched to the original COMMU screenshot (toon + rim, thin dark outline, water shows through jacket/hair/shadows), despill key-out, keep hero visible during pile exit"
3. push（规则同前，被拒先 fetch + merge --no-edit）；中文简短报告 + 提交号。
