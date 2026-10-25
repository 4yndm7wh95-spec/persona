using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using P3Makoto.Core;

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
		"../dive/mask_00.png", "../dive/mask_01.png", "../dive/mask_02.png", "../dive/mask_03.png", "../dive/mask_04.png", "../dive/mask_blank.png" };

	/// <summary>原版入水 75 帧 + 循环 210 帧缓存为一张 RG 图，播放时只更新帧号。</summary>
	internal static Texture2D? Warp => cached?.WarpTexture;
	private static readonly string[] Models = { "SK_PC0051_C002.glb", "SK_PC0051_F000.glb", "SK_PC0051_H000.glb", "SK_Pp2001_000.glb" };
	private static readonly string[] MotionNames = { "CommuStart", "CommuListStart", "CommuListLoop", "CommuLoop", "CommuListCancel" };
	private static readonly string[] TopNames = { "CampTopStart", "CampTopLoop" };
	/// <summary>v0.14.101 弃牌堆 = 原版营地 ITEM 页：伸手（进入 3.33s → 循环 6.67s）；资源单独读，读不到只影响弃牌堆。</summary>
	private static readonly string[] ItemNames = { "CampItemStart", "CampItemLoop" };
	/// <summary>v0.14.104 消耗堆 = 原版营地 SKILL 页：倒挂举召唤器（进入 3.17s → 循环 6.67s）；读不到只影响消耗堆。</summary>
	private static readonly string[] SkillNames = { "CampSkillStart", "CampSkillLoop" };
	private const string ItemWord = "item_word.png";

	/// <summary>ITEM 页头发色键带一点红（Shaders.HeroKeyOut 靠 c.r 认出头发，和外套分开上色）；其他页是纯色键。</summary>
	private static readonly Color ItemHairKey = new(0.02f, 1f, 0f);

	/// <summary>舞台演哪一页：COMMU（抽牌堆：拿牌 → 松手，牌浮空自转）；Top（营地主菜单：倒入水 → 水下待机，牌组牌堆，不显示牌）；Item（弃牌堆：ITEM 页伸手，人物填深海军蓝）。</summary>
	internal enum Act { Commu, Top, Item, Skill }

	/// <summary>
	/// SKILL 页的镜头 / 光 / 召唤器，与 tools/p3r/skill_pile_preview.py 用的离线渲染（tools/p3r/hero_render/render.gd）一致：
	/// 人物放在 (6.87cm, 0, 0)、偏航 -2°，镜头高 5cm；光从下方 (-0.4, -0.6, 0.7)；召唤器深灰 5E6680 分两层明暗（用户：原来像橡皮泥白模）。
	/// </summary>
	private static readonly Vector3 SkillCharaPos = new(0.06868873f, 0f, 0f), SkillCamPos = new(0f, 0.05f, 0f);
	private const float SkillCharaYaw = -2f;
	private static readonly Vector3 DefaultLight = new(-0.4f, 0.6f, 0.7f), SkillLight = new(-0.4f, -0.6f, 0.7f);
	private static readonly Color GunCol = new("9DACC7"), SkillGunCol = new("5E6680");
	private const float SkillGunToon = 0.25f;
	private static Task<Prepared>? prep;

	/// <summary>后台线程里能做的部分：PNG 解码、GLB 解析 + 生成场景（还没进场景树）、动作数据读取校验。</summary>
	private sealed class Prepared
	{
		internal readonly Dictionary<string, Image> Images = new(StringComparer.Ordinal);
		internal readonly List<(string Model, Node Scene)> Scenes = new();
		internal readonly Dictionary<string, Clip> Clips = new(StringComparer.Ordinal);
		internal float[] Warp = Array.Empty<float>();
		internal string? DiveError, ItemError, SkillError;
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
				float[] enter = Floats("dive/warp.bin", 75 * 10 * 17 * 2);
				float[] loop = Floats("dive/warp_loop.bin", 210 * 10 * 17 * 2);
				p.Warp = new float[enter.Length + loop.Length];
				Array.Copy(enter, p.Warp, enter.Length);
				Array.Copy(loop, 0, p.Warp, enter.Length, loop.Length);
			}
			catch (Exception e)
			{
				p.DiveError = e.Message;   // 主线程装配时报警告；牌组退回原样式
			}
			try
			{
				Image word = new();
				if (word.LoadPngFromBuffer(Read("tex/" + ItemWord)) != Error.Ok || word.IsEmpty())
					throw new InvalidOperationException("invalid item texture: " + ItemWord);
				p.Images.Add(ItemWord, word);
				foreach (string name in ItemNames) p.Clips.Add(name, new Clip(name, true));
			}
			catch (Exception e)
			{
				p.ItemError = e.Message;   // 只弃牌堆退回原样式
			}
			try
			{
				foreach (string name in SkillNames) p.Clips.Add(name, new Clip(name, true));
			}
			catch (Exception e)
			{
				p.SkillError = e.Message;   // 只消耗堆退回原样式
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
			GD.Print($"{ModEntry.Tag} hero3d ready (dive {(stage.WarpTexture != null ? "ok" : "unavailable")}); materials=ink-preserve; dive_mode=native-layout-loop210");
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
			if (act != Act.Commu) GD.PushWarning($"[P3Makoto] {act} open failed: {e.Message}");   // 只牌组 / 弃牌堆退回原样式
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
	internal static void AddShadow(Node parent, Vector2 offset, float opacity = 0.55f, float soft = 0f)
	{
		if (cached is { Playing: true } stage) parent.AddChild(stage.Shadow(offset, opacity, soft));
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
	internal static void SetWarp(Texture2D? warp, float on, float frame = 0f)
	{
		if (cached?.Display.Material is not ShaderMaterial m) return;
		if (warp != null) m.SetShaderParameter("warp_tex", warp);
		m.SetShaderParameter("warp_on", on);
		m.SetShaderParameter("warp_frame", frame);
	}

	/// <summary>ITEM 页人物落位那一下（原版 ItemSilhouette：等 10 帧、20 帧 CurveUp2 移到 (-90, -110)）：在 Open 的位置上再偏移。</summary>
	internal static void Nudge(Vector2 offset)
	{
		if (cached is not { Playing: true } stage) return;
		if (stage.Panned) stage.Pan(stage.Base + offset);
		else stage.Display.Position = stage.Base + offset;
	}

	/// <summary>ITEM 页能用（资源读到了）。</summary>
	internal static bool ItemReady => cached is { ItemReady: true };

	/// <summary>SKILL 页能用（资源读到了）。</summary>
	internal static bool SkillReady => cached is { SkillReady: true };

	internal static void Kick()
	{
		if (cached is { Playing: true, Released: true } stage) stage.Target += KickDeg;
	}

	/// <summary>人物与水面共用入场遮罩；空纹理参数只更新是否完全露出。</summary>
	internal static void SetEntryMask(Texture2D? mask, float full)
	{
		if (cached?.Display.Material is not ShaderMaterial m) return;
		if (mask != null) m.SetShaderParameter("entry_mask", mask);
		m.SetShaderParameter("entry_full", full);
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
			// 无缩放骨骼的动作合法地对应零字节 scale.bin，无需读取。
			Scales = ScaleBones.Length > 0 ? Floats("anim/" + name + ".scale.bin", checked(Frames * ScaleBones.Length * 3)) : Array.Empty<float>();
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
		internal readonly BoneScaleChannels ScaleChannels;
		internal Rig(Skeleton3D skeleton)
		{
			Skeleton = skeleton;
			ScaleChannels = new BoneScaleChannels(index => skeleton.SetBonePoseScale(index, Vector3.One));
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
			// Sparse scale tracks omit unit values. Restore the previous clip's channels
			// before switching; ITEM hides the gun which SKILL must show at full scale.
			rig.ScaleChannels.Enter(this, scales);
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
		internal Texture2D? WarpTexture;
		private readonly ShaderMaterial shadowMat = new() { Shader = Shaders.HeroShadow };
		private readonly Dictionary<string, Material> materials = new(StringComparer.Ordinal);
		private readonly List<(Skeleton3D Skeleton, int Index)> spinBones = new();
		private readonly List<Motion> motions = new();
		private readonly List<Motion> topMotions = new();
		private readonly List<Motion> itemMotions = new();
		private readonly List<Motion> skillMotions = new();
		private readonly List<Node3D> heroScenes = new();
		private readonly List<ShaderMaterial> rampMats = new();
		private ShaderMaterial? gunMat, hairKeyMat, faceMat;
		private readonly List<ShaderMaterial> outlineMats = new();
		private Camera3D? camera;
		private Rig? faceRig;
		private readonly Line2D noseLine = NoseStroke("P3ItemNoseLower");
		private readonly Line2D noseUpper = NoseStroke("P3ItemNoseUpper");
		private readonly Vector2[] nosePoints = new Vector2[17], noseUpperPoints = new Vector2[17];

		private static Line2D NoseStroke(string name)
		{
			// Both ends taper to a point, as in the original ITEM facial ink.
			var width = new Curve();
			width.AddPoint(new Vector2(0f, 0f));
			width.AddPoint(new Vector2(0.22f, 0.8f));
			width.AddPoint(new Vector2(0.5f, 1f));
			width.AddPoint(new Vector2(0.78f, 0.8f));
			width.AddPoint(new Vector2(1f, 0f));
			return new Line2D { Name = name, Antialiased = true, Width = 1.15f,
				DefaultColor = new Color("071D43"), WidthCurve = width };
		}
		internal bool ItemReady => itemMotions.Count > 0;
		internal bool SkillReady => skillMotions.Count > 0;
		internal Vector2 Base;
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
			foreach ((string file, Image image) in p.Images)
			{
				textures.Add(file, ImageTexture.CreateFromImage(image));
				image.Dispose();
			}
			Node3D world = new();
			viewport.AddChild(world);
			camera = new Camera3D { KeepAspect = Camera3D.KeepAspectEnum.Width, Fov = 37.35f, Current = true };
			world.AddChild(camera);
			world.AddChild(new WorldEnvironment { Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.ClearColor } });
			foreach ((string model, Node scene) in p.Scenes)
			{
				world.AddChild(scene); // 根节点不作位移 / 旋转，位置全来自动作。
				bool card = model == "SK_Pp2001_000.glb";
				if (card && scene is Node3D n3) cardScenes.Add(n3);
				else if (scene is Node3D h3) heroScenes.Add(h3);
				int count = card ? cards.Count : heroes.Count;
				Walk(scene, card);
				if (model == "SK_PC0051_F000.glb" && heroes.Count > count) faceRig = heroes[count];
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
				byte[] warpBytes = new byte[p.Warp.Length * sizeof(float)];
				Buffer.BlockCopy(p.Warp, 0, warpBytes, 0, warpBytes.Length);
				using Image warpImage = Image.CreateFromData(17, (75 + 210) * 10, false, Image.Format.Rgf, warpBytes);
				WarpTexture = ImageTexture.CreateFromImage(warpImage);
			}
			else
			{
				GD.PushWarning($"[P3Makoto] dive (deck pile) unavailable: {p.DiveError}");
			}
			if (p.ItemError == null)
			{
				foreach (string name in ItemNames)
					itemMotions.Add(new Motion(name, p.Clips[name], null, heroes, cards));
			}
			else
			{
				GD.PushWarning($"[P3Makoto] ITEM page (discard pile) unavailable: {p.ItemError}");
			}
			if (p.SkillError == null)
			{
				foreach (string name in SkillNames)
					skillMotions.Add(new Motion(name, p.Clips[name], null, heroes, cards));
			}
			else
			{
				GD.PushWarning($"[P3Makoto] SKILL page (exhaust pile) unavailable: {p.SkillError}");
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
					if (name == "MI_PC0051_C002_03_CaFiOp") gunMat = flat;
					if (name == "MI_PC0051_H000_00_CaRtOp") hairKeyMat = flat;
					m = flat;
				}
				else if (tex != null)
				{
					ShaderMaterial ramp = new() { Shader = Shaders.HeroRamp };
					ramp.SetShaderParameter("tex", textures[tex]);
					if (name == "MI_PC0051_F000_00_CaBaOp") faceMat = ramp;
					rampMats.Add(ramp);
					m = ramp;
				}
				else if (name == "MI_PC0051_OlOpCamp")
				{
					ShaderMaterial outline = new() { Shader = Shaders.HeroOutline };
					outlineMats.Add(outline);
					m = outline;
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
			if (which == Act.Item && itemMotions.Count == 0) throw new InvalidOperationException("item page assets unavailable");
			if (which == Act.Skill && skillMotions.Count == 0) throw new InvalidOperationException("skill page assets unavailable");
			act = which;
			seq = which switch { Act.Top => topMotions, Act.Item => itemMotions, Act.Skill => skillMotions, _ => motions };
			bool skill = which == Act.Skill;
			foreach (Node3D h in heroScenes)
			{
				h.Position = skill ? SkillCharaPos : Vector3.Zero;
				h.RotationDegrees = new Vector3(0f, skill ? SkillCharaYaw : 0f, 0f);
			}
			if (camera != null) camera.Position = skill ? SkillCamPos : Vector3.Zero;
			foreach (ShaderMaterial r in rampMats) r.SetShaderParameter("light_dir", skill ? SkillLight : DefaultLight);
			faceMat?.SetShaderParameter("item_face", which == Act.Item ? 1f : 0f);
			if (gunMat != null)
			{
				gunMat.SetShaderParameter("col", skill ? SkillGunCol : GunCol);
				gunMat.SetShaderParameter("toon", skill ? SkillGunToon : 0f);
				gunMat.SetShaderParameter("light_dir", skill ? SkillLight : DefaultLight);
			}
			foreach (Node3D c in cardScenes) c.Visible = which == Act.Commu;
			if (Display.Material is ShaderMaterial km)
			{
				km.SetShaderParameter("warp_on", 0f);
				km.SetShaderParameter("entry_full", 1f);
				km.SetShaderParameter("fill_on", which == Act.Item ? 1f : 0f);
			}
			foreach (ShaderMaterial o in outlineMats) o.SetShaderParameter("on", which == Act.Item ? 1f : 0f);
			hairKeyMat?.SetShaderParameter("col", which == Act.Item ? ItemHairKey : HeroKey);
			phase = 0;
			time = 0;
			blendFrom = -1;
			releaseTime = baseAngle = extra = velocity = Target = 0;
			seq[0].Pose(0);
			if (noseLine.GetParent() == null) Display.AddChild(noseLine);
			if (noseUpper.GetParent() == null) Display.AddChild(noseUpper);
			noseLine.Visible = which == Act.Item;
			noseUpper.Visible = which == Act.Item;
			Display.Size = view;
			// v0.14.105：ITEM / SKILL 页挪人物改成平移镜头的取景（视锥偏移），显示框始终铺满屏幕 —— 挪显示框会在屏幕边上露出一条没有人物的断层（用户截图）
			Panned = which == Act.Item || which == Act.Skill;
			if (Panned)
			{
				Display.Position = Vector2.Zero;
				Base = new Vector2(shift, 0);
				Pan(Base);
			}
			else
			{
				if (camera != null) camera.Projection = Camera3D.ProjectionType.Perspective;
				Display.Position = Base = new Vector2(shift, 0);
			}
			owner = parent;
			owner.AddChild(Display);
			UpdateNoseLine();
			owner.TreeExiting += Close;
			Playing = true;
			lastTick = Time.GetTicksUsec();
			tree.ProcessFrame += Tick;
			ticking = true;
			viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
		}

		internal bool Panned { get; private set; }

		/// <summary>人物在屏幕上平移 px（屏幕像素，右 / 下为正）：透视镜头换成等价的视锥（近平面宽 = 2·near·tan(fov/2)），再偏移视锥。</summary>
		internal void Pan(Vector2 px)
		{
			if (camera == null) return;
			float w = Math.Max(Display.Size.X, 1f);
			float size = 2f * camera.Near * Mathf.Tan(Mathf.DegToRad(37.35f) / 2f);
			camera.Projection = Camera3D.ProjectionType.Frustum;
			camera.Size = size;
			camera.FrustumOffset = new Vector2(-px.X / w * size, px.Y / w * size);
			UpdateNoseLine();
		}

		private void UpdateNoseLine()
		{
			if (act != Act.Item || faceRig == null || camera == null || !Display.IsInsideTree()) return;
			Skeleton3D s = faceRig.Skeleton;
			Vector2 viewportSize = new(viewport.Size.X, viewport.Size.Y);
			// Godot 4.5 Camera3D.UnprojectPosition omits KeepAspect for a frustum,
			// although the renderer honors it. Build the same matrix as the renderer.
			Projection projection = camera.Projection == Camera3D.ProjectionType.Frustum
				? Projection.CreateFrustumAspect(camera.Size, viewportSize.X / viewportSize.Y,
					camera.FrustumOffset, camera.Near, camera.Far, camera.KeepAspect == Camera3D.KeepAspectEnum.Width)
				: camera.GetCameraProjection();
			Transform3D toCamera = camera.GetCameraTransform().AffineInverse() * s.GlobalTransform;
			Vector2 At(string bone)
			{
				int i = faceRig.Index(bone);
				if (i < 0) return Vector2.Zero;
				Vector3 p = toCamera * s.GetBoneGlobalPose(i).Origin;
				Vector4 clip = projection * new Vector4(p.X, p.Y, p.Z, 1f);
				if (Mathf.IsZeroApprox(clip.W)) return Vector2.Zero;
				return new Vector2(clip.X / clip.W * 0.5f + 0.5f, -clip.Y / clip.W * 0.5f + 0.5f) * viewportSize;
			}
			Vector2 nose = At("Jnt_C_Nose_00"), leftEye = At("Jnt_L_Eye_00"), rightEye = At("Jnt_R_Eye_00");
			Vector2 eyes = rightEye - leftEye;
			if (eyes.LengthSquared() < 1f) { noseLine.Visible = noseUpper.Visible = false; return; }
			Vector2 across = eyes.Normalized();
			if (across.X < 0f) across = -across;
			Vector2 down = across.Orthogonal();
			if (down.Y < 0f) down = -down;
			float span = eyes.Length();
			if (Display.Material is ShaderMaterial km)
			{
				km.SetShaderParameter("item_eyes", new Vector4(leftEye.X / viewportSize.X, leftEye.Y / viewportSize.Y,
					rightEye.X / viewportSize.X, rightEye.Y / viewportSize.Y));
				km.SetShaderParameter("item_eye_axis", across);
				km.SetShaderParameter("item_eye_span", span);
			}
			Vector2 start = nose - across * span * 0.11f - down * span * 0.08f;
			Vector2 px = Display.Size / new Vector2(viewport.Size.X, viewport.Size.Y);
			// Match the user's trace: a short rising arc above a separate lower stroke.
			// Use the projected eye axis so the ink follows the face and camera pan.
			Vector2 lower1 = start + across * span * 0.035f + down * span * 0.07f;
			Vector2 lower2 = start + across * span * 0.075f + down * span * 0.15f;
			Vector2 lower3 = start + across * span * 0.10f + down * span * 0.22f;
			Vector2 upper0 = start + across * span * 0.005f - down * span * 0.115f;
			Vector2 upper1 = start + across * span * 0.055f - down * span * 0.135f;
			Vector2 upper2 = start + across * span * 0.105f - down * span * 0.17f;
			Vector2 upper3 = start + across * span * 0.145f - down * span * 0.22f;
			for (int i = 0; i < nosePoints.Length; i++)
			{
				float t = i / (float)(nosePoints.Length - 1);
				nosePoints[i] = start.BezierInterpolate(lower1, lower2, lower3, t) * px;
				noseUpperPoints[i] = upper0.BezierInterpolate(upper1, upper2, upper3, t) * px;
			}
			noseLine.Points = nosePoints;
			noseUpper.Points = noseUpperPoints;
			noseLine.Width = noseUpper.Width = Mathf.Clamp(span * 0.014f, 1.35f, 1.8f) * px.Y;
			noseLine.Visible = noseUpper.Visible = true;
		}

		internal TextureRect Shadow(Vector2 offset, float opacity, float soft)
		{
			shadowMat.SetShaderParameter("cut", offset / Display.Size);
			shadowMat.SetShaderParameter("opacity", opacity);
			shadowMat.SetShaderParameter("soft", new Vector2(soft, soft) / Display.Size);
			return new TextureRect
			{
				Texture = Display.Texture, Material = shadowMat, Size = Display.Size, Position = Display.Position + offset,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
		}

		internal void Close()
		{
			if (Display.Material is ShaderMaterial km) km.SetShaderParameter("entry_full", 1f);
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
			DiveFx.Warm(parent);   // 单帧水花、遮罩、整段波浪图在空闲预热阶段实际画过，避免入水时才首次提交。
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
				// 拿牌 → 松手：两段首尾不连续，做混合过渡（另两处首尾本来就连续）；ITEM 进入 → 循环也按 UE 切换动作的默认混合过渡，避免跳
				if ((act == Act.Commu || act == Act.Item || act == Act.Skill) && phase == 0) { blendFrom = 0; blendTime = 0; }
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
			UpdateNoseLine();
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
