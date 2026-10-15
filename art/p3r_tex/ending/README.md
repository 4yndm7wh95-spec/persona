# P3R「要闭上眼睛吗？」素材提取与天台背景

日期：2026-10-15。只新增本目录及 art/v16/ending 的素材；未修改代码、既有资源或游戏文件，未启动游戏。

## 推荐接入

1. **SC_close_eyes_trim.png**：原版简体中文“闭上眼睛”，264×104，透明PNG；保留闭/眼大、上/睛小的原版布局。需要原UV尺寸时用SC_close_eyes.png（510×166）。
2. **SC_question_trim.png**：原版“要 闭 上 眼 睛 吗 ？”，343×37，透明PNG。它本来就存在于图集中，不必拿字体近似绘制。
3. **SC_six_dots_trim.png**：原版六个小点，150×24。各有原UV尺寸版本及原版模糊版本。
4. **SC_lens_reflection.png / _trim.png**：原版柔光反光裁片。base__T_UI_CinemaScope_00_texture.png包含额外横向亮带与反光。是否作为左右反光需由接入方结合原材质用法判断，未声称已复原材质。
5. 动态简体中文可测试 **DFGBJH8.ttc**（DFLiJinHeiW8-GB）：所需“要闭上眼睛吗？闭上眼睛”字符全部可渲染。但它是黑体，不是截图文字的宋体/明朝样式，不能冒充截图原字体。若要忠实复现本句和大字样，优先用上面两张原版文字PNG。
6. 背景推荐 **art/v16/ending/ending_rooftop_b_wip.png**：更强柔焦、较冷灰紫。A版地面与栏杆更清楚、稍偏暖。用户后续明确要求“只保留场景”，两版已无人物、红木箱和箱上物品。

## 找到 / 未找到

|需求|结果|
|---|---|
|原版简中“闭上眼睛”大小字布局|找到，来自zh-Hans的CinemaScope_02图集；不是本次重绘。|
|原版“要闭上眼睛吗？”|找到，已导出原句PNG，保留原版间距。|
|中文字体原文件|找到DFGBJH8等6个原始ufont；按真实文件头分别使用ttc/ttf/otf后缀，字节完全未变。|
|与截图完全相同的宋体/明朝字体文件|未确认/未找到。图集文字是现成图形，不能仅凭外观确定它用哪份字体生成。|
|六个小点|找到，清晰和模糊原版都有。|
|镜头反光|找到图集中的柔光、横向亮带；已保留。|
|原版独立光标三角PNG|本次在CinemaScope关联贴图、SPR/PLG及消息材质中未找到。没有把自画三角当成提取素材。|
|2048×1152背景|未达到。实际A/B均1672×941，RGB全不透明；用户要求直接推送、不再重试，故使用_wip文件名。|

## 提取范围与方法

只读读取本机P3R的pak，复用既有CUE4Parse/CUE4Parse-Conversion 1.2.2.202610和Detex工具。游戏pak位置：G:/Gamebox/ZS15858/GAME15858/GAME15858/P3R/Content/Paks/。导出/解包/脚本中间目录：C:/Users/kkk/sts2_tools/p3r_ending_20261015/。

原FModel配置路径在应用虚拟化后不可用，后已在Codex包的LocalCache/Roaming/FModel中找到已有配置并只读复用。没有显示解密配置值、读取账号密码、绕过WindowsApps保护或修改FModel配置，也没有操作用户打开的FModel窗口。

检查了完整资源索引（当前provider挂载140007项），使用Ending、Ed、CloseEye、EyeClose、Eye、Sleep、Last、Choice、Select、Msg、Title及CinemaScope、MSGMIND、Font、DFT、DFGB、FOT、SC/CN/zh等词查找。实际匹配集中在Xrd777/UI/Message/Subtitles、UI/Misc/CinemaScope、消息材质及Font目录。导出本体和12种本地化CinemaScope_02图集，另保留TEST_CinemaScope候选；**TEST贴图只是原版调试素材，不应当作结局画面使用**。

SPR的纹理引用路径出现de本地化引用，不直接用于选择语言：本次明确从zh-Hans图集读取像素，只复用SPR中的UV坐标。裁片不重新绘制、不改变字形；_trim仅裁掉全透明边缘。原版贴图中的模糊字样与模糊小点也是原素材，不是本次人为模糊。

## 字体核对

ufont并非一定是单字体TTF：文件头ttcf为字体集合，故DFGBJH8/DFT_C8使用.ttc；OTTO为OpenType，使用.otf；00010000使用.ttf。全部逐字节复制并记录SHA256。未做格式转换、字体子集化或系统字体替代。

font_samples.png每个候选各渲染“要闭上眼睛吗？”与“闭上眼睛”两行；不使用中文回退字体。缺字候选的样张真实呈现缺字，并附Unicode提示；不能将其标作中文完整通过。

|文件|字体内部名称|所需简体字检查|
|---|---|---|
|DFGBJH8.ttc|DFLiJinHeiW8-GB|全部通过|
|DFT_C8.ttc|DFLiKingHei-XB|缺“闭、吗”|
|Font_FOT-SkipStd-E.otf|FOT-Skip Std E|缺“闭、吗”|
|FOT-SkipPro-E.otf|FOT-Skip Pro E|缺“闭、吗”|
|AsiaKSJ-B.ttf|KoreanSJB Regular|韩文字体，本句大部分汉字缺失|
|Amrys-Medium.otf|Amrys Medium|本句中文缺失|

## 背景与预览检查

- 内置imagegen生成/编辑；没有使用CLI/API替代。A共4轮，最终采用第4轮“删除人物和木箱”编辑。B采用第1轮，之后1轮尺寸重试未改善；第3轮被用户中止，未采用。按用户最后要求不再重画。
- 两版只有空天台：白天地面、铁栏杆、远处建筑与柔焦光线，无人物、木箱、UI或文字；RGB为全不透明背景。
- 尺寸未满足2048×1152：原始输出实际1672×941。原PNG没有拉伸或插值放大成“达标文件”，如实保留_wip。
- A版整体更暖、地缝仍可辨识，柔焦弱于截图；B版整体更灰紫且模糊更强，较适合本次效果。
- preview_ending.png是三栏对照：A、B、原截图。仅预览时将背景缩放到布局画布，左右各留13%，上边中央下沉2%、下边中央上拱2.5%。问句、右下大字和六点来自导出贴图；三角光标是预览专用几何近似，**没有作为原版提取素材交付**。镜头画框也是预览排版，并未改动背景PNG。
- 所有交付PNG已逐一解码打开；六个字体都能加载，中文覆盖情况如上。每个文件大小及来源在下表。

## 最终背景提示词

### A（编辑A第三轮的图，去掉人物/木箱）

```text
Edit this exact school rooftop background. REMOVE the entire small cluster in the left-middle: both people, the red wooden crate, the dark object on it, and their cast shadows. Replace that whole cluster seamlessly with the same pale concrete rooftop paving. Keep ONLY THE EMPTY ROOFTOP SCENE, distant metal railings, soft rooftop architecture, and pale lavender-grey daylight. No people, no human silhouettes, no crate, no box, no props, no ghost outlines or leftover shadows. Preserve high oblique overhead camera and full composition, with EXTREMELY STRONG soft-focus blur across the whole scene; concrete seams and railing almost dissolve into indistinct color masses. Full-bleed completely opaque image. No UI, no writing, no watermark. Output EXACT 2048x1152 pixel PNG (2K 16:9), without borders.
```

### B（以空天台A第四轮为参考）

```text
Edit this empty school rooftop background into a second version. Keep ONLY the empty scene: pale grey concrete paving, far iron safety railing, vague building forms. NO PEOPLE, no human silhouettes, no red wooden crate, no box or personal objects. Keep the same high oblique overhead viewpoint but cool the daylight toward bright pale grey-lavender, more neutral and less golden. Apply VERY STRONG optical defocus everywhere so railing, shadows and concrete seams are barely distinguishable color patches, like an unfocused view through sleepy eyes. All areas equally very blurry, no detailed foreground. Full-bleed opaque background, no UI, no text, no watermark, no frame. Output 2048x1152 pixels exactly, 2K landscape16:9.
```



## 文件大小与原始路径

|文件|字节|原始路径/来源|
|---|---:|---|
|art/p3r_tex/ending/Amrys-Medium.otf|286228|P3R/Content/Xrd777/Font/Amrys-Medium.ufont|
|art/p3r_tex/ending/AsiaKSJ-B.ttf|1074232|P3R/Content/Xrd777/Font/AsiaKSJ-B.ufont|
|art/p3r_tex/ending/base__T_UI_CinemaScope_00_texture.png|56334|P3R/Content/Xrd777/UI/Message/Subtitles/Sel/T_UI_CinemaScope_00_texture.uasset|
|art/p3r_tex/ending/base__T_UI_CinemaScope_01.png|8441|P3R/Content/Xrd777/UI/Message/Subtitles/Msg/T_UI_CinemaScope_01.uasset|
|art/p3r_tex/ending/base__T_UI_CinemaScope_02_texture.png|187160|P3R/Content/Xrd777/UI/Message/Subtitles/Sel/T_UI_CinemaScope_02_texture.uasset|
|art/p3r_tex/ending/base__T_UI_TEST_CinemaScope.png|32044|P3R/Content/Xrd777/Events/UI/T_UI_TEST_CinemaScope.uasset|
|art/p3r_tex/ending/contact_sheet.png|453356|本次imagegen输出或预览/样张合成|
|art/p3r_tex/ending/de__T_UI_CinemaScope_02_texture.png|200559|P3R/Content/L10N/de/Xrd777/UI/Message/Subtitles/Sel/T_UI_CinemaScope_02_texture.uasset|
|art/p3r_tex/ending/DFGBJH8.ttc|2039296|P3R/Content/Xrd777/Font/DFGBJH8.ufont|
|art/p3r_tex/ending/DFT_C8.ttc|5355600|P3R/Content/Xrd777/Font/DFT_C8.ufont|
|art/p3r_tex/ending/en__T_UI_CinemaScope_02_texture.png|192016|P3R/Content/L10N/en/Xrd777/UI/Message/Subtitles/Sel/T_UI_CinemaScope_02_texture.uasset|
|art/p3r_tex/ending/es__T_UI_CinemaScope_02_texture.png|190397|P3R/Content/L10N/es/Xrd777/UI/Message/Subtitles/Sel/T_UI_CinemaScope_02_texture.uasset|
|art/p3r_tex/ending/Font_FOT-SkipStd-E.otf|6559600|P3R/Content/Xrd777/Font/Font_FOT-SkipStd-E.ufont|
|art/p3r_tex/ending/font_samples.png|118752|本次imagegen输出或预览/样张合成|
|art/p3r_tex/ending/FOT-SkipPro-E.otf|9659908|P3R/Content/Xrd777/Font/FOT-SkipPro-E.ufont|
|art/p3r_tex/ending/fr__T_UI_CinemaScope_02_texture.png|197685|P3R/Content/L10N/fr/Xrd777/UI/Message/Subtitles/Sel/T_UI_CinemaScope_02_texture.uasset|
|art/p3r_tex/ending/it__T_UI_CinemaScope_02_texture.png|190053|P3R/Content/L10N/it/Xrd777/UI/Message/Subtitles/Sel/T_UI_CinemaScope_02_texture.uasset|
|art/p3r_tex/ending/ko__T_UI_CinemaScope_02_texture.png|184041|P3R/Content/L10N/ko/Xrd777/UI/Message/Subtitles/Sel/T_UI_CinemaScope_02_texture.uasset|
|art/p3r_tex/ending/pl__T_UI_CinemaScope_02_texture.png|193168|P3R/Content/L10N/pl/Xrd777/UI/Message/Subtitles/Sel/T_UI_CinemaScope_02_texture.uasset|
|art/p3r_tex/ending/pt__T_UI_CinemaScope_02_texture.png|188007|P3R/Content/L10N/pt/Xrd777/UI/Message/Subtitles/Sel/T_UI_CinemaScope_02_texture.uasset|
|art/p3r_tex/ending/ru__T_UI_CinemaScope_02_texture.png|192649|P3R/Content/L10N/ru/Xrd777/UI/Message/Subtitles/Sel/T_UI_CinemaScope_02_texture.uasset|
|art/p3r_tex/ending/SC_close_eyes.png|6942|P3R/Content/L10N/zh-Hans/Xrd777/UI/Message/Subtitles/Sel/T_UI_CinemaScope_02_texture.uasset|
|art/p3r_tex/ending/SC_close_eyes_blurred.png|19778|P3R/Content/L10N/zh-Hans/Xrd777/UI/Message/Subtitles/Sel/T_UI_CinemaScope_02_texture.uasset|
|art/p3r_tex/ending/SC_close_eyes_blurred_trim.png|18104|P3R/Content/L10N/zh-Hans/Xrd777/UI/Message/Subtitles/Sel/T_UI_CinemaScope_02_texture.uasset|
|art/p3r_tex/ending/SC_close_eyes_trim.png|5923|P3R/Content/L10N/zh-Hans/Xrd777/UI/Message/Subtitles/Sel/T_UI_CinemaScope_02_texture.uasset|
|art/p3r_tex/ending/SC_lens_reflection.png|78491|P3R/Content/L10N/zh-Hans/Xrd777/UI/Message/Subtitles/Sel/T_UI_CinemaScope_02_texture.uasset|
|art/p3r_tex/ending/SC_lens_reflection_trim.png|77184|P3R/Content/L10N/zh-Hans/Xrd777/UI/Message/Subtitles/Sel/T_UI_CinemaScope_02_texture.uasset|
|art/p3r_tex/ending/SC_question.png|5557|P3R/Content/L10N/zh-Hans/Xrd777/UI/Message/Subtitles/Sel/T_UI_CinemaScope_02_texture.uasset|
|art/p3r_tex/ending/SC_question_trim.png|4686|P3R/Content/L10N/zh-Hans/Xrd777/UI/Message/Subtitles/Sel/T_UI_CinemaScope_02_texture.uasset|
|art/p3r_tex/ending/SC_six_dots.png|2584|P3R/Content/L10N/zh-Hans/Xrd777/UI/Message/Subtitles/Sel/T_UI_CinemaScope_02_texture.uasset|
|art/p3r_tex/ending/SC_six_dots_blurred.png|5882|P3R/Content/L10N/zh-Hans/Xrd777/UI/Message/Subtitles/Sel/T_UI_CinemaScope_02_texture.uasset|
|art/p3r_tex/ending/SC_six_dots_blurred_trim.png|4441|P3R/Content/L10N/zh-Hans/Xrd777/UI/Message/Subtitles/Sel/T_UI_CinemaScope_02_texture.uasset|
|art/p3r_tex/ending/SC_six_dots_trim.png|1534|P3R/Content/L10N/zh-Hans/Xrd777/UI/Message/Subtitles/Sel/T_UI_CinemaScope_02_texture.uasset|
|art/p3r_tex/ending/tr__T_UI_CinemaScope_02_texture.png|194699|P3R/Content/L10N/tr/Xrd777/UI/Message/Subtitles/Sel/T_UI_CinemaScope_02_texture.uasset|
|art/p3r_tex/ending/zh-Hans__T_UI_CinemaScope_02_texture.png|186472|P3R/Content/L10N/zh-Hans/Xrd777/UI/Message/Subtitles/Sel/T_UI_CinemaScope_02_texture.uasset|
|art/p3r_tex/ending/zh-Hant__T_UI_CinemaScope_02_texture.png|187434|P3R/Content/L10N/zh-Hant/Xrd777/UI/Message/Subtitles/Sel/T_UI_CinemaScope_02_texture.uasset|
|art/v16/ending/ending_rooftop_a_wip.png|1589831|本次imagegen输出或预览/样张合成|
|art/v16/ending/ending_rooftop_b_wip.png|1468602|本次imagegen输出或预览/样张合成|
|art/v16/ending/preview_ending.png|1223282|本次imagegen输出或预览/样张合成|
