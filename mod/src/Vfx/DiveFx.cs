using System;
using Godot;
using P3Makoto.Core;

namespace P3Makoto.Vfx;

/// <summary>原版入水：单帧水花按节奏切换，露出遮罩与缓存波浪独立连续运动。</summary>
internal static class DiveFx
{
	internal static bool Ready => HeroStage.Tex("splash_09.png") != null && HeroStage.Tex("mask_04.png") != null && HeroStage.Tex("mask_blank.png") != null && HeroStage.Warp != null;

	private static void Bind(ShaderMaterial mat, int index, Vector2 view)
	{
		Texture2D tex = HeroStage.Tex($"splash_{Math.Min(index, 9):00}.png")!;
		mat.SetShaderParameter("frame_tex", tex);
		// SPR_UI_Camp：十张统一 960×948；原生缩放 (2,1.1392)、中心 (960,540)。
		Vector2 size = new(tex.GetWidth() * 2f * view.X / 1920f, tex.GetHeight() * 1.1392f * view.Y / 1080f);
		Vector2 pos = (view - size) / 2f;
		mat.SetShaderParameter("frame_map", new Vector4(view.X / size.X, view.Y / size.Y, -pos.X / size.X, -pos.Y / size.Y));
	}
	private static ShaderMaterial SplashMaterial(Vector2 view, int index)
	{
		ShaderMaterial mat = new() { Shader = Shaders.PileSplash };
		Bind(mat, index, view);
		return mat;
	}

	/// <summary>空闲时实际取样全部水花、遮罩及波浪缓存，预热绘制路径。</summary>
	internal static void Warm(Node parent)
	{
		if (!Ready) return;
		for (int a = 0; a < DiveTimeline.SplashFrames; a++)
		{
			ShaderMaterial splash = SplashMaterial(new Vector2(1920, 1080), a);
			parent.AddChild(new ColorRect { Size = new Vector2(1000, 1000), Material = splash, MouseFilter = Control.MouseFilterEnum.Ignore });
			if ((a & 1) != 0) continue;
			Texture2D mask = HeroStage.Tex($"mask_{a / 2:00}.png")!;
			ShaderMaterial bg = new() { Shader = Shaders.CommuBg };
			bg.SetShaderParameter("reveal", 0f);
			bg.SetShaderParameter("mask_tex", mask);
			bg.SetShaderParameter("mask_next", mask);
			bg.SetShaderParameter("mask_mix", 0f);
			bg.SetShaderParameter("warp_tex", HeroStage.Warp!);
			bg.SetShaderParameter("warp_on", 1f);
			bg.SetShaderParameter("warp_frame", a * 7f + 0.5f);
			parent.AddChild(new ColorRect { Size = new Vector2(1000, 1000), Material = bg, MouseFilter = Control.MouseFilterEnum.Ignore });
		}
	}

	/// <summary>转场结束或提前关闭时解绑回调；不在播放中创建 / 上传波浪图。</summary>
	internal static void Start(Control root, ShaderMaterial bg, Vector2 view)
	{
		if (!Ready || root.GetTree() is not SceneTree tree) return;
		ShaderMaterial splashMat = SplashMaterial(view, 0);
		ColorRect splash = new() { Size = view, Material = splashMat, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(splash);
		bg.SetShaderParameter("reveal", 0f);
		bg.SetShaderParameter("warp_tex", HeroStage.Warp!);
		HeroStage.SetWarp(HeroStage.Warp, 0f);
		ulong t0 = Time.GetTicksUsec(), previous = t0;
		int lastSplash = -1, lastMask = -2, frames = 0, slowFrames = 0;
		double totalCpu = 0, maxCpu = 0, maxGap = 0;
		bool done = false, completed = false;
		void Tick()
		{
			if (done) return;
			if (!GodotObject.IsInstanceValid(root) || !GodotObject.IsInstanceValid(splash)) { Stop(); return; }
			ulong begin = Time.GetTicksUsec();
			double t = (begin - t0) / 1_000_000.0;
			if (frames > 0)
			{
				double gap = (begin - previous) / 1000.0;
				maxGap = Math.Max(maxGap, gap);
				if (gap > 33.4) slowFrames++;
			}
			previous = begin;
			DiveTimeline.Frame state = DiveTimeline.At(t);
			int si = state.Splash;
			splash.Visible = si < DiveTimeline.SplashFrames;
			if (si < DiveTimeline.SplashFrames)
			{
				if (si != lastSplash)
				{
					lastSplash = si;
					Bind(splashMat, si, view);
				}
				splashMat.SetShaderParameter("fade", state.Alpha);
			}
			if (state.Mask != lastMask)
			{
				lastMask = state.Mask;
				Texture2D mask = HeroStage.Tex(state.Mask < 0 ? "mask_blank.png" : $"mask_{state.Mask:00}.png")!;
				bg.SetShaderParameter("mask_tex", mask);
				bg.SetShaderParameter("mask_next", mask);
				bg.SetShaderParameter("mask_mix", 0f);
				HeroStage.SetEntryMask(mask, state.Reveal);
			}
			bg.SetShaderParameter("reveal", state.Reveal);
			HeroStage.SetEntryMask(null, state.Reveal);
			// 原版 In 末帧仍有静态弯曲；接 Loop_L，同一个基准网格继续动，不能在这里归零。
			bg.SetShaderParameter("warp_frame", state.Warp);
			bg.SetShaderParameter("warp_on", 1f);
			HeroStage.SetWarp(null, 1f, state.Warp);
			double cpu = (Time.GetTicksUsec() - begin) / 1000.0;
			totalCpu += cpu;
			maxCpu = Math.Max(maxCpu, cpu);
			frames++;
			if (!completed && t >= 2.5) { completed = true; Report(); }
		}
		void Report() => GD.Print($"{ModEntry.Tag} perf dive frames={frames} tick_avg={totalCpu / Math.Max(1, frames):0.000}ms tick_max={maxCpu:0.000}ms frame_max={maxGap:0.0}ms slow33={slowFrames} completed={completed} loop=original210 splash=native30tick24 masks=original5");
		void Stop()
		{
			if (done) return;
			done = true;
			tree.ProcessFrame -= Tick;
			if (GodotObject.IsInstanceValid(root)) root.TreeExiting -= Stop;
			if (GodotObject.IsInstanceValid(splash)) splash.QueueFree();
			if (GodotObject.IsInstanceValid(bg)) { bg.SetShaderParameter("warp_on", 0f); bg.SetShaderParameter("reveal", 1f); }
			HeroStage.SetWarp(null, 0f);
			HeroStage.SetEntryMask(null, 1f);
			if (!completed) Report();
		}
		tree.ProcessFrame += Tick;
		root.TreeExiting += Stop;
		Tick();
	}
}
