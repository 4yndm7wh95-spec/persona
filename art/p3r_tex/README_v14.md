# P3R 原版 UI / 总攻击音效补充导出

## 结果
只读读取本机 P3R pak 和既有 WAV 解包文件。沿用 CUE4Parse/CUE4Parse-Conversion（以前的纹理导出工具）、已有本机 FModel 解密配置及 ffmpeg。不启动游戏，不更改游戏文件、原有素材或代码；中间文件在 C:/Users/kkk/sts2_tools/p3r_v14_originals_20261014/。

新增50张素材PNG、4张contact_sheet.png、6个完整OGG及音效LIST.csv。本说明列出每张新素材的原路径。PNG保留原Texture2D尺寸及RGBA；原本不透明的灰度遮罩保持不透明，灰度并非背景抠图错误。多边形素材采用原始索引三角形、顶点RGBA颜色和原始坐标边界转成PNG，4倍采样抗锯齿，未重新设计或生成图片。多边形源没有固有位图分辨率，下表尺寸是原始坐标边界取整。

## 找到 / 未找到

|项目|结果|
|---|---|
|SHUFFLE TIME 开场标题|找到！PLG_UI_Battle_ShuffleTime 的 PlgDatas[27]，名称 shuffle time；转换为1530×184透明PNG。它是原版多边形字形，不在同名Texture2D图集中。|
|名字带Shuffle的贴图|找到26个（本体+12种本地化语言各2张），24张新导出，英文2张已有故复用。所有原路径逐条列在下面。|
|大号PERSONA标题|找到！PLG_UI_Battle 的 PlgDatas[3] Skill_TitleLogo 实际显示PERSONA，1240×245；同时导出2636×9的配套分隔线及日/英文指令字样图集。|
|蓝色结城理菜单剪影|未找到静态Texture2D。检查MI_BtlSillhouette确认其RenderTargets参数引用运行时TextureRenderTarget2D RT_Temp0，不能作为现成主角PNG导出；未拿别的立绘代替。|
|菜单斜切色带|未找到完整配色的独立PNG。找到配套标题线轮廓和MM_Btl_SideColor_00等材质线索；原材质动态背景效果没有冒充导出PNG。|
|总攻击其他贴图|导出11张：9个角色效果灰度遮罩、AlloutCheck、AlloutUI。已导出的5张主角Finish贴图不重复；浏览图包含它们供对照。|
|技能列表底板 / 属性图标|找到2种SkillName_base底板原始轮廓以及日/英文02、03、04、07四组图集（8张），包含属性图标、选择指示、条框和技能提示。最终选中状态的配色/材质效果没有静态整张贴图，不能保证这两个底板就是完整高亮效果。|
|Zoom / Extermination / Kime01完整音效|均找到，约3.1、4.5、2.9秒，不剪切。|
|未导出过的ShuffleTime系统音效|catalog匹配17条，全部已在sfx_p3r或sfx_p3r_sys2导出过；本次全部跳过，不重复。|
|战斗菜单音效|导出Btl_CommandPanel、Sys_Cursor以及战斗确认Btl_Ok（作为原名保留，非Decide命名）。Sys_Cursor是通用系统光标，未验证只供战斗使用；未找到独立命名Decide的战斗菜单音效。|

## 检索范围与关键词
本机原资源：G:/Gamebox/ZS15858/GAME15858/GAME15858/P3R/Content/Paks/；资源索引140007条，导出工具报告1个未挂载归档，因此结论仅限成功挂载的资源，不能保证遗漏归档没有其他素材。

搜索Battle/UI/Sprite、Battle/UI/Texture、Battle/UI/Polygon、Battle/UI/Material、UI/Battle/Layout、UI/Camp/Textures/HeroGaussMask、Blueprints/Battle/GUI、Battle/Camera/CommandCamera及L10N语言版本；关键词ShuffleTime、Shuffle、Title、Logo、Command、Cmd、Skill、Persona、Pc01、Btl_UI、List、Icon、Allout。查看基础及英文Battle_00/02/03/04/05/06/07图集；05/06是伤害数字/战斗提示，本次不新增；HeroGaussMaskSkill是模糊场景遮罩，未冒充主角剪影。

关键非贴图线索：
- P3R/Content/Xrd777/Battle/UI/Polygon/PLG_UI_Battle_ShuffleTime.uasset
- P3R/Content/Xrd777/Battle/UI/Polygon/PLG_UI_Battle.uasset
- P3R/Content/Xrd777/Battle/UI/Material/MI_BtlSillhouette.uasset -> MM_BtlSillhouette -> /Game/Xrd777/Kernel/Textures/RT_Temp0
- P3R/Content/Xrd777/Battle/UI/Material/MM_Btl_SideColor_00.uasset
- P3R/Content/Xrd777/UI/Battle/Layout/DT_UILayout_BattleShuffleText.uasset
- P3R/Content/Xrd777/Blueprints/Battle/GUI/BP_BtlGuiShuffleSceneCapture2D.uasset

音效搜索research/p3r_se/catalog.csv里的Xrd777__JP__system及Battle库，关键词Allout_Zoom、Allout_Extermination、Allout_Kime01、ShuffleTime、Command、Cursor、Decide、Skill，并核对既有LIST.csv去重。排除敌人动作skill、商店/邮件/Velvet光标、aegis_command_pose和技能获得/合成音效，未用这些替代战斗菜单声音。

## 音频格式
6个OGG都是Vorbis q=5、48000Hz、2声道；没有-t/atrim、归一化、降噪或音量处理。3条总攻击源本身为48000Hz立体声；菜单3条源为48000Hz单声道，按要求复制到左右声道，**不是原生双声道空间音频**。原始内容完整保留，OGG容器时长比catalog多约0.002667秒，来自编码时长/填充，不是剪切。

## 新增贴图对应表

|文件（相对art/p3r_tex）|尺寸|原资源路径|
|---|---|---|
|allout/base__T_FX_Pc0010Allout_00_Alp.png|1024×1024|P3R/Content/Xrd777/Effects/Textures/Alpha/T_FX_Pc0010Allout_00_Alp.uasset|
|allout/base__T_FX_Pc0009Allout_00_Alp.png|1024×1024|P3R/Content/Xrd777/Effects/Textures/Alpha/T_FX_Pc0009Allout_00_Alp.uasset|
|allout/base__T_FX_Pc0008Allout_00_Alp.png|1024×1024|P3R/Content/Xrd777/Effects/Textures/Alpha/T_FX_Pc0008Allout_00_Alp.uasset|
|allout/base__T_FX_Pc0007Allout_00_Alp.png|1024×1024|P3R/Content/Xrd777/Effects/Textures/Alpha/T_FX_Pc0007Allout_00_Alp.uasset|
|allout/base__T_FX_Pc0005Allout_00_Alp.png|1024×1024|P3R/Content/Xrd777/Effects/Textures/Alpha/T_FX_Pc0005Allout_00_Alp.uasset|
|allout/base__T_FX_Pc0004Allout_00_Alp.png|1024×1024|P3R/Content/Xrd777/Effects/Textures/Alpha/T_FX_Pc0004Allout_00_Alp.uasset|
|allout/base__T_FX_Pc0002Allout_00_Alp.png|1024×1024|P3R/Content/Xrd777/Effects/Textures/Alpha/T_FX_Pc0002Allout_00_Alp.uasset|
|allout/base__T_FX_Pc0003Allout_00_Alp.png|1024×1024|P3R/Content/Xrd777/Effects/Textures/Alpha/T_FX_Pc0003Allout_00_Alp.uasset|
|allout/base__T_FX_Pc0001Allout_00_Alp.png|1024×1024|P3R/Content/Xrd777/Effects/Textures/Alpha/T_FX_Pc0001Allout_00_Alp.uasset|
|shuffle/base__T_UI_Battle_ShuffleTime_01_texture.png|1024×1024|P3R/Content/Xrd777/Battle/UI/Sprite/T_UI_Battle_ShuffleTime_01_texture.uasset|
|shuffle/base__T_UI_Battle_ShuffleTime_00_texture.png|2048×2048|P3R/Content/Xrd777/Battle/UI/Sprite/T_UI_Battle_ShuffleTime_00_texture.uasset|
|skill/base__T_UI_Battle_07_texture.png|2048×1024|P3R/Content/Xrd777/Battle/UI/Sprite/T_UI_Battle_07_texture.uasset|
|skill/base__T_UI_Battle_04_texture.png|1024×512|P3R/Content/Xrd777/Battle/UI/Sprite/T_UI_Battle_04_texture.uasset|
|skill/base__T_UI_Battle_03_texture.png|1024×1024|P3R/Content/Xrd777/Battle/UI/Sprite/T_UI_Battle_03_texture.uasset|
|skill/base__T_UI_Battle_02_texture.png|1024×512|P3R/Content/Xrd777/Battle/UI/Sprite/T_UI_Battle_02_texture.uasset|
|command/base__T_UI_Battle_00_texture.png|1024×1024|P3R/Content/Xrd777/Battle/UI/Sprite/T_UI_Battle_00_texture.uasset|
|allout/base__T_Btl_AlloutCheck_00.png|512×512|P3R/Content/Xrd777/Battle/Allout/T_Btl_AlloutCheck_00.uasset|
|allout/base__T_Btl_AlloutUI_00.png|1024×1024|P3R/Content/Xrd777/Battle/Allout/T_Btl_AlloutUI_00.uasset|
|shuffle/pl__T_UI_Battle_ShuffleTime_01_texture.png|1024×1024|P3R/Content/L10N/pl/Xrd777/Battle/UI/Sprite/T_UI_Battle_ShuffleTime_01_texture.uasset|
|shuffle/pl__T_UI_Battle_ShuffleTime_00_texture.png|2048×2048|P3R/Content/L10N/pl/Xrd777/Battle/UI/Sprite/T_UI_Battle_ShuffleTime_00_texture.uasset|
|skill/en__T_UI_Battle_07_texture.png|2048×1024|P3R/Content/L10N/en/Xrd777/Battle/UI/Sprite/T_UI_Battle_07_texture.uasset|
|skill/en__T_UI_Battle_04_texture.png|1024×512|P3R/Content/L10N/en/Xrd777/Battle/UI/Sprite/T_UI_Battle_04_texture.uasset|
|skill/en__T_UI_Battle_03_texture.png|1024×1024|P3R/Content/L10N/en/Xrd777/Battle/UI/Sprite/T_UI_Battle_03_texture.uasset|
|skill/en__T_UI_Battle_02_texture.png|1024×512|P3R/Content/L10N/en/Xrd777/Battle/UI/Sprite/T_UI_Battle_02_texture.uasset|
|command/en__T_UI_Battle_00_texture.png|1024×1024|P3R/Content/L10N/en/Xrd777/Battle/UI/Sprite/T_UI_Battle_00_texture.uasset|
|shuffle/pt__T_UI_Battle_ShuffleTime_01_texture.png|1024×1024|P3R/Content/L10N/pt/Xrd777/Battle/UI/Sprite/T_UI_Battle_ShuffleTime_01_texture.uasset|
|shuffle/pt__T_UI_Battle_ShuffleTime_00_texture.png|2048×2048|P3R/Content/L10N/pt/Xrd777/Battle/UI/Sprite/T_UI_Battle_ShuffleTime_00_texture.uasset|
|shuffle/it__T_UI_Battle_ShuffleTime_01_texture.png|1024×1024|P3R/Content/L10N/it/Xrd777/Battle/UI/Sprite/T_UI_Battle_ShuffleTime_01_texture.uasset|
|shuffle/it__T_UI_Battle_ShuffleTime_00_texture.png|2048×2048|P3R/Content/L10N/it/Xrd777/Battle/UI/Sprite/T_UI_Battle_ShuffleTime_00_texture.uasset|
|shuffle/fr__T_UI_Battle_ShuffleTime_01_texture.png|1024×1024|P3R/Content/L10N/fr/Xrd777/Battle/UI/Sprite/T_UI_Battle_ShuffleTime_01_texture.uasset|
|shuffle/fr__T_UI_Battle_ShuffleTime_00_texture.png|2048×2048|P3R/Content/L10N/fr/Xrd777/Battle/UI/Sprite/T_UI_Battle_ShuffleTime_00_texture.uasset|
|shuffle/de__T_UI_Battle_ShuffleTime_01_texture.png|1024×1024|P3R/Content/L10N/de/Xrd777/Battle/UI/Sprite/T_UI_Battle_ShuffleTime_01_texture.uasset|
|shuffle/de__T_UI_Battle_ShuffleTime_00_texture.png|2048×2048|P3R/Content/L10N/de/Xrd777/Battle/UI/Sprite/T_UI_Battle_ShuffleTime_00_texture.uasset|
|shuffle/es__T_UI_Battle_ShuffleTime_01_texture.png|1024×1024|P3R/Content/L10N/es/Xrd777/Battle/UI/Sprite/T_UI_Battle_ShuffleTime_01_texture.uasset|
|shuffle/es__T_UI_Battle_ShuffleTime_00_texture.png|2048×2048|P3R/Content/L10N/es/Xrd777/Battle/UI/Sprite/T_UI_Battle_ShuffleTime_00_texture.uasset|
|shuffle/ru__T_UI_Battle_ShuffleTime_01_texture.png|1024×1024|P3R/Content/L10N/ru/Xrd777/Battle/UI/Sprite/T_UI_Battle_ShuffleTime_01_texture.uasset|
|shuffle/ru__T_UI_Battle_ShuffleTime_00_texture.png|2048×2048|P3R/Content/L10N/ru/Xrd777/Battle/UI/Sprite/T_UI_Battle_ShuffleTime_00_texture.uasset|
|shuffle/tr__T_UI_Battle_ShuffleTime_01_texture.png|1024×1024|P3R/Content/L10N/tr/Xrd777/Battle/UI/Sprite/T_UI_Battle_ShuffleTime_01_texture.uasset|
|shuffle/tr__T_UI_Battle_ShuffleTime_00_texture.png|2048×2048|P3R/Content/L10N/tr/Xrd777/Battle/UI/Sprite/T_UI_Battle_ShuffleTime_00_texture.uasset|
|shuffle/zh-Hant__T_UI_Battle_ShuffleTime_01_texture.png|1024×1024|P3R/Content/L10N/zh-Hant/Xrd777/Battle/UI/Sprite/T_UI_Battle_ShuffleTime_01_texture.uasset|
|shuffle/zh-Hant__T_UI_Battle_ShuffleTime_00_texture.png|2048×2048|P3R/Content/L10N/zh-Hant/Xrd777/Battle/UI/Sprite/T_UI_Battle_ShuffleTime_00_texture.uasset|
|shuffle/zh-Hans__T_UI_Battle_ShuffleTime_01_texture.png|1024×1024|P3R/Content/L10N/zh-Hans/Xrd777/Battle/UI/Sprite/T_UI_Battle_ShuffleTime_01_texture.uasset|
|shuffle/zh-Hans__T_UI_Battle_ShuffleTime_00_texture.png|2048×2048|P3R/Content/L10N/zh-Hans/Xrd777/Battle/UI/Sprite/T_UI_Battle_ShuffleTime_00_texture.uasset|
|shuffle/ko__T_UI_Battle_ShuffleTime_01_texture.png|1024×1024|P3R/Content/L10N/ko/Xrd777/Battle/UI/Sprite/T_UI_Battle_ShuffleTime_01_texture.uasset|
|shuffle/ko__T_UI_Battle_ShuffleTime_00_texture.png|2048×2048|P3R/Content/L10N/ko/Xrd777/Battle/UI/Sprite/T_UI_Battle_ShuffleTime_00_texture.uasset|
|shuffle/PLG_UI_Battle_ShuffleTime__shuffle_time.png|1530×184|P3R/Content/Xrd777/Battle/UI/Polygon/PLG_UI_Battle_ShuffleTime.uasset#PlgDatas[27] shuffle time|
|skill/PLG_UI_Battle__SkillName_base_1.png|453×63|P3R/Content/Xrd777/Battle/UI/Polygon/PLG_UI_Battle.uasset#PlgDatas[0] SkillName_base_1|
|skill/PLG_UI_Battle__SkillName_base_2.png|528×75|P3R/Content/Xrd777/Battle/UI/Polygon/PLG_UI_Battle.uasset#PlgDatas[1] SkillName_base_2|
|command/PLG_UI_Battle__Skill_TitleLogo_line.png|2636×9|P3R/Content/Xrd777/Battle/UI/Polygon/PLG_UI_Battle.uasset#PlgDatas[2] Skill_TitleLogo_line|
|command/PLG_UI_Battle__Skill_TitleLogo.png|1240×245|P3R/Content/Xrd777/Battle/UI/Polygon/PLG_UI_Battle.uasset#PlgDatas[3] Skill_TitleLogo|

## 已有英文Shuffle图集（未覆盖）

|文件|原资源路径|
|---|---|
|shuffle/T_UI_Battle_ShuffleTime_01_texture.png|P3R/Content/L10N/en/Xrd777/Battle/UI/Sprite/T_UI_Battle_ShuffleTime_01_texture.uasset|
|shuffle/T_UI_Battle_ShuffleTime_00_texture.png|P3R/Content/L10N/en/Xrd777/Battle/UI/Sprite/T_UI_Battle_ShuffleTime_00_texture.uasset|

## 新增音效对应表

来源均为Xrd777__JP__system；序号及cue以catalog为准。

|文件|序号|原时长秒|OGG时长秒|原始WAV路径|
|---|---:|---:|---:|---|
|Btl_CommandPanel.ogg|9|0.309458|0.312125|C:\Users\kkk\sts2_tools\p3r_rip\se\Xrd777__JP__system\Btl_CommandPanel__00009.wav|
|Btl_Ok.ogg|17|0.312521|0.315188|C:\Users\kkk\sts2_tools\p3r_rip\se\Xrd777__JP__system\Btl_Ok__00017.wav|
|Sys_Cursor.ogg|186|0.600021|0.602688|C:\Users\kkk\sts2_tools\p3r_rip\se\Xrd777__JP__system\Sys_Cursor__00186.wav|
|Snd_FX_Btl_Allout_Extermination.ogg|425|4.506542|4.509208|C:\Users\kkk\sts2_tools\p3r_rip\se\Xrd777__JP__system\Snd_FX_Btl_Allout_Extermination__00425.wav|
|Snd_FX_Btl_Allout_Kime01.ogg|426|2.924562|2.927229|C:\Users\kkk\sts2_tools\p3r_rip\se\Xrd777__JP__system\Snd_FX_Btl_Allout_Kime01__00426.wav|
|Snd_FX_Btl_Allout_Zoom.ogg|429|3.100021|3.102688|C:\Users\kkk\sts2_tools\p3r_rip\se\Xrd777__JP__system\Snd_FX_Btl_Allout_Zoom__00429.wav|

## 跳过的已导出Shuffle音效

- system #31: ShuffleTime_Persona; Snd_FX_Btl_PersonaAura_00
- system #42: ShuffleTime_ParameterUp
- system #45: ShuffleTime_ExpUp
- system #196: ShuffleTime_Skill; persona_change
- system #408: ShuffleTime_ArcanaBurst
- system #409: ShuffleTime_ArcanaCardSet1
- system #410: ShuffleTime_ArcanaCardSet2
- system #411: ShuffleTime_CardSet
- system #412: ShuffleTime_ItemGet
- system #413: ShuffleTime_Cup_Kaja
- system #414: ShuffleTime_PowerUp
- system #415: ShuffleTime_Result
- system #416: ShuffleTime_Select
- system #417: ShuffleTime_SpArcana_Flip
- system #418: ShuffleTime_Start
- system #419: ShuffleTime_Stockover
- system #420: ShuffleTime_Whoosh

## 浏览
每类的contact_sheet.png包含缩略图、文件名及原尺寸；棋盘格仅在浏览图中，素材PNG本身没有添加背景。shuffle/allout浏览图包含对应目录的已有素材，旧文件未重新导出或覆盖。
