# 伟大封印手臂零件：不合格批次报告

日期：2026-10-14。工具：内置 imagegen。

## 结论

本批次不过关，不能用于骨骼动画。没有合格的 arm_point_mid.png 或 arm_point_up.png，也没有完成 preview_point.png。尚未提交任何新美术资源。按用户后续要求，两张最终零件应使用真正的 alpha 透明背景，取代最初的绿幕要求。

## 实际尝试及问题

过渡帧共尝试四轮（首轮生成、三次重画请求，其中最后一次被安全系统拒绝而未产生图片）。指天帧尚未开始，不能判断其生成质量。

1. 第一轮：手掌张开、只有一条手臂，但肩部顶端目测约 (354,320)，相对目标 (485,295) 偏差约 133 像素；手臂偏大。背景为绿幕，存在轻微纹理，未证明逐像素为纯 #00FF00。
2. 第二轮：缩小和定位修正后，肩部顶端目测约 (431,345)，偏差约 74 像素，仍超过 25 像素上限；掌心目测约 (650,185)，指尖约 (685,123)。背景仍为绿幕。
3. 第三轮：用户要求改为真透明底。请求保留姿势并向右平移 54 像素、向上平移 50 像素，但输出没有执行准确的平移。肩部顶端目测约 (438,398)，偏差约 113 像素；指尖目测约 (782,98)，掌心约 (736,190)，均明显偏离要求。手臂和手周围出现红色及白色半透明光晕。文件经 Pillow 检查为 RGBA、1024×1536，alpha 范围 0–254：确有透明通道，但并不意味着轮廓以外干净透明。
4. 第四轮：重新要求精确位置、真实透明、无光晕。imagegen 在输出审核阶段拒绝，返回 HTTP 400 / moderation_blocked，标记 safety_violations=[abuse]，categories=[other]。请求 ID：49018521-e305-423e-84f6-29e28e807214。没有产生可检查图片；没有尝试绕过拒绝。

以上坐标均为基于完整尺寸显示的目测估计，不是轮廓定位算法的精确测量；精度约数像素至十余像素。未对失败图片做离线修图或裁切。

## 可能原因（推测，非已确认事实）

- 图像生成对像素坐标和平移指令的执行不够稳定；精确位置要求没有成为硬约束。
- 旧手臂参考自身有位置和比例偏差，尽管提示词要求只参考其风格，仍可能影响生成布局；第三轮以失败图为编辑参考，也可能延续偏差。
- 请求真透明时，模型仍可能生成半透明照明效果。alpha 通道存在与背景完全干净是两项不同的检查。
- 第四轮提示词包含 “severed shoulder sleeve attachment”。severed 有“切断、截断”的含义，用在肩部和手臂描述中，可能使审核把无血腥的骨骼动画分件误判为断肢。这是本次提示词的措辞问题，也是可能原因之一；并非系统已确认的原因。服务只给出 abuse / other 宽泛分类，也可能与生成画面有关，不能断言该词是唯一或实际触发因素。没有尝试绕过拒绝。

## 最终尝试提示词（第四轮，未生成成功）

```text
Create an isolated 2D anime game skeletal animation sprite using the provided Makoto master as STYLE reference only. EXACT 1024x1536 canvas, TRUE transparent background with alpha channel, alpha ZERO on every pixel outside the sprite, no black backdrop, no white backdrop, no checkerboard drawing, no glow or shadow or ambient lighting effects. ONLY ONE right arm from severed shoulder sleeve attachment to fingertips, no torso/head/body/props. Black tailored suit sleeve, plain red upper arm band with black border stripes NO LETTERS, white shirt cuff, pale slender male hand. Draw arm diagonally upward right at 45 degrees, slight elbow bend. Shoulder sleeve attachment top precisely (485,295), attachment bottom near (515,365), elbow near (570,245), wrist near (625,165), palm centered (640,130), finger tips near (665,85). Open palm faces viewer with four fingers held together, thumb alongside. Subject must occupy ONLY x480..700 y70..365, all remaining canvas fully transparent. Shoulder-to-fingertips length around 300px, arm width 55px, palm width 35px. No texture outside sprite. Precise position crucial.
```

## 仓库操作记录

仓库：C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload。
分支：claude/amazing-darwin-b00arn。

最初工作区干净，目标分支完成 fetch 和同步。经用户授权增加指定稀疏检出目录；出现路径过长错误后，经用户再次授权用仅对单次命令生效的 core.longpaths=true 补齐，随后状态恢复干净。

准备推送本报告时，重新检查发现 art/p3r_tex/ 下多个未跟踪图片和目录，以及 art/sfx_p3r_c4/ 未跟踪目录。它们不是本聊天生成的文件；没有读取内容、加入暂存区或改动它们。当时按原任务“有未提交改动就停下报告”的规则暂停。用户随后明确要求推送该可能原因，本次仅提交 art/parts_src/REPORT_POINT.md，其他未跟踪文件保持原状。

明确说明：尚未尝试指天帧，安全拒绝针对过渡帧第四轮，不能据此说指天手势本身被禁止。

