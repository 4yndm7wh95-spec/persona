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
		// v0.14.97（用户：入水特效帧率有点低）：原版水花只有 10 帧（约 19fps），相邻两帧交叉淡化；波浪逐帧之间插值，按屏幕刷新率走
		TextureRect next = new() { MouseFilter = Control.MouseFilterEnum.Ignore, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale };
		root.AddChild(next);
		bg.SetShaderParameter("reveal", 0f);
		bg.SetShaderParameter("warp_tex", warpTex);
		HeroStage.SetWarp(warpTex, 0f);
		ulong t0 = Time.GetTicksUsec();
		int lastSplash = -2;
		float[] frame = new float[17 * 10 * 2];
		byte[] bytes = new byte[17 * 10 * 2 * 4];
		bool done = false;
		void Tick()
		{
			if (done) return;
			if (!GodotObject.IsInstanceValid(root) || !GodotObject.IsInstanceValid(splash) || !GodotObject.IsInstanceValid(next)) { Stop(); return; }
			float t = (Time.GetTicksUsec() - t0) / 1_000_000f;
			// 水花：当前帧 + 下一帧按进度淡入
			float fs = t / SplashStep;
			int si = (int)fs;
			float frac = fs - si;
			float Alpha(int i) => i < 7 ? 1f : Mathf.Max(0f, 1f - (i - 6) / 4f);
			void Place(TextureRect r, int i)
			{
				if (HeroStage.Tex($"splash_{i:00}.png") is not Texture2D tex) { r.Visible = false; return; }
				r.Visible = true;
				r.Texture = tex;
				r.Size = new Vector2(tex.GetWidth() * sx, tex.GetHeight() * sy);
				r.Position = new Vector2((view.X - r.Size.X) / 2f, -0.10f * view.Y);
			}
			if (si != lastSplash)
			{
				lastSplash = si;
				if (si <= 9) Place(splash, si); else splash.Visible = false;
				if (si + 1 <= 9) Place(next, si + 1); else next.Visible = false;
				if (si <= 9 && HeroStage.Tex($"mask_{si:00}.png") is Texture2D mask) bg.SetShaderParameter("mask_tex", mask);
			}
			if (si <= 9) splash.Modulate = new Color(1f, 1f, 1f, Alpha(si) * (si + 1 <= 9 ? 1f - frac : 1f - frac));
			if (si + 1 <= 9) next.Modulate = new Color(1f, 1f, 1f, Alpha(si + 1) * frac);
			bg.SetShaderParameter("reveal", t >= Full ? 1f : 0f);
			// 波浪：相邻两帧线性插值
			float wfF = Mathf.Min(t * WarpFps, WarpFrames - 1);
			int wa = (int)wfF, wb = Math.Min(wa + 1, (int)WarpFrames - 1);
			float wt = wfF - wa;
			const int n = 17 * 10 * 2;
			for (int i = 0; i < n; i++) frame[i] = warp[wa * n + i] * (1f - wt) + warp[wb * n + i] * wt;
			Buffer.BlockCopy(frame, 0, bytes, 0, bytes.Length);
			img.SetData(17, 10, false, Image.Format.Rgf, bytes);
			warpTex.Update(img);
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
			if (GodotObject.IsInstanceValid(next)) next.QueueFree();
			if (GodotObject.IsInstanceValid(bg)) { bg.SetShaderParameter("warp_on", 0f); bg.SetShaderParameter("reveal", 1f); }
			HeroStage.SetWarp(null, 0f);
		}
		tree.ProcessFrame += Tick;
		root.TreeExiting += Stop;
		Tick();
	}
}
