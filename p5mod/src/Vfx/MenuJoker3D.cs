using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 牌堆入场的原版菜单 3D Joker，游戏里实时渲染（用户 2026-11-24：入场都很卡、帧率低 —— 之前是原版录像 27.5fps 的 7–12 张帧图，
/// 和 60fps 以上的界面动画放在一起一格一格地跳）。现在按显示器帧率连续渲染：机位在逐帧拟合的关键帧之间样条插值，动作在原版两帧之间插值，
/// 关键帧时刻和原版逐帧一致（tools/p5/menu3d/fit），中间是真正的补间。
/// 数据（tools/p5/menu3d/export_runtime.py → res://p5joker/m3d/）：mesh.bin（网格、UV、颜色）+ 每个动作片段的逐帧顶点（float16）；
/// 着色同云端渲染 render_f16.gd：不打光、贴图原色、alpha 裁切。
/// 相机用离轴视锥：原版 1920×1080 屏幕坐标 → 我们排版的 1080p 坐标是缩放 + 平移（各页的 Map），换算成视锥的大小和偏移，
/// 渲染直接落在屏幕坐标上（不用裁帧图、不怕伸出原版画面边）。
/// </summary>
public sealed class MenuJoker3D
{
	/// <summary>一个关键帧：原版第 U 帧（页面自己的编号，0 起）、姿势（片段 A 第 FA 帧 往 片段 B 第 FB 帧 混合 W）、机位（x, y, z, 旋转 x, y, z 度, 竖直 fov）。</summary>
	public readonly record struct Key(float U, string A, float FA, string B, float FB, float W, float[] Cam);

	private sealed class Surface
	{
		public string Name = "";
		public int Count;
		public int[] Indices = Array.Empty<int>();
		public Vector2[]? Uv;
		public Color Color;
		public string Tex = "";
		public bool Cull;
		public int Clip;
	}

	private static List<Surface>? _surfaces;
	private static int _total;
	private static readonly Dictionary<string, float[]?> Clips = new();
	private static bool _failed;
	private const string Dir = "res://p5joker/m3d/";

	/// <summary>读网格（只读一次）；数据不在 pck 里就返回 false（页面退回不放 3D）。</summary>
	public static bool Available
	{
		get
		{
			if (_surfaces != null) return true;
			if (_failed) return false;
			try
			{
				byte[] b = FileAccess.GetFileAsBytes(Dir + "mesh.bin");
				if (b.Length < 12 || b[0] != 'M' || b[1] != '3' || b[2] != 'D' || b[3] != '1') { _failed = true; return false; }
				int o = 4;
				int n = BitConverter.ToInt32(b, o); o += 4;
				_total = BitConverter.ToInt32(b, o); o += 4;
				var list = new List<Surface>(n);
				for (int i = 0; i < n; i++)
				{
					var s = new Surface();
					int nl = BitConverter.ToInt32(b, o); o += 4;
					s.Name = System.Text.Encoding.UTF8.GetString(b, o, nl); o += nl;
					s.Count = BitConverter.ToInt32(b, o); o += 4;
					int ni = BitConverter.ToInt32(b, o); o += 4;
					s.Color = new Color(BitConverter.ToSingle(b, o), BitConverter.ToSingle(b, o + 4), BitConverter.ToSingle(b, o + 8), BitConverter.ToSingle(b, o + 12)); o += 16;
					int tl = BitConverter.ToInt32(b, o); o += 4;
					s.Tex = System.Text.Encoding.UTF8.GetString(b, o, tl); o += tl;
					s.Cull = b[o] != 0; s.Clip = b[o + 1]; o += 2;
					s.Indices = new int[ni];
					Buffer.BlockCopy(b, o, s.Indices, 0, ni * 4); o += ni * 4;
					if (tl > 0)
					{
						s.Uv = new Vector2[s.Count];
						for (int v = 0; v < s.Count; v++, o += 8) s.Uv[v] = new Vector2(BitConverter.ToSingle(b, o), BitConverter.ToSingle(b, o + 4));
					}
					list.Add(s);
				}
				_surfaces = list;
				return true;
			}
			catch (Exception e)
			{
				Stage.Report("menu 3d mesh", e);
				_failed = true;
				return false;
			}
		}
	}

	/// <summary>一个片段的全部顶点（[帧][顶点][xyz] float），第一次用时解码 float16。</summary>
	private static float[]? Clip(string name)
	{
		if (Clips.TryGetValue(name, out float[]? c)) return c;
		float[]? data = null;
		try
		{
			byte[] b = FileAccess.GetFileAsBytes(Dir + name + ".f16");
			if (b.Length >= _total * 6)
			{
				data = new float[b.Length / 2];
				for (int i = 0; i < data.Length; i++) data[i] = (float)BitConverter.ToHalf(b, i * 2);
			}
		}
		catch (Exception e)
		{
			Stage.Report("menu 3d clip " + name, e);
		}
		Clips[name] = data;
		return data;
	}

	/// <summary>预热：读网格和这些片段（打开牌堆时不用当场解码）。</summary>
	public static void Preload(params string[] clips)
	{
		if (!Available) return;
		foreach (string c in clips) Clip(c);
	}

	private readonly SubViewport _vp;
	private readonly Camera3D _cam;
	private readonly MeshInstance3D[] _inst;
	private readonly ArrayMesh[] _mesh;
	private readonly int[] _offset;
	private readonly bool[] _rebuild;
	private readonly Vector3[] _pos;
	private readonly Vector2 _view;
	private readonly float _k, _s;
	private readonly Vector2 _o;
	/// <summary>显示用的精灵（加到页面的节点树里，Z 序由页面决定）。</summary>
	public readonly Sprite2D Sprite;
	/// <summary>false = 不用精灵显示，由别的着色器直接采样 Texture（PileField：黑区里反色）。</summary>
	public bool UseSprite = true;
	/// <summary>渲染结果（屏幕大小、屏幕坐标对齐）。</summary>
	public Texture2D Texture => _vp.GetTexture();

	/// <summary>
	/// view：屏幕大小；k = view.Y / 1080；scale / offset：原版 1920×1080 坐标 → 我们排版的 1080p 坐标（p · scale + offset），再 × k 到屏幕。
	/// hide：不画的网格（道具：手机、卡片、书包、枪……）。
	/// </summary>
	public MenuJoker3D(Node parent, Vector2 view, float scale, Vector2 offset, ICollection<string> hide)
	{
		_view = view;
		_k = view.Y / 1080f;
		_s = scale;
		_o = offset;
		Vector2I px = new(Mathf.Max(2, Mathf.RoundToInt(view.X)), Mathf.Max(2, Mathf.RoundToInt(view.Y)));
		_vp = new SubViewport
		{
			Name = "P5MenuJoker3D", Size = px, TransparentBg = true, OwnWorld3D = true, Msaa3D = Viewport.Msaa.Msaa4X,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled, Disable3D = false,
		};
		parent.AddChild(_vp);
		_cam = new Camera3D { Projection = Camera3D.ProjectionType.Frustum, Near = 1f, Far = 2000f, Current = true };
		_vp.AddChild(_cam);
		var surfaces = _surfaces!;
		_inst = new MeshInstance3D[surfaces.Count];
		_mesh = new ArrayMesh[surfaces.Count];
		_offset = new int[surfaces.Count];
		_rebuild = new bool[surfaces.Count];
		_pos = new Vector3[_total];
		int cursor = 0;
		for (int i = 0; i < surfaces.Count; i++)
		{
			Surface s = surfaces[i];
			_offset[i] = cursor;
			cursor += s.Count;
			var mat = new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				CullMode = s.Cull ? BaseMaterial3D.CullModeEnum.Back : BaseMaterial3D.CullModeEnum.Disabled,
				AlbedoColor = s.Color,
			};
			if (s.Tex.Length > 0 && Fx.Tex("m3d_" + s.Tex) is Texture2D t)
			{
				mat.AlbedoColor = Colors.White;
				mat.AlbedoTexture = t;
				mat.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
				mat.AlphaScissorThreshold = Mathf.Max(0.001f, s.Clip / 255f);
				mat.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps;
			}
			_mesh[i] = new ArrayMesh();
			// 之后只更新顶点缓冲（MeshSurfaceUpdateVertexRegion），网格的包围盒还是第一次建的那一帧：动得远的部件（牌组页甩下来的枪）会被视锥剔掉。
			// 包围盒给死一个够大的（模型坐标，人在 ±200 以内）。
			_inst[i] = new MeshInstance3D { Mesh = _mesh[i], MaterialOverride = mat, Visible = !hide.Contains(s.Name), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
				CustomAabb = new Aabb(new Vector3(-500f, -500f, -500f), new Vector3(1000f, 1000f, 1000f)) };
			_vp.AddChild(_inst[i]);
		}
		Sprite = new Sprite2D { Name = "P5MenuJoker3DView", Texture = _vp.GetTexture(), Centered = false, Visible = false };
	}

	private void Build(int i)
	{
		Surface s = _surfaces![i];
		var arr = new Godot.Collections.Array();
		arr.Resize((int)Mesh.ArrayType.Max);
		var v = new Vector3[s.Count];
		Array.Copy(_pos, _offset[i], v, 0, s.Count);
		arr[(int)Mesh.ArrayType.Vertex] = v;
		arr[(int)Mesh.ArrayType.Index] = s.Indices;
		if (s.Uv != null) arr[(int)Mesh.ArrayType.TexUV] = s.Uv;
		_mesh[i].ClearSurfaces();
		_mesh[i].AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arr);
	}

	private bool _built;
	private byte[][]? _bytes;
	private float[] _flat = Array.Empty<float>();

	/// <summary>把顶点写进网格：第一次建网格；之后只更新顶点缓冲（顶点缓冲只有位置、步长 12 字节时；否则重建）。</summary>
	private void Upload()
	{
		var surfaces = _surfaces!;
		_bytes ??= new byte[surfaces.Count][];
		if (_flat.Length != _pos.Length * 3) _flat = new float[_pos.Length * 3];
		for (int v = 0; v < _pos.Length; v++) { _flat[v * 3] = _pos[v].X; _flat[v * 3 + 1] = _pos[v].Y; _flat[v * 3 + 2] = _pos[v].Z; }
		for (int i = 0; i < surfaces.Count; i++)
		{
			if (!_inst[i].Visible) continue;
			if (!_built || _rebuild[i])
			{
				Build(i);
				if (!_built)
				{
					var fmt = _mesh[i].SurfaceGetFormat(0);
					_rebuild[i] = RenderingServer.MeshSurfaceGetFormatVertexStride((RenderingServer.ArrayFormat)(long)fmt, surfaces[i].Count) != 12;
				}
				continue;
			}
			int bytes = surfaces[i].Count * 12;
			byte[] buf = _bytes[i] ??= new byte[bytes];
			Buffer.BlockCopy(_flat, _offset[i] * 12, buf, 0, bytes);
			RenderingServer.MeshSurfaceUpdateVertexRegion(_mesh[i].GetRid(), 0, 0, buf);
		}
		_built = true;
	}

	/// <summary>片段 c 第 f 帧（小数 = 相邻两帧线性插值）的顶点，按权重 w 累加进 _pos。</summary>
	private static void Accumulate(Vector3[] dst, float[] c, float f, float w, bool first)
	{
		int frames = c.Length / (_total * 3);
		float ff = Mathf.Clamp(f, 0f, frames - 1);
		int f0 = (int)MathF.Floor(ff), f1 = Math.Min(f0 + 1, frames - 1);
		float t = ff - f0;
		int a = f0 * _total * 3, b = f1 * _total * 3;
		for (int v = 0; v < _total; v++)
		{
			int o = v * 3;
			float x = c[a + o], y = c[a + o + 1], z = c[a + o + 2];
			if (t > 1e-4f)
			{
				x += (c[b + o] - x) * t; y += (c[b + o + 1] - y) * t; z += (c[b + o + 2] - z) * t;
			}
			Vector3 p = new Vector3(x, y, z) * w;
			dst[v] = first ? p : dst[v] + p;
		}
	}

	/// <summary>原版坐标里的机位 → 离轴视锥（渲染直接落在我们的屏幕坐标上）。</summary>
	private void SetCamera(float[] c)
	{
		_cam.Position = new Vector3(c[0], c[1], c[2]);
		_cam.RotationDegrees = new Vector3(c[3], c[4], c[5]);
		float f = 540f / MathF.Tan(Mathf.DegToRad(c[6]) * 0.5f);         // 原版像素焦距
		// 屏幕 (xs, ys) ↔ 原版 ((xs / k − o.x) / s, (ys / k − o.y) / s)；像平面（距离 1）坐标 X = (xo − 960) / f，Y = −(yo − 540) / f
		float x0 = ((0f / _k - _o.X) / _s - 960f) / f, x1 = ((_view.X / _k - _o.X) / _s - 960f) / f;
		float y0 = -((0f / _k - _o.Y) / _s - 540f) / f, y1 = -((_view.Y / _k - _o.Y) / _s - 540f) / f;
		float near = _cam.Near;
		_cam.Size = (y0 - y1) * near;
		_cam.FrustumOffset = new Vector2((x0 + x1) * 0.5f * near, (y0 + y1) * 0.5f * near);
	}

	/// <summary>渲染这一帧的姿势和机位（每个进程帧调一次；之后精灵显示的就是这一帧）。</summary>
	public void Show(string a, float fa, string b, float fb, float w, float[] cam)
	{
		if (Clip(a) is not float[] ca) return;
		Accumulate(_pos, ca, fa, 1f - w, true);
		if (w > 1e-4f && Clip(b) is float[] cb) Accumulate(_pos, cb, fb, w, false);
		else if (w > 1e-4f) Accumulate(_pos, ca, fa, w, false);
		Upload();
		SetCamera(cam);
		_vp.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
		Sprite.Visible = UseSprite;
	}

	/// <summary>原版第 u 帧（小数）的姿势和机位：机位 Catmull-Rom 样条、动作线性插值（关键帧必须按 U 排好、相邻帧同片段）。</summary>
	public void Show(Key[] keys, float u)
	{
		if (keys.Length == 0) return;
		u = Mathf.Clamp(u, keys[0].U, keys[^1].U);
		int i = 0;
		while (i < keys.Length - 2 && keys[i + 1].U <= u) i++;
		Key k1 = keys[i], k2 = keys[Math.Min(i + 1, keys.Length - 1)];
		Key k0 = keys[Math.Max(i - 1, 0)], k3 = keys[Math.Min(i + 2, keys.Length - 1)];
		float t = k2.U > k1.U ? Mathf.Clamp((u - k1.U) / (k2.U - k1.U), 0f, 1f) : 0f;
		float[] cam = new float[7];
		for (int j = 0; j < 7; j++)
		{
			float p0 = k0.Cam[j], p1 = k1.Cam[j], p2 = k2.Cam[j], p3 = k3.Cam[j];
			cam[j] = 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t);
		}
		Show(k1.A, Mathf.Lerp(k1.FA, k2.FA, t), k1.B, Mathf.Lerp(k1.FB, k2.FB, t), Mathf.Lerp(k1.W, k2.W, t), cam);
	}

	/// <summary>不再显示：停止渲染。</summary>
	public void Hide()
	{
		Sprite.Visible = false;
		_vp.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
	}

	/// <summary>预热用：在一个小画面里画一帧（编译 3D 材质的着色器）。</summary>
	public static void Warm(Control slot)
	{
		if (!Available) return;
		Preload("main_0000", "main_0005", "main_0010", "main_0015", "main_0016");
		var m = new MenuJoker3D(slot, new Vector2(64f, 36f), 1f, Vector2.Zero, Array.Empty<string>());
		slot.AddChild(m.Sprite);
		m.Show("main_0015", 0f, "main_0015", 0f, 0f, new float[] { -57f, 44f, 73.6f, 41.2f, -48.9f, 11.1f, 40.5f });
	}
}
