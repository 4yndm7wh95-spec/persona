# 召唤原版动作参考：第1步渲染及机位问题报告

## 已完成

开始前 fetch + merge 成功，快进到 e5f8ecb6。仅使用序号15、16、17，分别为 BB0001_051.GAP#main[14]、main[15]、main[16]，没有使用13、14。小刀及开枪两组按用户反馈记为已认可。

复用开枪的同一无面具模型 `C:/Users/kkk/sts2_tools/p5r_rip/model/joker/fixed2/joker_thief_nomask.glb`。D方案：按同名节点挂动作，只保留rotation和Bip01根节点translation，其余节点translation/scale用模型自身。不挂枪，不新增道具或面具；手扶面具、撕面具指动作语义，当前无面具模型没有可见面具实体。

逐帧PNG为1024×1536、RGBA真透明，每2帧一张：15段11张、16段8张、17段11张，共30张。每段提供联系表及原速GIF，路径为15_sheet.png/15.gif、16_sheet.png/16.gif、17_sheet.png/17.gif。GIF延续gun/pick的灰底320×480预览做法，PNG保持透明；不降速。

动画时间轴采用原GLB时间和先前相同的30fps参考：15段0.6666667秒、16段0.5秒、17段0.6666667秒。GIF受百分之一秒格式精度限制，分别670/500/670毫秒。精确段末采样不在GIF里额外延时。原生游戏FPS仍未另行确认，不能把30fps参考冒称为已确认的游戏原生帧率。

## 相机完全沿用gun/pick

- camera_location: `[909.5668334960938, 423.4912414550781, -5.387542724609375]`
- camera_rotation: `[1.5707963705062866, -4.487446858547628e-08, 2.0188241004943848]`
- ortho_scale: `290.64661372791636`
- camera_direction: `[0.9013032363068559, 0.4331887304891343, 0]`
- selected_percent: `50`
- azimuth_degrees: `25.670095872954956`
- output_horizontal_mirror: `true`

三段全程固定相机，不逐帧追踪、缩放或改角度。50%角为25.670095872954956°，沿用渲染后水平镜像。

## 遇到的问题与暂停点

相机参数与gun/pick逐项一致，但15～17动作自身的原始身体朝向不同，这次结果从背后看到人物，手与脸侧的关系难看清。16、17段的大幅衣摆在这一固定构图里部分超出画面。没有擅自绕人物转相机、旋转模型、缩放改构图，也没有把背面模型强行重绘为正面。

这是用户没有指定如何处理的情况，按“遇到没写到的情况停下来报告”暂停进一步调整及后续生图。当前成果作为机位诊断参考推送，不能宣称已通过用户机位验收或衣摆完整性验收。后续需用户确认是否允许针对召唤动作更改人物相对机位方向/统一构图；确认前不画召唤立绘。

alpha>128主体边界触及画布边缘的帧共 15 张：

- `research/p5r/summon/16_frames/frame_006.png`
- `research/p5r/summon/16_frames/frame_008.png`
- `research/p5r/summon/16_frames/frame_010.png`
- `research/p5r/summon/16_frames/frame_012.png`
- `research/p5r/summon/16_frames/frame_014.png`
- `research/p5r/summon/17_frames/frame_000.png`
- `research/p5r/summon/17_frames/frame_004.png`
- `research/p5r/summon/17_frames/frame_006.png`
- `research/p5r/summon/17_frames/frame_008.png`
- `research/p5r/summon/17_frames/frame_010.png`
- `research/p5r/summon/17_frames/frame_012.png`
- `research/p5r/summon/17_frames/frame_014.png`
- `research/p5r/summon/17_frames/frame_016.png`
- `research/p5r/summon/17_frames/frame_018.png`
- `research/p5r/summon/17_frames/frame_020.png`

## 四张待确认关键帧

| 文件 | 动作 | 帧 | 时间秒 | 选择依据 |
|---|---|---:|---:|---|
| key_mask_hold.png | 15 / main[14] | 0 | 0.0000 | 手扶脸侧，保持段的稳定起始帧 |
| key_rip_mid.png | 16 / main[15] | 2 | 0.0667 | 右手上举到头顶附近 |
| key_rip_open.png | 16 / main[15] | 14 | 0.4667 | 每2帧采样中最接近段末的展臂帧 |
| key_stance.png | 17 / main[16] | 0 | 0.0000 | 张开架势保持段起始帧 |

关键帧直接复制对应透明PNG，没有另改姿势或镜头。key_rip_open取16段采样末帧14（原段末为15，差1帧）；key_contact_sheet.png四张并排供确认。它们都保留上述机位问题，不作定稿。

## 复现与保护

scripts/prepare_summon_refs.py、render_summon_refs.py、package_summon_refs.py收录实际使用脚本。Blender为本机4.5.9，Workbench Flat/Texture/Standard与gun/pick一致。source_clips.json列原动作输入及本机D方案GLB路径；render_records.json列相机和逐帧手部坐标；postprocessed.json列透明度/边界/GIF时长。

art/cards的11个已有修改原样保留，不提交、不stash、不还原。既有guard_a修改与其他无关未跟踪资源原样保留。未打开结城理mod图片、未读obs.json或密码、未启动游戏、未改动游戏原文件。不rebase/reset/强推。推送当前参考与报告后，用ls-remote确认origin包含提交。
