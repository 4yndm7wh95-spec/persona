using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace P3Makoto.Vfx;

/// <summary>缓存的原版 COMMU 舞台；运行时直接读 GLB / PNG / 动作，不依赖导入文件。</summary>
internal static class HeroStage
{
	public const float BaseSpinDegPerSec = 36f;
	public const float KickDeg = 120f;
	public const float KickOmega = 9f;
	public static readonly Color HeroKey = new(0f, 1f, 0f);
	public const float KeyDistanceLow = 0.25f, KeyDistanceHigh = 0.45f;
	private const string Path = "res://p3makoto/hero3d/";
	private static Stage? cached;
	private static bool failed;

	private static bool Ensure(SceneTree tree)
	{
		if (failed) return false;
		if (cached != null) return true;
		Stage stage = new(tree);
		try
		{
			stage.Load();
			cached = stage;
			return true;
		}
		catch (Exception e)
		{
			stage.Dispose();
			Fail(e);
			return false;
		}
	}

	private static void Fail(Exception e)
	{
		if (!failed) GD.PushWarning($"[P3Makoto] hero3d unavailable; using portrait: {e.Message}");
		failed = true;
	}

	internal static bool Open(Node2D parent, Vector2 view, float shift)
	{
		if (!Ensure(parent.GetTree())) return false;
		try
		{
			cached!.Open(parent, view, shift);
			return true;
		}
		catch (Exception e)
		{
			cached!.Close();
			Fail(e);
			return false;
		}
	}

	/// <summary>scope 防止旧牌堆的延迟退场把新牌堆的舞台关掉。</summary>
	internal static void Close(Node? scope = null)
	{
		if (cached == null) return;
		if (scope != null && !scope.IsAncestorOf(cached.Display)) return;
		cached.Close();
	}

	internal static void Kick()
	{
		if (cached is { Playing: true, Released: true } stage) stage.Target += KickDeg;
	}

	internal static bool Warm(Node parent)
	{
		if (!Ensure(parent.GetTree())) return false;
		cached!.Warm(parent);
		return true;
	}

	private sealed class Clip
	{
		internal readonly string[] Bones, ScaleBones;
		internal readonly float[] Data, Scales;
		internal readonly int Frames;
		internal readonly double Fps;
		internal double Duration => (Frames - 1) / Fps;

		internal Clip(string name, bool hero)
		{
			using JsonDocument json = JsonDocument.Parse(Read("anim/" + name + ".json"));
			JsonElement root = json.RootElement;
			Bones = Names(root.GetProperty("bones"));
			Frames = root.GetProperty("frames").GetInt32();
			Fps = root.GetProperty("fps").GetDouble();
			if (Frames < 2 || Bones.Length == 0 || !double.IsFinite(Fps) || Fps <= 0)
				throw new InvalidOperationException("invalid animation metadata: " + name);
			Data = Floats("anim/" + name + ".bin", checked(Frames * Bones.Length * 7));
			ScaleBones = hero ? Names(root.GetProperty("scale_bones")) : Array.Empty<string>();
			Scales = hero ? Floats("anim/" + name + ".scale.bin", checked(Frames * ScaleBones.Length * 3)) : Array.Empty<float>();
			// 坏四元数在初始化时拒绝，正常播放时不扫描 / 分配大数组。
			for (int o = 0; o < Data.Length; o += 7)
			{
				Quaternion q = Rotation(Data, o);
				if (q.LengthSquared() < 0.000001f) throw new InvalidOperationException("invalid quaternion: " + name);
			}
		}

		private static string[] Names(JsonElement list)
		{
			string[] names = new string[list.GetArrayLength()];
			for (int i = 0; i < names.Length; i++)
				names[i] = list[i].GetString() ?? throw new InvalidOperationException("null bone name");
			return names;
		}
	}

	private sealed class Rig
	{
		internal readonly Skeleton3D Skeleton;
		internal readonly Dictionary<string, int> Bones = new(StringComparer.Ordinal);
		internal Rig(Skeleton3D skeleton)
		{
			Skeleton = skeleton;
			for (int i = 0; i < skeleton.GetBoneCount(); i++) Bones.Add(skeleton.GetBoneName(i).ToString(), i);
		}
		internal int Index(string name) => Bones.TryGetValue(name, out int i) ? i : -1;
	}

	private sealed class Binding
	{
		private readonly Rig rig;
		private readonly Clip clip;
		private readonly int[] bones, scales;
		internal Binding(Rig r, Clip c)
		{
			rig = r;
			clip = c;
			bones = Map(c.Bones);
			scales = Map(c.ScaleBones);
		}
		private int[] Map(string[] names)
		{
			int[] map = new int[names.Length];
			for (int i = 0; i < map.Length; i++) map[i] = rig.Index(names[i]);
			return map;
		}
		internal void Pose(double time)
		{
			double f = Math.Clamp(time * clip.Fps, 0, clip.Frames - 1);
			int a = (int)f, b = Math.Min(a + 1, clip.Frames - 1);
			float t = (float)(f - a);
			Skeleton3D skeleton = rig.Skeleton;
			for (int i = 0; i < bones.Length; i++)
			{
				int index = bones[i];
				if (index < 0) continue;
				int oa = (a * bones.Length + i) * 7, ob = (b * bones.Length + i) * 7;
				skeleton.SetBonePosePosition(index, Vector(clip.Data, oa).Lerp(Vector(clip.Data, ob), t));
				skeleton.SetBonePoseRotation(index, Rotation(clip.Data, oa).Normalized().Slerp(Rotation(clip.Data, ob).Normalized(), t));
			}
			for (int i = 0; i < scales.Length; i++)
			{
				if (scales[i] < 0) continue;
				int oa = (a * scales.Length + i) * 3, ob = (b * scales.Length + i) * 3;
				// stage_hero3d.py 已换好轴；包括值为 1 的帧也要应用，清掉上段动作的缩放。
				skeleton.SetBonePoseScale(scales[i], Vector(clip.Scales, oa).Lerp(Vector(clip.Scales, ob), t));
			}
		}
	}

	private sealed class Motion
	{
		internal readonly Clip Hero;
		internal readonly Binding[] Bindings;
		internal Motion(string name, List<Rig> heroes, List<Rig> cards)
		{
			Hero = new Clip(name, true);
			Clip card = new("Card_" + name, false);
			if (Hero.Frames != card.Frames || Math.Abs(Hero.Fps - card.Fps) > 0.001)
				throw new InvalidOperationException("hero/card timeline mismatch: " + name);
			Bindings = new Binding[heroes.Count + cards.Count];
			int i = 0;
			foreach (Rig r in heroes) Bindings[i++] = new Binding(r, Hero);
			foreach (Rig r in cards) Bindings[i++] = new Binding(r, card);
		}
		internal void Pose(double time)
		{
			foreach (Binding b in Bindings) b.Pose(time);
		}
	}

	private sealed class Stage
	{
		private readonly SceneTree tree;
		private readonly SubViewport viewport = new()
		{
			Name = "P3HeroStage", OwnWorld3D = true, TransparentBg = true,
			Msaa3D = Viewport.Msaa.Msaa2X, Size = new Vector2I(1920, 1080),
			RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
			ProcessMode = Node.ProcessModeEnum.Always,
		};
		internal readonly TextureRect Display = new()
		{
			Name = "P3Hero3D", ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		private readonly List<Rig> heroes = new(), cards = new();
		private readonly Dictionary<string, Texture2D> textures = new(StringComparer.Ordinal);
		private readonly Dictionary<string, Material> materials = new(StringComparer.Ordinal);
		private readonly List<(Skeleton3D Skeleton, int Index)> spinBones = new();
		private readonly List<Motion> motions = new();
		private Node2D? owner;
		private ShaderMaterial outline = null!;
		private int phase, warmFrames;
		private ulong lastTick;
		private double time;
		private float releaseTime, baseAngle, extra, velocity;
		internal float Target;
		internal bool Playing { get; private set; }
		internal bool Released => phase > 0;

		internal Stage(SceneTree t) { tree = t; }

		internal void Load()
		{
			foreach (string file in new[] { "T_PC0051_C002_00_Col.png", "T_PC0051_F000_00_Col.png", "T_PC0051_E000_00_Col.png", "T_Pp2001_030_0017.png", "T_Pp2001_Backside_00.png" })
			{
				using Image image = new();
				if (image.LoadPngFromBuffer(Read("tex/" + file)) != Error.Ok || image.IsEmpty())
					throw new InvalidOperationException("invalid hero texture: " + file);
				textures.Add(file, ImageTexture.CreateFromImage(image));
			}
			outline = new ShaderMaterial { Shader = Shaders.HeroOutline };
			Node3D world = new();
			viewport.AddChild(world);
			world.AddChild(new Camera3D { KeepAspect = Camera3D.KeepAspectEnum.Width, Fov = 37.35f, Current = true });
			world.AddChild(new WorldEnvironment { Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.ClearColor } });
			foreach (string model in new[] { "SK_PC0051_C002.glb", "SK_PC0051_F000.glb", "SK_PC0051_H000.glb", "SK_Pp2001_000.glb" })
			{
				using GltfDocument doc = new();
				using GltfState state = new();
				if (doc.AppendFromBuffer(Read(model), Path, state) != Error.Ok)
					throw new InvalidOperationException("invalid hero model: " + model);
				Node scene = doc.GenerateScene(state) ?? throw new InvalidOperationException("empty hero scene: " + model);
				world.AddChild(scene); // 根节点不作位移 / 旋转，位置全来自动作。
				bool card = model == "SK_Pp2001_000.glb";
				int count = card ? cards.Count : heroes.Count;
				Walk(scene, card);
				if ((card ? cards.Count : heroes.Count) == count)
					throw new InvalidOperationException("hero skeleton missing: " + model);
			}
			foreach (Rig r in cards)
			{
				int index = r.Index("Jnt_C_Center_01");
				if (index >= 0) spinBones.Add((r.Skeleton, index));
			}
			if (spinBones.Count == 0) throw new InvalidOperationException("card spin bone missing");
			foreach (string name in new[] { "CommuStart", "CommuListStart", "CommuListLoop", "CommuLoop", "CommuListCancel" })
				motions.Add(new Motion(name, heroes, cards));
			ShaderMaterial keyOut = new() { Shader = Shaders.HeroKeyOut };
			keyOut.SetShaderParameter("key_color", HeroKey);
			keyOut.SetShaderParameter("key_low", KeyDistanceLow);
			keyOut.SetShaderParameter("key_high", KeyDistanceHigh);
			Display.Material = keyOut;
			Display.Texture = viewport.GetTexture();
			motions[0].Pose(0);
			tree.Root.AddChild(viewport); // 挂在持久根下，不随牌堆 root 销毁。
		}

		private void Walk(Node node, bool card)
		{
			if (node is Skeleton3D skeleton) (card ? cards : heroes).Add(new Rig(skeleton));
			if (node is AnimationPlayer player) player.ProcessMode = Node.ProcessModeEnum.Disabled;
			if (node is MeshInstance3D mesh && mesh.Mesh != null)
			{
				for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
				{
					string name = mesh.Mesh.SurfaceGetMaterial(i)?.ResourceName ?? "";
					mesh.SetSurfaceOverrideMaterial(i, MaterialFor(name, card));
				}
			}
			foreach (Node child in node.GetChildren()) Walk(child, card);
		}

		private Material MaterialFor(string name, bool card)
		{
			string key = (card ? "card:" : "hero:") + name;
			if (materials.TryGetValue(key, out Material? existing)) return existing;
			Material? m = null;
			if (card)
			{
				m = new StandardMaterial3D
				{
					ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
					Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor,
					AlbedoTexture = textures[name.Contains("Backside", StringComparison.Ordinal) ? "T_Pp2001_Backside_00.png" : "T_Pp2001_030_0017.png"],
				};
			}
			else
			{
				// FILLS / TEXMAP 与 tools/p3r/hero_render/render.gd 保持一致。
				string? fill = name switch
				{
					"MI_PC0051_C002_02_CaFiOp" => "5BBCFF", "MI_PC0051_C002_03_CaFiOp" => "9DACC7",
					"MI_PC0051_C002_04_CaFiOp" => "444444", "MI_PC0051_C002_05_CaFiOp" => "0961B5",
					"MI_PC0051_C002_06_CaFiOp" => "BCC4D2", "MI_PC0051_C002_01_CaFiOpTxRs" => "000000",
					"MI_PC0051_H000_00_CaBaOp" => "00E7F8", _ => null,
				};
				string? tex = name switch
				{
					"MI_PC0051_C002_00_CaBaOp" => "T_PC0051_C002_00_Col.png",
					"MI_PC0051_F000_00_CaBaOp" => "T_PC0051_F000_00_Col.png", _ => null,
				};
				if (fill != null || name.EndsWith("CaRtOp", StringComparison.Ordinal))
				{
					ShaderMaterial flat = new() { Shader = Shaders.HeroFlat };
					flat.SetShaderParameter("col", fill != null ? new Color(fill) : HeroKey);
					m = flat;
				}
				else if (tex != null)
				{
					ShaderMaterial ramp = new() { Shader = Shaders.HeroRamp };
					ramp.SetShaderParameter("tex", textures[tex]);
					m = ramp;
				}
				else if (name == "MI_PC0051_F000_00_CaEyOp")
				{
					m = new StandardMaterial3D
					{
						ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
						AlbedoTexture = textures["T_PC0051_E000_00_Col.png"], Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor,
					};
				}
				if (m != null) m.NextPass = outline;
			}
			m ??= new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = new Color(0, 0, 0, 0) };
			materials.Add(key, m);
			return m;
		}

		private void Resize(Vector2 view)
		{
			float scale = Math.Min(1f, 1920f / Math.Max(view.X, 1f));
			viewport.Size = new Vector2I(Math.Max(1, (int)(view.X * scale)), Math.Max(1, (int)(view.Y * scale)));
		}

		internal void Open(Node2D parent, Vector2 view, float shift)
		{
			Close();
			Resize(view);
			phase = 0;
			time = 0;
			releaseTime = baseAngle = extra = velocity = Target = 0;
			motions[0].Pose(0);
			Display.Size = view;
			Display.Position = new Vector2(shift, 0);
			owner = parent;
			owner.AddChild(Display);
			owner.TreeExiting += Close;
			Playing = true;
			lastTick = Time.GetTicksUsec();
			tree.ProcessFrame += Tick;
			viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
		}

		internal void Close()
		{
			Playing = false;
			tree.ProcessFrame -= Tick;
			CancelWarm();
			viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
			if (owner != null && GodotObject.IsInstanceValid(owner)) owner.TreeExiting -= Close;
			owner = null;
			Display.GetParent()?.RemoveChild(Display); // 只移走显示节点，舞台 / 贴图 / 骨骼缓存保留。
		}

		internal void Warm(Node parent)
		{
			if (Playing || warmFrames > 0) return;
			Resize(tree.Root.GetVisibleRect().Size);
			motions[0].Pose(0);
			// tiny 节点由 IdleWarmup 清理；同时编译用于显示的 CanvasItem 色键着色器。
			parent.AddChild(new TextureRect
			{
				Texture = Display.Texture, Material = Display.Material, Size = new Vector2(1000, 1000),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = Control.MouseFilterEnum.Ignore,
			});
			warmFrames = 2;
			RenderingServer.FramePostDraw += WarmDrawn;
			viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
		}

		private void WarmDrawn()
		{
			if (--warmFrames > 0) viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
			else
			{
				CancelWarm();
				viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
			}
		}
		private void CancelWarm()
		{
			RenderingServer.FramePostDraw -= WarmDrawn;
			warmFrames = 0;
		}

		private void Tick()
		{
			if (!Playing) return;
			ulong now = Time.GetTicksUsec();
			double elapsed = (now - lastTick) / 1000000.0;
			lastTick = now;
			double beforeRelease = phase == 0 ? Math.Max(0, motions[0].Hero.Duration - time) : 0;
			time += elapsed;
			while (phase < 2 && time >= motions[phase].Hero.Duration)
			{
				time -= motions[phase].Hero.Duration;
				phase++;
			}
			if (phase == 2) time %= motions[2].Hero.Duration;
			motions[phase].Pose(time); // 主角与牌永远用同一段、同一秒数。
			if (!Released) return;
			float dt = (float)Math.Clamp(elapsed - beforeRelease, 0, 1.0 / 30.0);
			releaseTime += dt;
			float ramp = Mathf.Clamp(releaseTime / 0.6f, 0, 1);
			baseAngle = (baseAngle + BaseSpinDegPerSec * ramp * ramp * (3f - 2f * ramp) * dt) % 360f;
			float a = KickOmega * KickOmega * (Target - extra) - 2f * KickOmega * velocity;
			velocity += a * dt;
			extra += velocity * dt;
			float turns = Mathf.Floor(extra / 360f) * 360f;
			extra -= turns;
			Target -= turns;
			Quaternion spin = new(Vector3.Up, Mathf.DegToRad(baseAngle + extra));
			foreach (var bone in spinBones) bone.Skeleton.SetBonePoseRotation(bone.Index, spin);
		}

		internal void Dispose()
		{
			Close();
			Display.Free();
			viewport.Free();
		}
	}

	private static byte[] Read(string file)
	{
		byte[] data = FileAccess.GetFileAsBytes(Path + file);
		if (data.Length == 0) throw new InvalidOperationException("missing / empty hero resource: " + file);
		return data;
	}
	private static float[] Floats(string file, int count)
	{
		byte[] bytes = Read(file);
		if (bytes.Length != checked(count * sizeof(float))) throw new InvalidOperationException("invalid animation size: " + file);
		float[] data = new float[count];
		Buffer.BlockCopy(bytes, 0, data, 0, bytes.Length);
		foreach (float value in data)
			if (!float.IsFinite(value)) throw new InvalidOperationException("non-finite animation value: " + file);
		return data;
	}
	private static Vector3 Vector(float[] data, int o) => new(data[o], data[o + 1], data[o + 2]);
	private static Quaternion Rotation(float[] data, int o) => new(data[o + 3], data[o + 4], data[o + 5], data[o + 6]);
}
