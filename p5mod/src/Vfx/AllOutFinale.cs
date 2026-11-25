using System;
using System.Collections.Generic;
using System.IO;
using Godot;

namespace P5Joker;

/// <summary>
/// P5R 总攻击收尾（BKSK_HERO_CTE：红底、碎星、Joker、竖排 THE SHOW'S OVER）的原版数据实时重放。
/// 数据由 tools/p5/allout_native/export_runtime.py 从解码好的原版网格 / 材质 / 240 帧节点矩阵打成 res://p5joker/allout/hero.bin，
/// 规则同 tools/p5/allout_native/godot/capture.gd（原生剔除、相机 bank、下底板关闭）。人物、碎星都是原版的矢量网格，任何分辨率都不糊。
/// 分三块离屏画面（各自 3D 世界、同一个相机）：背景（碎星 + 红底板）、人物、竖排字 —— 总攻击把怪物剪影夹在背景和人物之间。
/// Seek(源时间) 设定当前帧：源时间 2.0 之前整屏红底，2.133 竖排字、2.2 人物和碎星出现（原版 30fps 第 64 / 66 帧）。
/// </summary>
public sealed class AllOutFinale
{
	public const string DataPath = "res://p5joker/allout/hero.bin";
	public const float RedAt = 2.0f, TextAt = 2.1333f, ArtAt = 2.2f, End = 4.0f;

	/// <summary>读数据（预览工具换成仓库里的文件）。</summary>
	public static Func<byte[]?> LoadData = () =>
	{
		using Godot.FileAccess? f = Godot.FileAccess.Open(DataPath, Godot.FileAccess.ModeFlags.Read);
		return f?.GetBuffer((long)f.GetLength());
	};

	private sealed class Mat { public Color Color; public bool Cull; public string Tex = ""; }
	private sealed class MeshData { public int Node, Mat; public Vector3[] Verts = Array.Empty<Vector3>(); public Vector2[] Uv = Array.Empty<Vector2>(); public int[] Idx = Array.Empty<int>(); }
	private sealed class Layer
	{
		public Transform3D Place;
		public float Start, End;
		public bool Hidden, Ortho;
		public Color[] Tint = Array.Empty<Color>();
		public int[] Parent = Array.Empty<int>();
		public Mat[] Mats = Array.Empty<Mat>();
		public MeshData[] Meshes = Array.Empty<MeshData>();
		public Transform3D[,] Frames = new Transform3D[0, 0];
	}
	private sealed class Data
	{
		public float Fps, Fov, Near, Far;
		public int Frames, CamLayer, CamNode;
		public float[] Bank = Array.Empty<float>();
		public Layer[] Layers = Array.Empty<Layer>();
	}

	private static Data? _data;
	private static bool _failed;

	private static Data? Load()
	{
		if (_data != null || _failed) return _data;
		try
		{
			byte[]? raw = LoadData();
			if (raw == null || raw.Length < 16) { _failed = true; return null; }
			using BinaryReader r = new(new MemoryStream(raw));
			if (new string(r.ReadChars(4)) != "AOA1") { _failed = true; return null; }
			Data d = new() { Fps = r.ReadSingle(), Frames = r.ReadInt32() };
			d.Fov = r.ReadSingle(); d.Near = r.ReadSingle(); d.Far = r.ReadSingle();
			d.CamLayer = r.ReadInt32(); d.CamNode = r.ReadInt32();
			d.Bank = new float[d.Frames];
			for (int i = 0; i < d.Frames; i++) d.Bank[i] = r.ReadSingle();
			d.Layers = new Layer[r.ReadInt32()];
			for (int li = 0; li < d.Layers.Length; li++)
			{
				Layer l = new();
				Vector3 x = V3(r), y = V3(r), z = V3(r), o = V3(r);
				l.Place = new Transform3D(new Basis(x, y, z), o);
				l.Start = r.ReadSingle(); l.End = r.ReadSingle();
				l.Hidden = r.ReadInt32() != 0; l.Ortho = r.ReadInt32() != 0;
				l.Tint = new Color[d.Frames];
				for (int i = 0; i < d.Frames; i++) l.Tint[i] = new Color(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
				int nodes = r.ReadInt32();
				l.Parent = new int[nodes];
				for (int i = 0; i < nodes; i++) l.Parent[i] = r.ReadInt32();
				l.Mats = new Mat[r.ReadInt32()];
				for (int i = 0; i < l.Mats.Length; i++)
				{
					Mat m = new() { Color = new Color(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle()), Cull = r.ReadInt32() != 0 };
					m.Tex = System.Text.Encoding.UTF8.GetString(r.ReadBytes(r.ReadInt32()));
					l.Mats[i] = m;
				}
				l.Meshes = new MeshData[r.ReadInt32()];
				for (int i = 0; i < l.Meshes.Length; i++)
				{
					MeshData m = new() { Node = r.ReadInt32(), Mat = r.ReadInt32() };
					m.Verts = new Vector3[r.ReadInt32()];
					for (int k = 0; k < m.Verts.Length; k++) m.Verts[k] = V3(r);
					m.Uv = new Vector2[r.ReadInt32()];
					for (int k = 0; k < m.Uv.Length; k++) m.Uv[k] = new Vector2(r.ReadSingle(), r.ReadSingle());
					m.Idx = new int[r.ReadInt32()];
					for (int k = 0; k < m.Idx.Length; k++) m.Idx[k] = r.ReadInt32();
					l.Meshes[i] = m;
				}
				l.Frames = new Transform3D[d.Frames, nodes];
				for (int f = 0; f < d.Frames; f++)
				{
					for (int k = 0; k < nodes; k++)
					{
						Vector3 a = V3(r), b = V3(r), c = V3(r), t = V3(r);
						l.Frames[f, k] = new Transform3D(new Basis(a, b, c), t);
					}
				}
				d.Layers[li] = l;
			}
			_data = d;
		}
		catch (Exception e)
		{
			_failed = true;
			Stage.Report("allout finale data", e);
		}
		return _data;
	}

	private static Vector3 V3(BinaryReader r) => new(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

	internal static void Preload() => Load();

	/// <summary>四块画面（全屏 TextureRect）：红底板、碎星（含网点 / 血花 / 白色笔触）、人物、竖排字。调用方按顺序摆进自己的层里、各自做入场。</summary>
	public readonly TextureRect Bg, Star, Front, Text;

	private readonly Data _d;
	private readonly List<SubViewport> _views = new();
	private readonly Camera3D[] _cams;
	private readonly Node3D[][] _nodes;
	private readonly Node3D?[] _holders;
	private readonly List<(StandardMaterial3D Mat, Color Base, int Layer)> _mats = new();

	// 四块各放哪些层：0 = 相机（无网格），1 = 碎星背景，2 = 人物，3 = 上底板（红），4 = 下底板（关闭），5 = 竖排字
	private static readonly int[][] Groups = { new[] { 3 }, new[] { 1 }, new[] { 2 }, new[] { 5 } };
	private const int TextGroup = 3;
	private readonly bool[] _shown = new bool[Groups.Length];
	private readonly bool[] _layerVisible;
	private int _sourceFrame = -1, _warmView;
	private readonly Node _owner;
	private readonly Vector2I _pixels, _flatPixels;

	private AllOutFinale(Data d, Node owner, Vector2 size)
	{
		_d = d;
		_nodes = new Node3D[d.Layers.Length][];
		_holders = new Node3D?[d.Layers.Length];
		_layerVisible = new bool[d.Layers.Length];
		_owner = owner;
		_cams = new Camera3D[Groups.Length];
		// 用户 2026-11-19：立绘边缘毛糙、有锯齿 —— 之前四块离屏画面都压在 1280×720 再放大到屏幕上。
		// 人物 / 碎星 / 竖排字按屏幕原分辨率画（最高 2560×1440）+ 4× MSAA；只有整块平涂的红底板还用 1280×720 省开销。
		float scale = Mathf.Min(1f, Mathf.Min(2560f / size.X, 1440f / size.Y));
		_pixels = new(Mathf.Max(64, Mathf.RoundToInt(size.X * scale)), Mathf.Max(36, Mathf.RoundToInt(size.Y * scale)));
		float flat = Mathf.Min(1f, Mathf.Min(1280f / size.X, 720f / size.Y));
		_flatPixels = new(Mathf.Max(64, Mathf.RoundToInt(size.X * flat)), Mathf.Max(36, Mathf.RoundToInt(size.Y * flat)));
		Bg = MakeRect(size, 0);
		Star = MakeRect(size, 1);
		Front = MakeRect(size, 2);
		Text = MakeRect(size, TextGroup);
	}

	private static TextureRect MakeRect(Vector2 size, int group) => new()
	{
		Size = size, StretchMode = TextureRect.StretchModeEnum.Scale,
		MouseFilter = Control.MouseFilterEnum.Ignore, Name = $"P5AllOutLayer{group}",
	};

	// Allocate one 3D layer during each frame of the glass animation.
	public void PrepareNextView()
	{
		int g = _views.Count;
		if (g >= Groups.Length) return;
		SubViewport vp = new()
		{
			Size = g == 0 ? _flatPixels : _pixels, TransparentBg = true, OwnWorld3D = true, Msaa3D = g == 0 ? Viewport.Msaa.Msaa2X : Viewport.Msaa.Msaa4X,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled, Name = $"P5AllOutView{g}",
		};
		_owner.AddChild(vp);
		_views.Add(vp);
		Node3D world = new();
		vp.AddChild(world);
		foreach (int li in Groups[g]) Build(li, world);
		Camera3D cam = g == TextGroup
			? new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 720f, Position = new Vector3(0f, 0f, 2000f), Far = 5000f, Current = true }
			: new Camera3D { Near = _d.Near, Far = _d.Far, Fov = _d.Fov, Current = true };
		vp.AddChild(cam);
		_cams[g] = cam;
		TextureRect rect = g switch { 0 => Bg, 1 => Star, 2 => Front, _ => Text };
		rect.Texture = vp.GetTexture();
		if (_views.Count == Groups.Length) Seek(ArtAt, textVisible: true, present: false);
	}

	public void CompletePreparation()
	{
		while (_views.Count < Groups.Length) PrepareNextView();
	}

	/// <summary>建好三块离屏画面（挂在 owner 下，跟着 owner 释放）；数据缺失时 null。</summary>
	public static AllOutFinale? Create(Node owner, Vector2 size)
	{
		Data? d = Load();
		if (d == null) return null;
		try
		{
			return new AllOutFinale(d, owner, size);
		}
		catch (Exception e)
		{
			Stage.Report("allout finale build", e);
			return null;
		}
	}

	private void Build(int li, Node3D world)
	{
		Layer l = _d.Layers[li];
		Node3D holder = new() { Transform = l.Place, Visible = false };
		world.AddChild(holder);
		_holders[li] = holder;
		int n = l.Parent.Length;
		Node3D[] nodes = new Node3D[n];
		for (int k = 0; k < n; k++)
		{
			nodes[k] = new Node3D();
			(l.Parent[k] < 0 ? holder : nodes[l.Parent[k]]).AddChild(nodes[k]);
		}
		_nodes[li] = nodes;
		StandardMaterial3D?[] mats = new StandardMaterial3D?[l.Mats.Length];
		for (int i = 0; i < l.Mats.Length; i++)
		{
			Mat m = l.Mats[i];
			StandardMaterial3D sm = new()
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				CullMode = m.Cull ? BaseMaterial3D.CullModeEnum.Back : BaseMaterial3D.CullModeEnum.Disabled,
				AlbedoColor = m.Color,
				Transparency = m.Color.A < 0.99f ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
			};
			if (m.Tex.Length > 0 && Stage.LoadTexture($"aoa_{m.Tex}") is Texture2D tex)
			{
				sm.AlbedoTexture = tex;
			}
			mats[i] = sm;
			_mats.Add((sm, m.Color, li));
		}
		foreach (MeshData md in l.Meshes)
		{
			var arrays = new Godot.Collections.Array();
			arrays.Resize((int)Mesh.ArrayType.Max);
			arrays[(int)Mesh.ArrayType.Vertex] = md.Verts;
			if (md.Uv.Length == md.Verts.Length && md.Uv.Length > 0) arrays[(int)Mesh.ArrayType.TexUV] = md.Uv;
			arrays[(int)Mesh.ArrayType.Index] = md.Idx;
			ArrayMesh mesh = new();
			mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
			MeshInstance3D mi = new() { Mesh = mesh, MaterialOverride = md.Mat >= 0 ? mats[md.Mat] : null };
			nodes[md.Node].AddChild(mi);
		}
	}

	// Warm one render target per frame while the glass animation is playing.
	public void WarmNextView()
	{
		if (_views.Count == Groups.Length && _warmView < _views.Count)
			_views[_warmView++].RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
	}

	/// <summary>Refresh the 3D layers only when a visible layer changes or advances to a new source frame.</summary>
	public void Seek(float t, bool textVisible, bool present = true)
	{
		int f = Mathf.Clamp(Mathf.RoundToInt(t * _d.Fps), 0, _d.Frames - 1);
		bool frameChanged = f != _sourceFrame;
		_sourceFrame = f;
		for (int g = 0; g < Groups.Length; g++)
		{
			bool show = present && (g != TextGroup || textVisible);
			bool redraw = frameChanged || !_shown[g];
			foreach (int li in Groups[g])
			{
				Layer l = _d.Layers[li];
				Color tint = l.Tint[f];
				bool visible = !l.Hidden && t + 1e-6f >= l.Start && t < l.End && tint.A > 1e-5f;
				if (_layerVisible[li] != visible)
				{
					_layerVisible[li] = visible;
					_holders[li]!.Visible = visible;
					redraw = true;
				}
				if (frameChanged)
				{
					Node3D[] nodes = _nodes[li];
					for (int k = 0; k < nodes.Length; k++) nodes[k].Transform = l.Frames[f, k];
				}
			}
			_shown[g] = show;
			if (show && redraw) _views[g].RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
		}
		if (!frameChanged) return;
		foreach (var (mat, b, li) in _mats)
		{
			Color tint = _d.Layers[li].Tint[f];
			float a = b.A * tint.A;
			Color color = new(b.R * tint.R, b.G * tint.G, b.B * tint.B, a);
			if (mat.AlbedoColor != color) mat.AlbedoColor = color;
			var alpha = a < 0.999f ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled;
			if (mat.Transparency != alpha) mat.Transparency = alpha;
		}
		Layer cl = _d.Layers[_d.CamLayer];
		Transform3D w = Transform3D.Identity;
		for (int k = _d.CamNode; k >= 0; k = cl.Parent[k]) w = cl.Frames[f, k] * w;
		w = cl.Place * w;
		w.Basis = w.Basis.Orthonormalized();
		w = w.RotatedLocal(Vector3.Forward, Mathf.DegToRad(_d.Bank[f]));
		for (int g = 0; g < Groups.Length; g++) if (g != TextGroup) _cams[g].GlobalTransform = w;
	}

	public void Free()
	{
		if (GodotObject.IsInstanceValid(Bg)) Bg.Hide();
		if (GodotObject.IsInstanceValid(Star)) Star.Hide();
		if (GodotObject.IsInstanceValid(Front)) Front.Hide();
		if (GodotObject.IsInstanceValid(Text)) Text.Hide();
		foreach (SubViewport v in _views)
		{
			if (GodotObject.IsInstanceValid(v)) v.QueueFree();
		}
		_views.Clear();
	}
}
