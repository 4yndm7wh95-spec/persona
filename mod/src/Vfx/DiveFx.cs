using System;
using Godot;

namespace P3Makoto.Vfx;

/// <summary>
/// v0.14.95 原版营地主菜单的"倒入水"转场（用户确认过离线预览 tools/p3r/dive_preview.py）：
/// 1) 原版水花 10 帧盖在最上层；2) 水下画面先只在水花轮廓里露出，约 0.3s 铺满（CommuBg 的 mask_tex / reveal）；
/// 3) 原版 UIM_UI_Camp_In 网格波浪 75 帧（30fps）同时扭水下背景和人物，播完 0.3s 内收掉；人物由 HeroStage（Act.Top）演。
/// 时间轴相对原版录屏整体提前 0.40s（原版按键后有一段停顿，我们点开就开始）。
/// </summary>
internal static class DiveFx
{
	private const float SplashStep = 0.053f;   // 原版录屏里水花约 1.6 帧（30fps）换一张
	private const float Full = 0.30f;          // 整屏变成水下
	private const float WarpFps = 30f, WarpFrames = 75f, WarpFade = 0.3f;

	internal static bool Ready => HeroStage.Tex("splash_00.png") != null && HeroStage.Tex("mask_00.png") != null && HeroStage.Warp is { Length: > 0 };

	/// <summary>在 root 最上层加水花；bg 是牌堆背景（Shaders.CommuBg）的材质。转场结束后自己停。</summary>
	internal static void Start(Control root, ShaderMaterial bg, Vector2 view)
	{
		if (!Ready || root.GetTree() is not SceneTree tree) return;
		float[] warp = HeroStage.Warp!;
		Image img = Image.CreateEmpty(17, 10, false, Image.Format.Rgf);
		ImageTexture warpTex = ImageTexture.CreateFromImage(img);
		// 水花摆法和离线预览一致：横向铺满 1.06 倍屏宽、纵向 1.6 倍（按 949 高）、上边缘在 −10% 处、水平居中
		float sx = 1.06f * view.X / 961f, sy = 1.6f * view.Y / 949f;
		TextureRect splash = new() { MouseFilter = Control.MouseFilterEnum.Ignore, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale };
		root.AddChild(splash);
		bg.SetShaderParameter("reveal", 0f);
		bg.SetShaderParameter("warp_tex", warpTex);
		HeroStage.SetWarp(warpTex, 0f);
		ulong t0 = Time.GetTicksUsec();
		int lastWarp = -1, lastSplash = -2;
		bool done = false;
		void Tick()
		{
			if (done) return;
			if (!GodotObject.IsInstanceValid(root) || !GodotObject.IsInstanceValid(splash)) { Stop(); return; }
			float t = (Time.GetTicksUsec() - t0) / 1_000_000f;
			// 水花
			int si = (int)(t / SplashStep);
			if (si != lastSplash)
			{
				lastSplash = si;
				if (si <= 9 && HeroStage.Tex($"splash_{si:00}.png") is Texture2D tex)
				{
					splash.Texture = tex;
					splash.Size = new Vector2(tex.GetWidth() * sx, tex.GetHeight() * sy);
					splash.Position = new Vector2((view.X - splash.Size.X) / 2f, -0.10f * view.Y);
					splash.Modulate = new Color(1f, 1f, 1f, si < 7 ? 1f : 1f - (si - 6) / 4f);
					if (HeroStage.Tex($"mask_{si:00}.png") is Texture2D mask) bg.SetShaderParameter("mask_tex", mask);
				}
				else splash.Visible = false;
			}
			bg.SetShaderParameter("reveal", t >= Full ? 1f : 0f);
			// 波浪
			int wf = Math.Min((int)(t * WarpFps), (int)WarpFrames - 1);
			if (wf != lastWarp)
			{
				lastWarp = wf;
				byte[] bytes = new byte[17 * 10 * 2 * 4];
				Buffer.BlockCopy(warp, wf * 17 * 10 * 2 * 4, bytes, 0, bytes.Length);
				img.SetData(17, 10, false, Image.Format.Rgf, bytes);
				warpTex.Update(img);
			}
			float end = WarpFrames / WarpFps;
			float on = t < end ? 1f : Mathf.Clamp(1f - (t - end) / WarpFade, 0f, 1f);
			bg.SetShaderParameter("warp_on", on);
			HeroStage.SetWarp(null, on);
			if (t > end + WarpFade) Stop();
		}
		void Stop()
		{
			if (done) return;
			done = true;
			tree.ProcessFrame -= Tick;
			if (GodotObject.IsInstanceValid(splash)) splash.QueueFree();
			if (GodotObject.IsInstanceValid(bg)) { bg.SetShaderParameter("warp_on", 0f); bg.SetShaderParameter("reveal", 1f); }
			HeroStage.SetWarp(null, 0f);
		}
		tree.ProcessFrame += Tick;
		root.TreeExiting += Stop;
		Tick();
	}
}
