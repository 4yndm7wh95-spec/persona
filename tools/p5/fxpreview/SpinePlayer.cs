using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// 预览用的最小 Spine 播放器：只认我们自己的 Joker 骨骼（tools/p5/build_rig_p5.py 的输出）用到的东西——
/// 骨骼 translate / rotate / scale（线性，没有曲线）、插槽换图（阶梯）、插槽 rgba（线性）、区域图和带权重的网格。
/// 不做动画之间的混合（游戏里是 0.05s 左右的交叉淡化，看特效节奏够用了）。
/// 坐标：骨骼空间 y 朝上；节点自己的 Scale 设成 (0.29, -0.29) 就和游戏里 Visuals 一样大、方向一样。
/// </summary>
public partial class SpinePlayer : Node2D
{
	private sealed class Bone
	{
		public string Name = "";
		public int Parent = -1;
		public float X, Y, Rot, Sx = 1f, Sy = 1f;
		public Transform2D World;
	}

	private sealed class Attachment
	{
		public bool Mesh;
		public Rect2 Region;
		// 区域图
		public float X, Y, Rot, Sx = 1f, Sy = 1f, W, H;
		// 网格：每个顶点若干 (骨骼, x, y, 权重)
		public (int Bone, Vector2 P, float W)[][] Verts = Array.Empty<(int, Vector2, float)[]>();
		public Vector2[] Uvs = Array.Empty<Vector2>();
		public int[] Tris = Array.Empty<int>();
	}

	private sealed class SlotDef
	{
		public string Name = "";
		public int Bone;
		public string? Setup;
	}

	private sealed class Anim
	{
		public float Duration;
		public Dictionary<string, List<(float T, string? Name)>> Attach = new();
		public Dictionary<string, List<(float T, Color C)>> Rgba = new();
		public Dictionary<string, Dictionary<string, List<(float T, float A, float B)>>> Bones = new();
	}

	private readonly List<Bone> _bones = new();
	private readonly List<SlotDef> _slots = new();
	private readonly Dictionary<string, Dictionary<string, Attachment>> _skin = new();
	private readonly Dictionary<string, Anim> _anims = new();
	private Texture2D _tex = null!;

	private string _current = "idle_loop";
	private float _time;
	private bool _loop = true;
	private readonly Queue<(string Name, bool Loop)> _queue = new();

	public static SpinePlayer Load(string jsonPath, string atlasPath, string pngPath)
	{
		SpinePlayer p = new();
		p._tex = ImageTexture.CreateFromImage(Image.LoadFromFile(pngPath));
		var regions = new Dictionary<string, Rect2>();
		string? name = null;
		foreach (string raw in System.IO.File.ReadAllLines(atlasPath).Skip(1))
		{
			string line = raw.Trim();
			if (line.StartsWith("bounds:") && name != null)
			{
				float[] v = line[7..].Split(',').Select(float.Parse).ToArray();
				regions[name] = new Rect2(v[0], v[1], v[2], v[3]);
			}
			else if (line.Length > 0 && !line.Contains(':'))
			{
				name = line;
			}
		}
		var root = Json.ParseString(System.IO.File.ReadAllText(jsonPath)).AsGodotDictionary();
		foreach (var bv in root["bones"].AsGodotArray())
		{
			var b = bv.AsGodotDictionary();
			Bone bone = new() { Name = (string)b["name"] };
			if (b.ContainsKey("parent"))
			{
				bone.Parent = p._bones.FindIndex(x => x.Name == (string)b["parent"]);
			}
			bone.X = F(b, "x"); bone.Y = F(b, "y"); bone.Rot = F(b, "rotation");
			bone.Sx = F(b, "scaleX", 1f); bone.Sy = F(b, "scaleY", 1f);
			p._bones.Add(bone);
		}
		foreach (var sv in root["slots"].AsGodotArray())
		{
			var s = sv.AsGodotDictionary();
			p._slots.Add(new SlotDef
			{
				Name = (string)s["name"],
				Bone = p._bones.FindIndex(x => x.Name == (string)s["bone"]),
				Setup = s.ContainsKey("attachment") ? (string)s["attachment"] : null,
			});
		}
		var skin = root["skins"].AsGodotArray()[0].AsGodotDictionary()["attachments"].AsGodotDictionary();
		foreach (var (slotName, atts) in skin)
		{
			var map = new Dictionary<string, Attachment>();
			foreach (var (attName, av) in atts.AsGodotDictionary())
			{
				var a = av.AsGodotDictionary();
				string key = (string)attName;
				Attachment at = new() { Region = regions.GetValueOrDefault(key) };
				if (a.ContainsKey("type") && (string)a["type"] == "mesh")
				{
					at.Mesh = true;
					float[] uvs = a["uvs"].AsFloat32Array();
					at.Uvs = new Vector2[uvs.Length / 2];
					for (int i = 0; i < at.Uvs.Length; i++)
					{
						at.Uvs[i] = new Vector2(at.Region.Position.X + uvs[i * 2] * at.Region.Size.X, at.Region.Position.Y + uvs[i * 2 + 1] * at.Region.Size.Y);
					}
					at.Tris = a["triangles"].AsInt32Array();
					float[] v = a["vertices"].AsFloat32Array();
					var verts = new List<(int, Vector2, float)[]>();
					for (int i = 0; i < v.Length;)
					{
						int n = (int)v[i++];
						var w = new (int, Vector2, float)[n];
						for (int k = 0; k < n; k++, i += 4)
						{
							w[k] = ((int)v[i], new Vector2(v[i + 1], v[i + 2]), v[i + 3]);
						}
						verts.Add(w);
					}
					at.Verts = verts.ToArray();
				}
				else
				{
					at.X = F(a, "x"); at.Y = F(a, "y"); at.Rot = F(a, "rotation");
					at.Sx = F(a, "scaleX", 1f); at.Sy = F(a, "scaleY", 1f);
					at.W = F(a, "width"); at.H = F(a, "height");
				}
				map[key] = at;
			}
			p._skin[(string)slotName] = map;
		}
		foreach (var (animName, anv) in root["animations"].AsGodotDictionary())
		{
			var an = anv.AsGodotDictionary();
			Anim anim = new();
			if (an.ContainsKey("slots"))
			{
				foreach (var (slotName, tv) in an["slots"].AsGodotDictionary())
				{
					var t = tv.AsGodotDictionary();
					if (t.ContainsKey("attachment"))
					{
						anim.Attach[(string)slotName] = t["attachment"].AsGodotArray().Select(k => k.AsGodotDictionary())
							.Select(k => (F(k, "time"), k["name"].VariantType == Variant.Type.Nil ? null : (string?)(string)k["name"])).ToList();
					}
					if (t.ContainsKey("rgba"))
					{
						anim.Rgba[(string)slotName] = t["rgba"].AsGodotArray().Select(k => k.AsGodotDictionary())
							.Select(k => (F(k, "time"), Color.FromHtml((string)k["color"]))).ToList();
					}
				}
			}
			if (an.ContainsKey("bones"))
			{
				foreach (var (boneName, tv) in an["bones"].AsGodotDictionary())
				{
					var tl = new Dictionary<string, List<(float, float, float)>>();
					foreach (var (kind, keys) in tv.AsGodotDictionary())
					{
						tl[(string)kind] = keys.AsGodotArray().Select(k => k.AsGodotDictionary()).Select(k => (string)kind switch
						{
							"rotate" => (F(k, "time"), F(k, "value"), 0f),
							"scale" => (F(k, "time"), F(k, "x", 1f), F(k, "y", 1f)),
							_ => (F(k, "time"), F(k, "x"), F(k, "y")),
						}).ToList();
					}
					anim.Bones[(string)boneName] = tl;
				}
			}
			anim.Duration = anim.Attach.Values.SelectMany(l => l.Select(k => k.T))
				.Concat(anim.Rgba.Values.SelectMany(l => l.Select(k => k.T)))
				.Concat(anim.Bones.Values.SelectMany(d => d.Values.SelectMany(l => l.Select(k => k.T)))).DefaultIfEmpty(0f).Max();
			p._anims[(string)animName] = anim;
		}
		return p;
	}

	private static float F(Godot.Collections.Dictionary d, string key, float def = 0f) => d.ContainsKey(key) ? (float)d[key].AsDouble() : def;

	public bool Has(string anim) => _anims.ContainsKey(anim);

	/// <summary>马上切到 anim；不循环的放完自动回 idle_loop（或者先放队列里的）。</summary>
	public void Play(string anim, bool loop = false)
	{
		if (!_anims.ContainsKey(anim))
		{
			GD.PrintErr($"[P5Joker] no animation {anim}");
			return;
		}
		_current = anim;
		_loop = loop;
		_time = 0f;
		_queue.Clear();
	}

	/// <summary>当前动画（游戏里 GetCurrentAnimationName）和它播到第几秒（GetTrackTime）。</summary>
	public string Current => _current;
	public float Time => _time;

	/// <summary>换成 anim 并从 time 秒处接着播（游戏里 SetTrackTime）。</summary>
	public void PlayFrom(string anim, float time)
	{
		Play(anim);
		_time = time;
	}

	public void Queue(string anim, bool loop = false) => _queue.Enqueue((anim, loop));

	public override void _Process(double delta)
	{
		_time += (float)delta;
		Anim a = _anims[_current];
		if (_time >= a.Duration)
		{
			if (_loop)
			{
				_time = a.Duration > 0f ? _time % a.Duration : 0f;
			}
			else
			{
				(string next, bool loop) = _queue.Count > 0 ? _queue.Dequeue() : ("idle_loop", true);
				_current = next;
				_loop = loop;
				_time = 0f;
			}
		}
		QueueRedraw();
	}

	public override void _Draw()
	{
		Anim a = _anims[_current];
		float t = _time;
		for (int i = 0; i < _bones.Count; i++)
		{
			Bone b = _bones[i];
			float x = b.X, y = b.Y, r = b.Rot, sx = b.Sx, sy = b.Sy;
			if (a.Bones.TryGetValue(b.Name, out var tl))
			{
				if (tl.TryGetValue("translate", out var tr)) { var v = Sample(tr, t); x += v.A; y += v.B; }
				if (tl.TryGetValue("rotate", out var ro)) { r += Sample(ro, t).A; }
				if (tl.TryGetValue("scale", out var sc)) { var v = Sample(sc, t); sx *= v.A; sy *= v.B; }
			}
			Transform2D local = new(Mathf.DegToRad(r), new Vector2(sx, sy), 0f, new Vector2(x, y));
			b.World = b.Parent >= 0 ? _bones[b.Parent].World * local : local;
		}
		foreach (SlotDef s in _slots)
		{
			string? attName = s.Setup;
			if (a.Attach.TryGetValue(s.Name, out var keys))
			{
				foreach (var k in keys)
				{
					if (k.T <= t) attName = k.Name; else break;
				}
			}
			if (attName == null || !_skin.TryGetValue(s.Name, out var map) || !map.TryGetValue(attName, out Attachment? at))
			{
				continue;
			}
			Color c = Colors.White;
			if (a.Rgba.TryGetValue(s.Name, out var ck))
			{
				c = SampleColor(ck, t);
			}
			if (c.A <= 0.001f)
			{
				continue;
			}
			DrawAttachment(at, _bones[s.Bone].World, c);
		}
	}

	private void DrawAttachment(Attachment at, Transform2D bone, Color c)
	{
		Vector2[] pts;
		Vector2[] uvs;
		int[] idx;
		if (at.Mesh)
		{
			pts = new Vector2[at.Verts.Length];
			for (int i = 0; i < pts.Length; i++)
			{
				Vector2 p = Vector2.Zero;
				foreach (var (bi, lp, w) in at.Verts[i])
				{
					p += _bones[bi].World * lp * w;
				}
				pts[i] = p;
			}
			uvs = at.Uvs;
			idx = at.Tris;
		}
		else
		{
			Transform2D m = bone * new Transform2D(Mathf.DegToRad(at.Rot), new Vector2(at.Sx, at.Sy), 0f, new Vector2(at.X, at.Y));
			float hw = at.W / 2f, hh = at.H / 2f;
			pts = new[] { m * new Vector2(-hw, hh), m * new Vector2(hw, hh), m * new Vector2(hw, -hh), m * new Vector2(-hw, -hh) };
			Rect2 rg = at.Region;
			uvs = new[] { rg.Position, rg.Position + new Vector2(rg.Size.X, 0f), rg.End, rg.Position + new Vector2(0f, rg.Size.Y) };
			idx = new[] { 0, 1, 2, 0, 2, 3 };
		}
		Vector2 texSize = _tex.GetSize();
		Vector2[] nuv = uvs.Select(u => u / texSize).ToArray();
		Color[] cols = Enumerable.Repeat(c, pts.Length).ToArray();
		RenderingServer.CanvasItemAddTriangleArray(GetCanvasItem(), idx, pts, cols, nuv, texture: _tex.GetRid());
	}

	private static (float A, float B) Sample(List<(float T, float A, float B)> keys, float t)
	{
		if (t <= keys[0].T) return (keys[0].A, keys[0].B);
		for (int i = 1; i < keys.Count; i++)
		{
			if (t < keys[i].T)
			{
				var k0 = keys[i - 1]; var k1 = keys[i];
				float u = (t - k0.T) / (k1.T - k0.T);
				return (Mathf.Lerp(k0.A, k1.A, u), Mathf.Lerp(k0.B, k1.B, u));
			}
		}
		return (keys[^1].A, keys[^1].B);
	}

	private static Color SampleColor(List<(float T, Color C)> keys, float t)
	{
		if (t < keys[0].T) return keys[0].C;
		for (int i = 1; i < keys.Count; i++)
		{
			if (t < keys[i].T)
			{
				var k0 = keys[i - 1]; var k1 = keys[i];
				return k1.T <= k0.T ? k1.C : k0.C.Lerp(k1.C, (t - k0.T) / (k1.T - k0.T));
			}
		}
		return keys[^1].C;
	}
}
