# P5R Joker 总攻击调查（2026-11-01）

本轮只读本机原版资源，未修改 mod 或游戏。旧任务文档仅作为线索，没有执行其中的历史任务指令。

## 确认到的原版组织
- BATTLE/EVENT/BCD/GOODBYE/BKSK_HERO.BCD（651 字节）直接含 bksk_hero_cte.EPL 与 bksk_splatter.EPL 引用。
- BKSK_HERO_CTE.EPL（356750 字节）中找到 camera、back_hero、front_hero、back_matte_up/down、fronttext_hero 等 GFS 名称。这证明收尾包含镜头、前后景和文字/遮罩层，并非一张完整截图。各层具体播放时间仍未解码。
- BKSK_ATTACK_CTE.EPL 中有 bokosuka_matte_attack.GFS、bksk_attack_kira.GMD、bksk_attack_do.EPL 与剪影遮罩。
- BKSK_JUMP_CTE.EPL 中有 glassBr_anim、black_jmp_bksk 贴图；BKSK_JUMP_SMOKE / BKSK_SPLATTER 有烟雾和喷溅。
- BASE.csv 还列出 BKSK_ATTACK.BCD、BKSK_JUMP.BCD 及各角色 BCD；本地 GOODBYE 目录尚未全部补解，不能声称已经拿到全部演出。

## 本轮导出
inventory.json：14 个已提取文件的原路径、大小、ASCII 引用、DDS 导出清单。
textures/：58 张内嵌 DDS 的 PNG；含重复资源、双子版本与模型贴图，不能称为 58 张 Joker 专属资源。
contact_sheet.png：贴图总览。
joker_finish.png：BKSK_P_BC0001.DDS 原尺寸 1024×1024 肖像。旧文档把它当作完整总攻击收尾图并不准确。
hero_graphic_on_red.png：仅检查纹理用的合成图，不是原版演出截图。

## 移植判断
1. 纹理/遮罩可原样输入 Godot ShaderMaterial；按原版材质规则处理通道、色彩、透明度、UV 与重复，不能一律当普通透明 PNG 使用。
2. 收尾镜头、图层变换和材质动画需要解析嵌套 GFS/GMD/动画包，再转成 Godot 数据播放；此前眼神特写的网格/材质关键帧播放器可作技术基础，但不表示现有播放器能直接读取整个 EPL。
3. 开场冲刺与 Joker 收尾姿势若使用角色模型，要继续追踪 BCD 的角色动作及相机绑定。当前没有确认专用 Joker 动作编号，也没有导出其动画。
4. Godot 不能直接执行 P5R BCD/EPL 或原版脚本；最终需适配事件、材质和镜头。优先复用原始数据，在 Godot 实时重放。
5. 音效、语音候选尚未与总攻击事件绑定，不能用未确认的 cue 冒充原版对应台词。
6. 当前未取得精确时间轴、总攻击专用角色动画或完整成品演出。建议下一步先解析 BKSK_HERO_CTE 的六组 GFS 层，制作收尾单段 Godot 预览，再追踪开场和群殴段。

## 第二轮组装试验
- 成功读取六个嵌套 GFS：camera、back_hero、front_hero、matte_up、matte_down、fronttext_hero。各层网格、材质、节点 PRS 关键帧已导出到 decoded/layer_*.json。
- 通用 Resource.Load 把 EPL 识别成 ModelPack，返回空对象。试验只在内存副本中将资源类型改为 Epl，成功读取外层 4 秒动画、层级变换与 EplModel/EplCamera/EplPostEffect 节点。原版文件未修改。
- outer_decoded/BKSK_HERO_CTE.json 保存外层解码；nested 保存原始 GFS 片段。
- godot/capture.gd 在 Godot 4.5.1 中重建原版网格、材质、UV 矩阵、位置缩放和四元数动画，应用压缩轨道 PositionScale/ScaleScale，并复用原版镜头轨道及 26.231289 度 FOV。
- 仍是技术试验：无名字 TargetId 的外层控制器绑定、启停可见性、后处理、相机 Single_8 语义未全部实现。文字二维投影用了临时坐标适配，不能声称原版布局已还原。需要原版录像逐帧校准。
- 人物来自原版分层网格，无重新生图。预览没有音效、前半段群殴或游戏逻辑接入。
- 未修改已安装 mod；当前仓库由其他工作切回 claude 分支，本轮试验独立保存，没有覆盖或切换分支。

## 第三轮：白块与时间配置修正
- 找到 EplAnimation.Controllers 中的 Field0C，按外层节点先序索引关联 ControllerIndex；PRSByte 的初始位置与对应节点 bind 位置独立比对通过。
- 两个 matte 底板此前未应用外层 Single4Byte RGB 控制：node 7 为 (0.99607843,0,0)，node 6 为 (0,0,0)。本轮接入原始颜色控制，不再显示默认白色。
- 接入原始启停窗口、SingleByte 不透明度关键帧，并按事件启用时间计算各内嵌 GFS 的局部时钟。文字 node 3 从 2.1333334 秒（30fps 第64帧）启用；背景 node 10 从 2.2 秒（第66帧）启用；人物 alpha 从第65帧的0过渡到第66帧的1。
- 原版外层 Duration=4 秒，Field04=30；结束值3.9666667是第119帧，预览按包含最后一个原生帧解释，在4秒结束。输出60fps用于采样，没有额外加速。
- 各嵌套轨道 Speed 未写入（null），使用名义1倍时间；保留关键帧秒数和压缩轨道缩放。EplModel 的未知字段不冒认速度参数。
- 镜头 near=10、far=4000、FOV=26.231289 均取原始相机配置。
- 仍有未还原项：二维文字的临时90度坐标适配/偏移、Camera Single_8 含义、Type22部分标志与后处理；前半段由BCD驱动的3D角色动作不在此EPL单独预览内。所以不宣称已达到原版逐帧一致。
- v2 预览只截取2.0秒起的收尾段，保持原速；另存完整4秒技术预览。未改 mod 或已有攻击特效。

## 第四轮诊断：人物定格、剪影运动与黑底板
- front_hero 的 root01 轨道在 0–0.33333334 秒绕 Y 转动，0.33333334–4.6666665 秒保持同一旋转与位置；shadow_Point 则在 2.2666667–4.6666665 秒继续移动。前一版截取 2.0–4.0 秒，且人物此前被外层 alpha 隐藏，因此用户看不到前段转动。这是已确认的源轨道行为，不代表整段原版角色本体动作已接入。
- 明确定位底部黑块为 layer_04 / back_matte_down / 外层节点6。进行同一时刻3秒的隔离渲染，仅在诊断开关下隐藏这一层，黑块消失；人物和剪影不变，确认来源。单独恢复原材质背面剔除没有解决黑块。
- 这证实上轮只应用 RGB / 时间并不足以完成底板与最终画面的合成。底板的显示范围、坐标/遮挡规则及原版角色本体/二维收尾交接仍未完整还原；不能把诊断隐藏底板冒充原版正确实现。
- 诊断图：godot/diag_cull/f00000001.png（原材质剔除）与 godot/diag_matte/f00000001.png（隔离隐藏底板）。capture.gd 默认仍保留底板，--diagnostic-hide-matte 仅供排查。

## 第五轮核对：左侧文字与黑块边缘
- 用户指出左侧文字后，重新核对完整 fronttext_hero：15个网格、19个节点控制器、4张原版材质贴图，字样为 THE SHOW'S OVER。文字原本沿左侧竖向排布；此前临时90度旋转并移到底部是错误适配，已撤回，恢复原始二维图层变换。
- 对照资料为 Persona Central 转引的 ATLUS 2016年原版P5视频画面（不是P5R逐帧基准）：https://personacentral.com/wp-content/uploads/2016/08/Hero-AoA-Header-696x392.jpg 。画面可见竖排文字和左下黑色斜形区域。因此前面“黑块一定是异常遮罩”的结论过早，应以边界、位置与镜头一起判断。
- 用户随后圈出的矩形黑区域确实来自下底板。相同3秒时刻下测试恢复原材质背面剔除，黑块仍在；隔离隐藏底板可确认来源，但这不是最终修复。
- 进一步找到尚未应用的 Camera / Single_8 单值轨道，数值从-4.5108953变化至10.082855，2.0667–2.1667秒快速变化。将此轨道作为相机bank角（度）转换到Godot光轴方向后，底板上沿从水平变成斜向，人物/背景整体倾角也更接近对照图。当前以这一解释做v4试验，尚无原引擎执行代码确认其完整语义，不能宣称已逐帧一致。
- diag_bank/f00000001.png 是3秒同一时刻的对比结果；v4渲染启用 --native-cull --camera-bank，不隐藏底板。完整4秒与后半段原速GIF分别保存。
- 左侧字体的二维投影尺度、后处理与原版3D角色动作交接仍待P5R实机录像验证。没有改动已安装mod。

## 第六轮：按用户要求关闭下底板（v5）
- 用户再次指出黑色区域仍在，说明仅恢复斜向镜头并未满足视觉要求。
- 新增 --hide-lower-matte 合成选项，关闭 layer_04/back_matte_down，露出后方原版红色背景与图案；原始文件和解码数据不删除、不重绘。
- 这是用户指定的版式调整，不能标称原版逐帧一致。其余原版节点动画、颜色、启停时刻、竖排文字及v4相机bank试验保持相同。
- v5命令：capture.gd --native-cull --camera-bank --hide-lower-matte；输出 frames_v5 与 allout_assembly_v5.gif。

## 第七轮：节点绑定与矩阵计算（v6）
- 以 TargetId 数字索引绑定，并逐条检查 TargetName 一致性：84条绑定通过。原预览仅按名称绑定，本资源的名称恰好匹配，但现在保留可验证的索引关系。
- 查到确定的坐标转换差异：GFDLibrary.Models.Node.LocalTransform 为 QuaternionRotation * Scale（System.Numerics行向量），而先设置Godot quaternion再设置scale会按不同顺序合成。现使用System.Numerics按源顺序计算每帧局部矩阵，转置传给Godot，保持完整父子层级。
- 已导出6层、每层240帧的节点变换数据（动画数据，不是烘焙视频），Godot仍实时渲染原始网格和材质。88个bind矩阵转换校验通过，Godot中5个采样时刻共440次世界矩阵比较通过，最大误差约0.0001094原始坐标单位。
- 相机不再用look_at重建朝向，改为使用原版相机节点的完整旋转矩阵；保留此前按Single_8轨道解释的bank控制。Single_8完整语义仍未从原引擎确认，不宣称逐帧复刻完成。
- 纠正此前关于人物入场动作的解释：root01（索引35）是无子网格的空节点。真正人物主网格在root（索引1）下，该root控制器只有一个常量PRS关键帧；shadow_Point（索引32）则有72个运动关键帧。不能把root01的0.33秒旋转当成人物本体动作。详细见motion_binding_audit.json。
- 这份收尾EPL的人物与剪影确有不同源轨道；本轮没有伪造主图位移或强制让剪影跟随主图。BCD驱动的原版3D角色本体动作仍未接入。
- 用户要求移除的下底板保持关闭。v6仍是收尾技术预览，不是完整总攻击，也未安装至mod。
