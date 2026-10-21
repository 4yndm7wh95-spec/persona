# 撤回卡面替换与简化游戏设置

基线8c377599，版本仍0.14.105。删除卡图/卡框替换实现、所有对应Harmony补丁、开关、字体接口、空闲预热任务及v4资源生成脚本。旧配置中CardArt/CardFrame作为未知字段被忽略，下次保存不再输出，无法重新开启替换。移除RitsuLib metadata/schema及模组详情按钮。

设置重新注入NSettingsScreen的常规页，标题“结城理”；按SectionName去重并刷新滚动范围。保留原设置文件和其他选项的值。10项设置使用短名称，普通开关统一开启/关闭，界面样式显示P3风格/原版，抽牌排序显示原版顺序/按类型，选牌动画保留完整/精简/关闭。没有新增必须确认的流程。

资源清理：
- p3makoto/cards、card_frame、approved_cards、card_art_v4和对应.godot/imported产物全部清除，连缺失源图的孤立CTEX也清除。
- 清除自定义pile_card_back，HUD和牌堆翻转改读游戏原图images/packed/run_history/card_back.png，两版本均可加载。
- 删除Cinzel和LXGW卡名字体及各自许可。主体UI字体保留。
- 最终包629个相关文件删除，615个其他文件逐字节保持，包括936×1680高清门；不动门位置与螃蟹战朝向修复。选牌/召唤动画本身的塔罗图形和牌堆人物动画不属于卡面替换，保持。
- 源美术位于art/，不作为mod资源；另一会话尚未提交的源稿未修改。删除的未提交CardFrameSkin大剑改动和旧打包图预先单独备份在本次聊天工作目录work/card_skin_removal_backup.zip，不在mod内。

验证：
- 隔离源码Release构建0警告0错误；0.111.0与0.107.1各1198处引用全部找到。
- remove_card_skins.py --check对仓库打包目录和最终stage均通过。最终PCK615个文件的目录及内容逐个对照stage通过，资源包118724612字节。
- 编译DLL不包含CardFrameSkin、ApprovedCardArt、ModConfiguration等移除类名。
- 原生Godot离线分别载入两游戏的资源包及本次mod包，确认原版卡背可加载、高清门936×1680、所有替换目录缺席。没有启动游戏，没有Computer Use，不代表实机设置界面验收。
- 安装前检查退出，先备份两套三文件，再仅更新P3Makoto.dll/.pck/.json并核对哈希。完整构建和安装记录在mod/_build/remove_card_skins_20261021。

DLL SHA256: 60c38f2b694754534627b3c29b1e55232d0f767f90871a08001ef805714eea93

PCK SHA256: 1e15b506ef681ba5237c4143c0d9461362a7cf1023c494c7109e9f64dd8ff3f6
