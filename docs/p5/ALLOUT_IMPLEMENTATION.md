# Joker 总攻击：资源、组装和实现路径

这是v6收尾技术试作，尚不是完整总攻击成品，没有安装到mod。详细调查沿革见 tools/p5/allout_native/RESEARCH_LOG.md；以该文件最后一轮结果为准，早期结论有已纠正的错误。

## 原版资源及层级

来源：P5R BASE.CPK / BATTLE/EVENT/BCD/GOODBYE/。
source目录保留BKSK_HERO.BCD、BKSK_HERO_CTE.EPL、BKSK_SPLATTER.EPL原始文件。

- BKSK_HERO.BCD：演出入口，引用收尾及喷溅EPL；操作码和3D角色调用尚未完整实现。
- BKSK_HERO_CTE.EPL：外层4秒时间轴及六个嵌套GFS。
- BKSK_SPLATTER.EPL：喷溅、烟雾过渡；尚未接入播放。

|nested编号|原始GFS（省略be0cmn_bokosuka_前缀）|外层节点|职责|
|---|---|---|---|
|00|camera|11|镜头|
|01|back_hero|10|星形、网点、碎片背景|
|02|front_hero|9|Joker分层人物和独立剪影|
|03|back_matte_up|7|上底板|
|04|back_matte_down|6|下底板，用户要求关闭|
|05|fronttext_hero|3|左侧竖排THE SHOW'S OVER|

人物是原版网格及材质，不是方形肖像。文字由15个网格、19个节点控制器、4张原始纹理组成，不能重新打字替代。godot/tex保留原名PNG；decoded/layer_*.json的Materials/DiffuseMap记录每张纹理绑定。

## 数据布局

工具全部位于 tools/p5/allout_native：
- source：原始BCD/EPL；nested：拆出的六个GFS。
- decoded：各层完整节点、网格、材质、轨道；outer_decoded：外层EPL结构。
- godot/layers.json：六层解码数据合集。
- godot/placements.json：外层世界矩阵，按上表排列。
- godot/timeline.json：外层启停、颜色、透明度轨道。
- godot/native_matrices.json：按原始运算生成的240帧节点变换数据，不是烘焙视频；Godot仍实时绘制网格。
- bake：生成矩阵与验证的C#工具。
- motion_binding_audit.json：人物、空节点、剪影绑定证据。
- godot/capture.gd：独立Godot播放器。

## 具体如何拼装

1. 原版EPL的通用Resource.Load会误读为ModelPack空对象。研究程序仅在内存副本中，把文件头偏移8处资源类型改成ResourceType.Epl，再交GFDLibrary读取；不修改原始文件。导出结果已附带，无需重新解码即可复现。
2. 外层树先序编号；EplAnimation.Controllers.Field0C是当前使用的节点索引，ControllerIndex关联轨道。外层节点名称为空，不能按名称猜绑定。
3. 按placements创建六个holder，再递归创建各GFS内部节点。内部轨道用TargetId绑定，并核对TargetName。
4. 按原顶点、UV、材质创建ArrayMesh与MeshInstance3D，三角形转为A,C,B绕序。读取DiffuseColor、透明度、纹理、原始背面剔除。TextureMap的Field4C/50/5C/60/7C/80转换UV矩阵，Flags位1启用。
5. 保留父子层级。GFD的System.Numerics行向量运算为Local=Rotation*Scale，再写Translation；World=Local*ParentWorld。转Godot需转置，父矩阵在左。直接依次设置Godot quaternion和scale会交换非均匀缩放次序，因此现使用原始矩阵计算。
6. 压缩PRS位置乘PositionScale，缩放乘ScaleScale；位置/缩放线性插值，旋转四元数球面插值；Speed缺省按名义1倍。bake工具按System.Numerics计算，附世界矩阵采样供Godot比较。
7. 外层Single4Byte按RGB调制，SingleByte按透明度解释。两片底板RGB分别红、黑。该解释已有视觉及数值验证，但不代表所有未知字段都确认。
8. 相机使用完整世界变换，near=10、far=4000、FOV=26.231289。Single_8当前解释为bank角并转Godot光轴旋转，对照画面支持，但尚未从原引擎确认完整语义。
9. 文字放透明SubViewport，正交相机size=720，保留原始竖排方向。之前旋转成横排放到底部是错误，已撤回；二维投影尺度仍待验收。
10. 文字在原生30fps第64帧（2.1333334秒）启用；背景第66帧（2.2秒）启用。Duration=4秒，控制器end=119/30按包含最后原生帧解释。60fps输出仅提高采样，没有额外加速。
11. --hide-lower-matte关闭下底板，露出原版红背景，属于用户指定版式调整，不是原版逐帧一致版本。

## 已验证的绑定与运动

84条内部节点绑定、88个bind矩阵转换通过。Godot在5个时间点共440次世界矩阵比较通过，最大误差约0.00011原始单位。

真正人物主网格在索引1的root下，该控制器只有一个常量PRS关键帧；shadow_Point（索引32）有72个独立运动关键帧。root01（索引35）虽有0.33秒转动，但没有子网格，是空节点。此前把该轨道解释成人物入场是错误，不能为满足同向运动而编造人物位移。

## 如何复现

依赖标准Godot 4.5.1；重新生成数据另需.NET 10和GFDStudio 0.10.3的GFDLibrary.dll。bake.csproj里的GFDLibrary路径可按机器调整。

在PowerShell、仓库根目录运行：
    $demo = (Resolve-Path tools/p5/allout_native).Path
    dotnet build "$demo/bake/bake.csproj"
    dotnet "$demo/bake/bin/Debug/net10.0/bake.dll" $demo
    New-Item -ItemType Directory -Force "$demo/godot/frames"
    & '<Godot console exe>' --path "$demo/godot" --script capture.gd --rendering-method gl_compatibility --resolution 1280x720 --write-movie frames/f.png --fixed-fps 60 --quit-after 240 -- --native-cull --camera-bank --hide-lower-matte

附带数据可跳过dotnet两步，直接用Godot播放。预览见 docs/p5/preview/allout/assembly_v6.gif；GIF截取后2秒，保持原速。

## 完整实现还需完成什么

1. 继续解码BKSK_HERO.BCD的角色动作/对象调用，定位对应3D Joker动作，补齐角色本体到二维定格的交接。
2. 接入队伍切入和群殴段：BKSK_JUMP_CTE、BKSK_ATTACK_CTE为已找到候选，不能仅凭名字确定全部流程。
3. 还原Type22全部字段、Single_8确切语义、后处理与喷溅，验证外层时钟/alpha等当前解释。
4. 对照P5R同版本画面逐帧验收。2016年P5宣传图只能辅助核对版式，不能作Royal逐帧基准。
5. 将资源与数据打入PCK，以Godot内建节点、着色器和统一时钟播放；补齐中断、释放、前后景层、卡牌/斩杀触发。保留现有打击、枪击、刀扇、毒。
6. 校准音效、语音、伤害时机和性能后再编译安装。当前目录没有完整可安装功能，不能把试作GIF直接当游戏特效。
