# 整套插画与模组配置（0.14.105）

- 源码基线8807c262，包含门左25/下60的位置微调及螃蟹战朝向修复。
- 用户确认安装整套v4（91+3），源图从HEAD导出，未包含另一会话未提交的CardFrameSkin和v3大剑图。gather_light明确覆盖为认可稿，未采用工作区待审稿。
- 配置参考本机RegentFX.ThirdParty.RitsuLibModConfig及STS2RitsuLib.Settings.RuntimeInteropMirrorSource。使用程序集metadata发现和JSON schema，无新的硬依赖；同时提供模组详情“模组配置”入口。常规设置不再显示本模组设置。
- CardArt默认开启，与停用的CardFrame独立；旧配置迁移保留原值。切换立即更新已有NCard和后续Reload/UpdatePortrait，原版卡框保留。
- 94张原始PNG哈希见art_manifest.json，94张CTEX像素与源图一致。3张保留图维持原尺寸，未缩放。原生Godot4.5.1载入最终PCK，验证94张导入纹理及PNG哈希、936×1680高清门、排除未提交v3大剑。旧stage的961个文件逐字节保持。
- 隔离源码构建：0警告/0错误；0.111.0与0.107.1各1244处引用全部找到。
- 532项生产源码离线桩检查：94张切换、卡牌池化、缓存、恢复原图、UI字段保持、配置迁移/持久化、中英文schema和11项设置读写。桩检查不等于实机UI验收；没有启动游戏或使用Computer Use。
- 资源包428452516字节。完整构建记录和安装备份：mod/_build/card_suite_20261021/。
- 目标：G111/mods/P3Makoto及G107/mods/P3Makoto/P3Makoto（版本仍0.14.105）。只更新本模组三文件；安装前检查游戏进程退出，覆盖前备份，覆盖后核对哈希。

构建资源时，在已有完整stage上运行tools/pck/build_card_art_v4.py，避免从零打包漏掉已安装的历史资源。新资源283个文件使用私有路径，不覆盖原版图或卡框。

DLL SHA256: a7e561dbc38934c7f354093ccb1767bec347648e32b1d6089d404d1dfdd67020

PCK SHA256: 5e3acaa8971142424b42a3282d6549449003b28f3c5853dc4978de512cf5ef57
