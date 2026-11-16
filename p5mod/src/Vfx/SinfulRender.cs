using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>Keep cinematic lighting off unrelated room/UI shaders and bound the cloudy sky's fill cost.</summary>
internal static class SinfulRender
{
	internal const int ActorLightMask = 1 << 19;
	internal static void Observe(Node lifetime)
	{
		var tree = lifetime.GetTree();
		var rid = lifetime.GetViewport().GetViewportRid();
		var samples = new List<double>();
		ulong last = Time.GetTicksUsec();
		double worstFrame = 0;
		void Tick()
		{
			ulong now = Time.GetTicksUsec();
			worstFrame = System.Math.Max(worstFrame, (now - last) / 1000.0);
			last = now;
			samples.Add(RenderingServer.ViewportGetMeasuredRenderTimeGpu(rid));
		}
		tree.ProcessFrame += Tick;
		lifetime.TreeExiting += () =>
		{
			tree.ProcessFrame -= Tick;
			if (samples.Count == 0) return;
			samples.Sort();
			GD.Print($"[P5Joker] finale render-budget-v1 frames={samples.Count} gpu_median={samples[samples.Count / 2]:0.0}ms gpu_p95={samples[(int)((samples.Count - 1) * 0.95)]:0.0}ms worst_frame={worstFrame:0.0}ms");
		};
	}

	internal static void LightActors(Node lifetime, IEnumerable<CanvasItem> actors)
	{
		var saved = new Dictionary<CanvasItem, int>();
		void Visit(Node node)
		{
			if (node is CanvasItem item && !saved.ContainsKey(item))
			{
				saved.Add(item, item.LightMask);
				item.LightMask |= ActorLightMask;
			}
			foreach (Node child in node.GetChildren()) Visit(child);
		}
		foreach (var actor in actors)
			if (GodotObject.IsInstanceValid(actor)) Visit(actor);
		lifetime.TreeExiting += () =>
		{
			foreach (var pair in saved)
				if (GodotObject.IsInstanceValid(pair.Key)) pair.Key.LightMask = pair.Value;
		};
	}

	// Sky only: no SCREEN_UV, fine character edges or external light dependence.
	internal static ColorRect Sky(Node parent, Vector2 screen, ShaderMaterial material)
	{
		float scale = Mathf.Min(1f, Mathf.Min(1280f / screen.X, 720f / screen.Y));
		var size = new Vector2I(Mathf.Max(1, Mathf.RoundToInt(screen.X * scale)), Mathf.Max(1, Mathf.RoundToInt(screen.Y * scale)));
		var viewport = new SubViewport
		{
			Name = "P5SinfulSkyBuffer", Size = size, TransparentBg = true,
			Disable3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
		};
		parent.AddChild(viewport);
		var source = new ColorRect { Size = size, Material = material, LightMask = 0, MouseFilter = Control.MouseFilterEnum.Ignore };
		viewport.AddChild(source);
		parent.AddChild(new TextureRect
		{
			Name = "P5SinfulSky", Size = screen, Texture = viewport.GetTexture(), LightMask = 0,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		});
		return source;
	}
}
