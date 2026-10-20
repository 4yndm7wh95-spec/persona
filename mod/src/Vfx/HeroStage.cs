using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

namespace P3Makoto.Vfx;

/// <summary>缓存的原版 COMMU 舞台；运行时直接读 GLB / PNG / 动作，不依赖导入文件。</summary>
internal static class HeroStage
{
	public const float BaseSpinDegPerSec = 36f;
	public const float KickDeg = 120f;
	public const float KickOmega = 9f;
	public static readonly Color HeroKey = new(0f, 1f, 0f);
	private const string Path = "res://p3makoto/hero3d/";
	private static Stage? cached;
	private static bool failed;

	private static readonly string[] Images = { "T_PC0051_C002_00_Col.png", "T_PC0051_F000_00_Col.png", "T_PC0051_E000_00_Col.png", "T_Pp2001_030_0017.png", "T_Pp2001_Backside_00.png", "T_UI_Camp_Caustics_00.png", "commu_word.png" };

	/// <summary>v0.14.96：营地主菜单（牌组入水）的资源单独读，读不到只影响牌组，不连累抽牌堆的 COMMU。</summary>
	private static readonly string[] DiveImages = {
		"../dive/splash_00.png", "../dive/splash_01.png", "../dive/splash_02.png", "../dive/splash_03.png", "../dive/splash_04.png", "../dive/splash_05.png", "../dive/splash_06.png", "../dive/splash_07.png", "../dive/splash_08.png", "../dive/splash_09.png",
		"../dive/mask_00.png", "../dive/mask_01.png", "../dive/mask_02.png", "../dive/mask_03.png", "../dive/mask_04.png", "../dive/mask_05.png", "../dive/mask_06.png", "../dive/mask_07.png", "../dive/mask_08.png", "../dive/mask_09.png" };

	/// <summary>入水转场的原版波浪网格偏差（stage_hero3d.py：75 帧 × 10 行 × 17 列 × [dx, dy]）。</summary>
	internal static float[]? Warp => cached?.WarpData;
	private static readonly string[] Models = { "SK_PC0051_C002.glb", "SK_PC0051_F000.glb", "SK_PC0051_H000.glb", "SK_Pp2001_000.glb" };
	private static readonly string[] MotionNames = { "CommuStart", "CommuListStart", "CommuListLoop", "CommuLoop", "CommuListCancel" };
	private static readonly string[] TopNames = { "CampTopStart", "CampTopLoop" };

	/// <summary>舞台演哪一页：COMMU（抽牌堆：拿牌 → 松手，牌浮空自转）；Top（营地主菜单：倒入水 → 水下待机，牌组牌堆 / ESC 设置用，不显示牌）。</summary>
	internal enum Act { Commu, Top }
	private static Task<Prepared>? prep;

	/// <summary>后台线程里能做的部分：PNG 解码、GLB 解析 + 生成场景（还没进场景树）、动作数据读取校验。</summary>
	private sealed class Prepared
	{
		internal readonly Dictionary<string, Image> Images = new(StringComparer.Ordinal);
		internal readonly List<(string Model, Node Scene)> Scenes = new();
		internal readonly Dictionary<string, Clip> Clips = new(StringComparer.Ordinal);
		internal float[] Warp = Array.Empty<float>();
		internal string? DiveError;
	}

	/// <summary>v0.14.91（实机日志：主线程一次加载 ~150ms，选人界面掉帧）：在后台线程开始解析，可重复调用。</summary>
	internal static void Prefetch()
	{
		if (prep != null || cached != null || failed) return;
		prep = Task.Run(() =>
		{
			Prepared p = new();
			foreach (string file in Images)
			{
				Image image = new();
				if (image.LoadPngFromBuffer(Read(file.StartsWith("../", StringComparison.Ordinal) ? file[3..] : "tex/" + file)) != Error.Ok || image.IsEmpty())
					throw new InvalidOperationException("invalid hero texture: " + file);
				p.Images.Add(file, image);
			}
			foreach (string model in Models)
			{
				using GltfDocument doc = new();
				using GltfState state = new();
				if (doc.AppendFromBuffer(Read(model), Path, state) != Error.Ok)
					throw new InvalidOperationException("invalid hero model: " + model);
				p.Scenes.Add((model, doc.GenerateScene(state) ?? throw new InvalidOperationException("empty hero scene: " + model)));
			}
			foreach (string name in MotionNames)
			{
				p.Clips.Add(name, new Clip(name, true));
				p.Clips.Add("Card_" + name, new Clip("Card_" + name, false));
			}
			try
			{
				foreach (string file in DiveImages)
				{
					Image image = new();
					if (image.LoadPngFromBuffer(Read(file[3..])) != Error.Ok || image.IsEmpty())
						throw new InvalidOperationException("invalid dive texture: " + file);
					p.Images.Add(file, image);
				}
				foreach (string name in TopNames) p.Clips.Add(name, new Clip(name, true));
				p.Warp = Floats("dive/warp.bin", 75 * 10 * 17 * 2);
			}
			catch (Exception e)
			{
				p.DiveError = e.Message;   // 主线程装配时报警告；牌组退回原样式
			}
			return p;
		});
	}

	/// <summary>后台解析已结束（成功或失败）；IdleWarmup 等到这一步再在主线程装配、渲染预热。</summary>
	internal static bool PrefetchDone => cached != null || failed || prep is { IsCompleted: true };

	private static bool Ensure(SceneTree tree)
	{
		if (failed) return false;
		if (cached != null) return true;
		Prefetch();
		Stage stage = new(tree);
		try
		{
			stage.Load(prep!.GetAwaiter().GetResult());   // 预热没赶上时（很快就开了抽牌堆）在这里等后台解析完
			cached = stage;
			GD.Print($"{ModEntry.Tag} hero3d ready (dive {(stage.WarpData.Length > 0 ? "ok" : "unavailable")})");
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
		if (!failed) GD.PushWarning($"[P3Makoto] hero3d unavailable; using portrait: {e}");   // 带调用栈，便于从日志定位
		failed = true;
	}

	internal static bool Open(Node2D parent, Vector2 view, float shift, Act act = Act.Commu)
	{
		if (!Ensure(parent.GetTree())) return false;
		try
		{
			cached!.Open(parent, view, shift, act);
			return true;
		}
		catch (Exception e)
		{
			cached!.Close();
			if (act == Act.Top) GD.PushWarning($"[P3Makoto] dive open failed: {e.Message}");   // 只牌组退回原样式
			else Fail(e);
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

	/// <summary>舞台能用（已加载或能加载）；抽牌堆据此决定换不换原版 COMMU 背景。</summary>
	internal static bool Available(SceneTree tree) => Ensure(tree);

	/// <summary>舞台里一起读进来的原版 UI 贴图（焦散、COMMU 字形）。</summary>
	internal static Texture2D? Tex(string file) => cached != null && (cached.Textures.TryGetValue(file, out Texture2D? t) || cached.Textures.TryGetValue("../dive/" + file, out t)) ? t : null;

	/// <summary>Open 之后调用：人物剪影的灰色小阴影（加在白色斜块下面、被它裁剪）。</summary>
	internal static void AddShadow(Node parent, Vector2 offset)
	{
		if (cached is { Playing: true } stage) parent.AddChild(stage.Shadow(offset));
	}

	/// <summary>Open 之后调用：让这个多边形在人物剪影处挖空（Shaders.HeroCut）。</summary>
	internal static void CutOut(CanvasItem item)
	{
		if (cached is not { Playing: true } stage) return;
		ShaderMaterial m = new() { Shader = Shaders.HeroCut };
		m.SetShaderParameter("hero", stage.Display.Texture);
		m.SetShaderParameter("hero_pos", stage.Display.Position);
		m.SetShaderParameter("hero_size", stage.Display.Size);
		if (item is Node2D n && stage.Display.GetParent() is Node2D host)
		{
			Transform2D t = host.GlobalTransform.AffineInverse() * n.GlobalTransform;
			m.SetShaderParameter("xa", t.X);
			m.SetShaderParameter("xb", t.Y);
			m.SetShaderParameter("xo", t.Origin);
		}
		item.Material = m;
	}

	/// <summary>入水转场的波浪扭曲（Shaders.WarpGlsl）：warp = 网格偏差贴图，on = 0~1。</summary>
	internal static void SetWarp(Texture2D? warp, float on)
	{
		if (cached?.Display.Material is not ShaderMaterial m) return;
		if (warp != null) m.SetShaderParameter("warp_tex", warp);
		m.SetShaderParameter("warp_on", on);
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
		internal void Pose(double time) => Pose(time, null, 0, 1f);

		/// <summary>
		/// v0.14.92（用户：拿牌 → 松手那一下衣角和挂坠跳一下）：原版 CommuStart 末帧和 CommuListStart 首帧的外套 / 耳机线骨骼差 23°，
		/// 原版靠 UE 切换动作时的混合过渡抹平。from 不为空时：先算上一段在 fromTime 的姿势，再按 w 过渡到这一段（两段骨骼列表一致）。
		/// </summary>
		internal void Pose(double time, Binding? from, double fromTime, float w)
		{
			(int a, int b, float t) = Frames(clip, time);
			int fa = 0, fb = 0;
			float ft = 0f;
			bool blend = from != null && w < 1f && from.clip.Bones.Length == clip.Bones.Length;
			if (blend) (fa, fb, ft) = Frames(from!.clip, fromTime);
			Skeleton3D skeleton = rig.Skeleton;
			for (int i = 0; i < bones.Length; i++)
			{
				int index = bones[i];
				if (index < 0) continue;
				int oa = (a * bones.Length + i) * 7, ob = (b * bones.Length + i) * 7;
				Vector3 pos = Vector(clip.Data, oa).Lerp(Vector(clip.Data, ob), t);
				Quaternion rot = Rotation(clip.Data, oa).Normalized().Slerp(Rotation(clip.Data, ob).Normalized(), t);
				if (blend)
				{
					float[] d = from!.clip.Data;
					int pa = (fa * bones.Length + i) * 7, pb = (fb * bones.Length + i) * 7;
					Vector3 fpos = Vector(d, pa).Lerp(Vector(d, pb), ft);
					Quaternion frot = Rotation(d, pa).Normalized().Slerp(Rotation(d, pb).Normalized(), ft);
					pos = fpos.Lerp(pos, w);
					rot = frot.Slerp(rot, w);
				}
				skeleton.SetBonePosePosition(index, pos);
				skeleton.SetBonePoseRotation(index, rot);
			}
			for (int i = 0; i < scales.Length; i++)
			{
				if (scales[i] < 0) continue;
				int oa = (a * scales.Length + i) * 3, ob = (b * scales.Length + i) * 3;
				// stage_hero3d.py 已换好轴；包括值为 1 的帧也要应用，清掉上段动作的缩放。
				skeleton.SetBonePoseScale(scales[i], Vector(clip.Scales, oa).Lerp(Vector(clip.Scales, ob), t));
			}
		}

		private static (int A, int B, float T) Frames(Clip c, double time)
		{
			double f = Math.Clamp(time * c.Fps, 0, c.Frames - 1);
			int a = (int)f;
			return (a, Math.Min(a + 1, c.Frames - 1), (float)(f - a));
		}
	}

	private sealed class Motion
	{
		internal readonly Clip Hero;
		internal readonly Binding[] Bindings;
		internal Motion(string name, Clip hero, Clip? card, List<Rig> heroes, List<Rig> cards)
		{
			Hero = hero;
			if (card == null) cards = new List<Rig>();   // 营地主菜单那几段没有牌
			if (card != null && (Hero.Frames != card.Frames || Math.Abs(Hero.Fps - card.Fps) > 0.001))
				throw new InvalidOperationException("hero/card timeline mismatch: " + name);
			Bindings = new Binding[heroes.Count + cards.Count];
			int i = 0;
			foreach (Rig r in heroes) Bindings[i++] = new Binding(r, Hero);
			foreach (Rig r in cards) Bindings[i++] = new Binding(r, card!);
		}
		internal void Pose(double time)
		{
			foreach (Binding b in Bindings) b.Pose(time);
		}

		/// <summary>从 from（停在 fromTime 的姿势）过渡到这一段，w 0→1。</summary>
		internal void Pose(double time, Motion from, double fromTime, float w)
		{
			for (int i = 0; i < Bindings.Length; i++) Bindings[i].Pose(time, from.Bindings[i], fromTime, w);
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
		internal Dictionary<string, Texture2D> Textures => textures;
		internal float[] WarpData = Array.Empty<float>();
		private readonly ShaderMaterial shadowMat = new() { Shader = Shaders.HeroShadow };
		private readonly Dictionary<string, Material> materials = new(StringComparer.Ordinal);
		private readonly List<(Skeleton3D Skeleton, int Index)> spinBones = new();
		private readonly List<Motion> motions = new();
		private readonly List<Motion> topMotions = new();
		private readonly List<Node3D> cardScenes = new();
		private List<Motion> seq = null!;
		private Act act;
		private Node2D? owner;
		private int phase, warmFrames, blendFrom = -1;
		private double blendTime;
		private const double BlendSeconds = 0.25;   // UE 切换动作的默认混合时长
		private bool ticking, warmHooked;   // v0.14.91：只解绑真绑上的回调（实机日志 18 条 "Attempt to disconnect"，每条带整段调用栈）
		private ulong lastTick;
		private double time;
		private float releaseTime, baseAngle, extra, velocity;
		internal float Target;
		internal bool Playing { get; private set; }
		internal bool Released => act == Act.Commu && phase > 0;

		internal Stage(SceneTree t) { tree = t; }

		internal void Load(Prepared p)
		{
			WarpData = p.Warp;
			foreach ((string file, Image image) in p.Images)
			{
				textures.Add(file, ImageTexture.CreateFromImage(image));
				image.Dispose();
			}
			Node3D world = new();
			viewport.AddChild(world);
			world.AddChild(new Camera3D { KeepAspect = Camera3D.KeepAspectEnum.Width, Fov = 37.35f, Current = true });
			world.AddChild(new WorldEnvironment { Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.ClearColor } });
			foreach ((string model, Node scene) in p.Scenes)
			{
				world.AddChild(scene); // 根节点不作位移 / 旋转，位置全来自动作。
				bool card = model == "SK_Pp2001_000.glb";
				if (card && scene is Node3D n3) cardScenes.Add(n3);
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
			foreach (string name in MotionNames)
				motions.Add(new Motion(name, p.Clips[name], p.Clips["Card_" + name], heroes, cards));
			if (p.DiveError == null)
			{
				foreach (string name in TopNames)
					topMotions.Add(new Motion(name, p.Clips[name], null, heroes, cards));
			}
			else
			{
				WarpData = Array.Empty<float>();
				GD.PushWarning($"[P3Makoto] dive (deck pile) unavailable: {p.DiveError}");
			}
			seq = motions;
			ShaderMaterial keyOut = new() { Shader = Shaders.HeroKeyOut };
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
				// FILLS / TEXMAP 与 tools/p3r/hero_render/render.gd 保持一致；CaRtOp = 纯色键，完全透出后面的水（用户：不要自己填色）。
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

		internal void Open(Node2D parent, Vector2 view, float shift, Act which)
		{
			Close();
			Resize(view);
			if (which == Act.Top && topMotions.Count == 0) throw new InvalidOperationException("dive assets unavailable");
			act = which;
			seq = which == Act.Top ? topMotions : motions;
			foreach (Node3D c in cardScenes) c.Visible = which == Act.Commu;
			if (Display.Material is ShaderMaterial km) km.SetShaderParameter("warp_on", 0f);
			phase = 0;
			time = 0;
			blendFrom = -1;
			releaseTime = baseAngle = extra = velocity = Target = 0;
			seq[0].Pose(0);
			Display.Size = view;
			Display.Position = new Vector2(shift, 0);
			owner = parent;
			owner.AddChild(Display);
			owner.TreeExiting += Close;
			Playing = true;
			lastTick = Time.GetTicksUsec();
			tree.ProcessFrame += Tick;
			ticking = true;
			viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
		}

		internal TextureRect Shadow(Vector2 offset)
		{
			shadowMat.SetShaderParameter("cut", offset / Display.Size);
			return new TextureRect
			{
				Texture = Display.Texture, Material = shadowMat, Size = Display.Size, Position = Display.Position + offset,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
		}

		internal void Close()
		{
			Playing = false;
			if (ticking) tree.ProcessFrame -= Tick;
			ticking = false;
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
			// v0.14.91（用户：第一次开抽牌堆仍卡一下）：COMMU 背景 / 白块挖空 / 人物阴影这几个 2D 着色器和"裁剪子节点"的绘制路径也先画一遍
			ShaderMaterial bg = new() { Shader = Shaders.CommuBg };
			if (Tex("T_UI_Camp_Caustics_00.png") is Texture2D caustics) bg.SetShaderParameter("caustics", caustics);
			parent.AddChild(new ColorRect { Material = bg, Size = new Vector2(64, 64), MouseFilter = Control.MouseFilterEnum.Ignore });
			Polygon2D clip = new() { Polygon = new[] { Vector2.Zero, new Vector2(64, 0), new Vector2(0, 64) }, ClipChildren = CanvasItem.ClipChildrenMode.AndDraw };
			ShaderMaterial cut = new() { Shader = Shaders.HeroCut };
			cut.SetShaderParameter("hero", Display.Texture);
			clip.Material = cut;
			parent.AddChild(clip);
			clip.AddChild(new TextureRect { Texture = Display.Texture, Material = shadowMat, Size = new Vector2(64, 64), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = Control.MouseFilterEnum.Ignore });
			if (Tex("commu_word.png") is Texture2D word) clip.AddChild(new Sprite2D { Texture = word, Scale = Vector2.One * 0.05f, Material = cut });
			warmFrames = 2;
			RenderingServer.FramePostDraw += WarmDrawn;
			warmHooked = true;
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
			if (warmHooked) RenderingServer.FramePostDraw -= WarmDrawn;
			warmHooked = false;
			warmFrames = 0;
		}

		private void Tick()
		{
			if (!Playing) return;
			ulong now = Time.GetTicksUsec();
			// 单帧最多推进 1/20 秒：打开牌堆那一帧要建界面（几十毫秒），不夹住的话入场动作会直接跳掉一截。
			double elapsed = Math.Min((now - lastTick) / 1000000.0, 0.05);
			lastTick = now;
			double beforeRelease = phase == 0 ? Math.Max(0, seq[0].Hero.Duration - time) : 0;
			time += elapsed;
			int last = seq.Count - 1;
			if (act == Act.Commu) last = 2;              // CommuStart → CommuListStart → CommuListLoop（循环）
			while (phase < last && time >= seq[phase].Hero.Duration)
			{
				time -= seq[phase].Hero.Duration;
				if (act == Act.Commu && phase == 0) { blendFrom = 0; blendTime = 0; }   // 拿牌 → 松手：两段首尾不连续，做混合过渡（另两处首尾本来就连续）
				phase++;
			}
			if (phase == last) time %= seq[last].Hero.Duration;
			if (blendFrom >= 0)
			{
				blendTime += elapsed;
				float w = (float)Math.Clamp(blendTime / BlendSeconds, 0, 1);
				w = w * w * (3f - 2f * w);
				seq[phase].Pose(time, seq[blendFrom], seq[blendFrom].Hero.Duration, w); // 上一段停在末帧
				if (w >= 1f) blendFrom = -1;
			}
			else
			{
				seq[phase].Pose(time); // 主角与牌永远用同一段、同一秒数。
			}
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
