# 状态查看按钮移到血条右侧（基于 v0.14.76）

## 最终需求

用户截图中「≡ ×N」按钮遮住血量数字，要求放到完整血条右端外侧的空位。用户随后明确：按钮一直显示即可。因此保留原有显示条件，不增加关闭列表后 1 秒隐藏的逻辑，也不改变点击开关列表、滚轮、悬停提亮等行为。

## 原因与修改思路

原 `StatusButton.Attach` 把内层 `%HealthBar`（`NHealthBar` 根节点）作为定位参照，用它的位置和尺寸计算右端。但可见血条的实际宽度由其内部 `%HpBarContainer` 管理；`NHealthBar.UpdateLayoutForCreatureBounds` 和 `UpdateWidthRelativeToReferenceValue` 调整的都是该容器，根节点尺寸不能代表血条轮廓。由此按钮可能落在血条和血量文字上。

修改位于 `mod/src/Scenes/TipList.cs`：

1. 挂载时继续把按钮放在原状态显示节点下，但另取 `%HpBarContainer` 作为定位参照。
2. 每帧读取完整容器的 `GetGlobalRect()`，取其右端和垂直中心。避免使用当前 HP 填充条，受伤不会让按钮向左挤进血条。
3. 按钮左端放到血条右端外 24 个界面单位，垂直居中；间距和按钮高度乘父节点的全局缩放，保持两者在缩放下的相对关系。
4. 该间距为现有悬停放大、提示弹动和阴影预留空间；按钮保持血条同一行，不占下方状态图标的位置。
5. 不改状态数量统计、列表内容、开关、战斗判定、其他角色行为，不改版本清单或 PCK 素材。

## 操作与验证

- 开始时确认无已跟踪文件改动；fetch 后普通快进检查结果为 Already up to date，基线 `2f1af6c`。
- 原有三份未跟踪素材/验收目录均原样保留，不纳入提交。
- 只读查看本机 0.111.0 / 0.107.1 的 `NHealthBar`，两者都使用 `%HpBarContainer`，确认没有新增版本专属成员依赖。
- Release 构建：0 警告、0 错误。
- ApiCheck：0.111.0 和 0.107.1 均检查 1059 处引用，均输出「结果：全部能找到 ✓」。
- `git diff --check` 通过。
- 确认游戏已退出后，备份两个游戏原来的 DLL，再覆盖新 DLL（928256 字节），逐一校验源文件与安装文件 SHA-256 一致。未重打 PCK，音乐盒和测试工具未改。
- 未启动游戏，未使用 Computer Use；截图所示位置的实际观感仍由用户进游戏复验，兼容检查不等于实机验证。

安装位置：

- 0.111.0：`C:/Users/kkk/Desktop/slaythespire/Slay the Spire 2/mods/P3Makoto/P3Makoto.dll`
- 0.107.1：`C:/Users/kkk/Downloads/Slay the Spire 2/mods/P3Makoto/P3Makoto/P3Makoto.dll`
- 旧 DLL 备份：`mod/_build/backup_status_button_20261018_065039_111/` 和 `mod/_build/backup_status_button_20261018_065039_107/`（均不提交）。

## 用户复验

1. 储君战斗中有状态时，按钮应持续显示在血条右侧，与血量文字和状态图标分开。
2. 悬停按钮、悬停角色触发提示弹动，边缘不应再盖住血条。
3. 点击打开/关闭状态列表、在按钮上滚动列表，原有功能应保持正常。
4. 受伤、回血、格挡变化、不同窗口大小或界面缩放后，按钮应继续跟随完整血条右端。
