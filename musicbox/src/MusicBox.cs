using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Godot;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Saves;

namespace P3MusicBox;

/// <summary>
/// P3 音乐盒：右上角设置（齿轮）按钮下方的小圆按钮。
/// · 默认不播放；左键 播放 / 暂停，右键 打开歌单（点歌切换、播放模式、歌词开关、音量）；
/// · 播放时原版背景音乐淡出到静音，暂停后淡回原来的音量；播放 / 暂停 / 切歌都有淡入淡出；
/// · 歌曲放在本 mod 文件夹的 music\ 里（.ogg / .mp3；同名 .lrc 显示歌词；playlist.json 决定顺序和标题，没有就按文件名）。
/// · 纯界面 + 本地播放，不影响玩法（清单里 affects_gameplay=false），联机时只装在自己这边也可以。
/// 不重写 Godot 虚函数（mod 程序集没有 Godot 源生成器，_Draw / _GuiInput 之类不会被调用），全部用普通节点 + 信号 + 每帧回调。
/// </summary>
[ModInitializer("Init")]
public static class MusicBox
{
	public const string Tag = "[P3MusicBox]";
	public const string Version = "1.0.0";

	// 配色（和 P3Makoto 一致的 P3R 蓝）
	private static readonly Color Navy = new(0.02f, 0.06f, 0.22f, 0.94f);
	private static readonly Color Cyan = new(0.46f, 0.83f, 0.97f);
	private static readonly Color Blue = new(0.09f, 0.41f, 0.85f);

	private const float FadeIn = 0.9f, FadeOut = 0.6f, SwitchOut = 0.35f, BgmFade = 1.5f;

	private sealed class Song
	{
		public string Title = "", Artist = "", File = "";
		public string? Lrc;
		public List<(double At, string Text)>? Lines;
	}

	private sealed class Prefs
	{
		public float Volume { get; set; } = 0.8f;
		public int Mode { get; set; }                 // 0 顺序循环 1 单曲循环 2 随机
		public bool Lyrics { get; set; } = true;
		public int Last { get; set; }
	}

	private enum State { Stopped, Playing, Paused }

	private static readonly List<Song> Songs = new();
	private static Prefs _prefs = new();
	private static State _state = State.Stopped;
	private static int _index;
	private static float _fade, _fadeTarget, _fadeSpeed = 1f;
	private static Action? _afterFadeOut;
	private static float _bgm = 1f, _lastBgmApplied = -1f;
	private static float _focus = 1f;
	private static ulong _lastTick, _lastBgmPush, _lastNote;
	private static string _modDir = "";

	private static CanvasLayer? _layer;
	private static AudioStreamPlayer? _player;
	private static Control? _button;
	private static Node2D? _icon, _ring;
	private static Line2D? _rim;
	private static Label? _glyph, _hoverTitle;
	private static Label[] _lyric = Array.Empty<Label>();
	private static int _lyricSlot;
	private static string _lyricShown = "";
	private static Control? _popup, _catcher;
	private static VBoxContainer? _list;
	private static Button? _modeButton, _lyricsButton;
	private static Label? _nowLabel;
	private static bool _hover;
	private static Font? _font;

	private static string PrefsPath => ProjectSettings.GlobalizePath("user://p3musicbox.json");

	public static void Init()
	{
		GD.Print($"{Tag} init v{Version}");
		Safe("init", () =>
		{
			_modDir = Path.GetDirectoryName(typeof(MusicBox).Assembly.Location) ?? "";
			LoadPrefs();
			ScanMusic();
			if (Engine.GetMainLoop() is SceneTree tree)
			{
				tree.ProcessFrame += () => Safe("tick", Tick);
			}
		});
	}

	private static void Safe(string where, Action a)
	{
		try
		{
			a();
		}
		catch (Exception e)
		{
			GD.PrintErr($"{Tag} {where} failed: {e}");
		}
	}

	// ───────────── 歌曲 ─────────────

	private static void ScanMusic()
	{
		Songs.Clear();
		string dir = Path.Combine(_modDir, "music");
		if (!Directory.Exists(dir))
		{
			GD.Print($"{Tag} no music folder: {dir}");
			return;
		}
		string list = Path.Combine(dir, "playlist.json");
		if (File.Exists(list))
		{
			using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(list));
			foreach (JsonElement e in doc.RootElement.EnumerateArray())
			{
				string file = e.TryGetProperty("file", out JsonElement f) ? f.GetString() ?? "" : "";
				if (file.Length == 0 || !File.Exists(Path.Combine(dir, file)))
				{
					continue;
				}
				string? lrc = e.TryGetProperty("lrc", out JsonElement l) ? l.GetString() : null;
				Songs.Add(new Song
				{
					File = Path.Combine(dir, file),
					Title = e.TryGetProperty("title", out JsonElement t) ? t.GetString() ?? Path.GetFileNameWithoutExtension(file) : Path.GetFileNameWithoutExtension(file),
					Artist = e.TryGetProperty("artist", out JsonElement a) ? a.GetString() ?? "" : "",
					Lrc = !string.IsNullOrEmpty(lrc) && File.Exists(Path.Combine(dir, lrc)) ? Path.Combine(dir, lrc) : null,
				});
			}
		}
		if (Songs.Count == 0)
		{
			foreach (string f in Directory.GetFiles(dir).Where(f => f.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
			{
				string lrc = Path.ChangeExtension(f, ".lrc");
				Songs.Add(new Song { File = f, Title = Path.GetFileNameWithoutExtension(f), Lrc = File.Exists(lrc) ? lrc : null });
			}
		}
		_index = Math.Clamp(_prefs.Last, 0, Math.Max(0, Songs.Count - 1));
		GD.Print($"{Tag} {Songs.Count} songs in {dir}");
	}

	private static AudioStream? LoadStream(Song s)
	{
		if (s.File.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
		{
			return new AudioStreamMP3 { Data = File.ReadAllBytes(s.File) };
		}
		return AudioStreamOggVorbis.LoadFromFile(s.File);
	}

	private static readonly Regex Stamp = new(@"\[(\d+):(\d+(?:[.:]\d+)?)\]", RegexOptions.Compiled);

	/// <summary>LRC：一行可以有多个时间戳；同一时刻的多行（原文 + 翻译）合成两行显示。</summary>
	private static List<(double, string)> ParseLrc(string path)
	{
		var map = new SortedDictionary<double, List<string>>();
		foreach (string raw in File.ReadAllLines(path))
		{
			MatchCollection ms = Stamp.Matches(raw);
			if (ms.Count == 0)
			{
				continue;
			}
			string text = Stamp.Replace(raw, "").Trim();
			foreach (Match m in ms)
			{
				double at = int.Parse(m.Groups[1].Value) * 60 + double.Parse(m.Groups[2].Value.Replace(':', '.'), System.Globalization.CultureInfo.InvariantCulture);
				if (!map.TryGetValue(at, out List<string>? l))
				{
					map[at] = l = new List<string>();
				}
				if (text.Length > 0)
				{
					l.Add(text);
				}
			}
		}
		return map.Select(kv => (kv.Key, string.Join("\n", kv.Value.Take(2)))).ToList();
	}

	// ───────────── 播放控制 ─────────────

	private static void Toggle()
	{
		if (Songs.Count == 0)
		{
			ShowLyric("music 文件夹里还没有歌曲");
			return;
		}
		switch (_state)
		{
			case State.Stopped:
				Start(_index);
				break;
			case State.Playing:
				_state = State.Paused;
				FadeTo(0f, FadeOut, () => { if (_state == State.Paused && _player != null) _player.StreamPaused = true; });
				break;
			case State.Paused:
				_state = State.Playing;
				if (_player != null) _player.StreamPaused = false;
				FadeTo(1f, FadeIn);
				break;
		}
		RefreshList();
	}

	/// <summary>切到第 i 首：正在响就先淡出再换，然后淡入。</summary>
	private static void Start(int i)
	{
		if (Songs.Count == 0 || _player is not AudioStreamPlayer player)
		{
			return;
		}
		i = ((i % Songs.Count) + Songs.Count) % Songs.Count;
		void Go()
		{
			Song s = Songs[i];
			AudioStream? st = LoadStream(s);
			if (st == null)
			{
				GD.PrintErr($"{Tag} cannot load {s.File}");
				return;
			}
			if (s.Lines == null && s.Lrc != null)
			{
				Safe("lrc", () => s.Lines = ParseLrc(s.Lrc));
			}
			_index = i;
			_prefs.Last = i;
			SavePrefs();
			player.Stream = st;
			player.StreamPaused = false;
			player.Play();
			_state = State.Playing;
			_fade = 0f;
			FadeTo(1f, FadeIn);
			_lyricShown = "";
			ShowLyric("");
			RefreshList();
		}
		if (_state == State.Playing && player.Playing && _fade > 0.01f)
		{
			FadeTo(0f, SwitchOut, Go);
		}
		else
		{
			Go();
		}
	}

	private static void Next()
	{
		if (Songs.Count == 0) return;
		int n = _prefs.Mode switch
		{
			1 => _index,
			2 => Songs.Count == 1 ? 0 : (_index + 1 + (int)(GD.Randi() % (uint)(Songs.Count - 1))) % Songs.Count,
			_ => _index + 1,
		};
		_state = State.Stopped;           // 已经播完了：直接换，不再淡出
		Start(n);
	}

	private static void FadeTo(float target, float seconds, Action? then = null)
	{
		_fadeTarget = target;
		_fadeSpeed = 1f / Mathf.Max(0.01f, seconds);
		_afterFadeOut = then;
	}

	// ───────────── 每帧 ─────────────

	private static void Tick()
	{
		ulong now = Time.GetTicksMsec();
		float dt = _lastTick == 0 ? 0f : Mathf.Min((now - _lastTick) / 1000f, 0.1f);   // 真实时间（不受游戏顿帧 / 时停影响）
		_lastTick = now;
		if (_layer == null || !GodotObject.IsInstanceValid(_layer))
		{
			Build();
			if (_layer == null) return;
		}
		// 音量淡入淡出
		if (_fade != _fadeTarget)
		{
			_fade = Mathf.MoveToward(_fade, _fadeTarget, _fadeSpeed * dt);
			if (_fade == _fadeTarget && _afterFadeOut != null)
			{
				Action a = _afterFadeOut;
				_afterFadeOut = null;
				a();
			}
		}
		SettingsSave? set = SaveManager.Instance?.SettingsSave;
		bool focused = _layer.GetTree()?.Root?.HasFocus() ?? true;
		bool muteBg = SaveManager.Instance?.PrefsSave?.MuteInBackground ?? false;
		_focus = Mathf.MoveToward(_focus, !focused && muteBg ? 0f : 1f, dt);
		float master = set?.VolumeMaster ?? 0.5f;
		float lin = SmoothFade(_fade) * _prefs.Volume * master * master * _focus * 0.85f;
		if (_player != null)
		{
			_player.VolumeDb = lin <= 0.0001f ? -80f : Mathf.LinearToDb(lin);
		}
		// 原版背景音乐：音乐盒在放（或正在淡入）时淡出到静音，暂停 / 停止后淡回
		bool ours = _state == State.Playing;
		_bgm = Mathf.MoveToward(_bgm, ours ? 0f : 1f, dt / BgmFade);
		float bgmVol = (set?.VolumeBgm ?? 0.5f) * SmoothFade(_bgm);
		if (NAudioManager.Instance is NAudioManager am && (Mathf.Abs(bgmVol - _lastBgmApplied) > 0.002f || (_bgm < 1f && now - _lastBgmPush > 500)))
		{
			// 淡回到 1 之后不再每帧写（不和游戏设置里的音量滑条抢）
			if (_bgm < 1f || _lastBgmApplied != bgmVol)
			{
				am.SetBgmVol(bgmVol);
				_lastBgmApplied = bgmVol;
				_lastBgmPush = now;
			}
		}
		UpdateButton(now, dt);
		UpdateLyrics();
	}

	private static float SmoothFade(float x) => x * x * (3f - 2f * x);

	// ───────────── 界面 ─────────────

	private static void Build()
	{
		if (Engine.GetMainLoop() is not SceneTree tree || tree.Root == null)
		{
			return;
		}
		_font = ResourceLoader.Load("res://fonts/jpn/NotoSansCJKjp-Medium.otf") as Font;
		_layer = new CanvasLayer { Layer = 89, Name = "P3MusicBox" };
		tree.Root.AddChild(_layer);
		_player = new AudioStreamPlayer { Bus = "Master", VolumeDb = -80f, Name = "P3MusicBoxPlayer" };
		_layer.AddChild(_player);
		_player.Finished += () => Safe("next", Next);

		// 按钮：深蓝圆盘 + 青色描边 + 外圈虚线（播放时慢转）+ 音符
		Control box = new() { Size = new Vector2(52f, 52f), MouseFilter = Control.MouseFilterEnum.Stop, Name = "Button", PivotOffset = new Vector2(26f, 26f) };
		_layer.AddChild(box);
		_button = box;
		Node2D icon = new() { Position = new Vector2(26f, 26f) };
		box.AddChild(icon);
		_icon = icon;
		icon.AddChild(new Polygon2D { Polygon = Circle(23f, 40), Color = Navy });
		_rim = new Line2D { Points = Circle(23f, 40).Append(Circle(23f, 40)[0]).ToArray(), Width = 2.5f, DefaultColor = Cyan, Antialiased = true };
		icon.AddChild(_rim);
		Node2D ring = new();
		icon.AddChild(ring);
		_ring = ring;
		for (int i = 0; i < 12; i++)
		{
			float a0 = Mathf.Tau * i / 12f, a1 = a0 + Mathf.Tau / 12f * 0.55f;
			ring.AddChild(new Line2D { Points = Arc(29f, a0, a1, 6), Width = 2f, DefaultColor = new Color(Cyan, 0.7f), Antialiased = true });
		}
		_glyph = new Label { Text = "♪", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Size = new Vector2(52f, 52f), Position = new Vector2(-26f, -28f), MouseFilter = Control.MouseFilterEnum.Ignore };
		Style(_glyph, 28, Colors.White);
		icon.AddChild(_glyph);
		_hoverTitle = new Label { HorizontalAlignment = HorizontalAlignment.Right, MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = new Color(1f, 1f, 1f, 0f) };
		Style(_hoverTitle, 17, Colors.White, outline: 6);
		_layer.AddChild(_hoverTitle);
		box.MouseEntered += () => _hover = true;
		box.MouseExited += () => _hover = false;
		box.GuiInput += e => Safe("click", () =>
		{
			if (e is InputEventMouseButton { Pressed: true } mb)
			{
				if (mb.ButtonIndex == MouseButton.Left) Toggle();
				else if (mb.ButtonIndex == MouseButton.Right) TogglePopup();
				box.AcceptEvent();
			}
		});

		// 歌词：按钮下方右对齐，两个标签交替淡入淡出
		_lyric = new Label[2];
		for (int i = 0; i < 2; i++)
		{
			Label l = new() { HorizontalAlignment = HorizontalAlignment.Right, MouseFilter = Control.MouseFilterEnum.Ignore, Size = new Vector2(640f, 60f), Modulate = new Color(1f, 1f, 1f, 0f) };
			Style(l, 18, Colors.White, outline: 7);
			_layer.AddChild(l);
			_lyric[i] = l;
		}
		BuildPopup();
	}

	private static void Style(Label l, int size, Color color, int outline = 0)
	{
		if (_font != null) l.AddThemeFontOverride("font", _font);
		l.AddThemeFontSizeOverride("font_size", size);
		l.AddThemeColorOverride("font_color", color);
		if (outline > 0)
		{
			l.AddThemeColorOverride("font_outline_color", new Color(0.01f, 0.03f, 0.12f, 0.95f));
			l.AddThemeConstantOverride("outline_size", outline);
		}
	}

	private static Vector2[] Circle(float r, int n) => Enumerable.Range(0, n).Select(i => Vector2.Right.Rotated(Mathf.Tau * i / n) * r).ToArray();

	private static Vector2[] Arc(float r, float a0, float a1, int n) => Enumerable.Range(0, n + 1).Select(i => Vector2.Right.Rotated(Mathf.Lerp(a0, a1, i / (float)n)) * r).ToArray();

	/// <summary>按钮位置：跟着顶栏的设置（齿轮）按钮，放在它正下方；没有顶栏（主菜单）时放右上角。</summary>
	private static Vector2 Anchor()
	{
		Control? pause = NRun.Instance?.GlobalUi?.TopBar?.Pause;
		if (pause != null && GodotObject.IsInstanceValid(pause) && pause.IsVisibleInTree())
		{
			Rect2 r = pause.GetGlobalRect();
			return new Vector2(r.GetCenter().X - 26f, r.End.Y + 8f);
		}
		Vector2 view = _layer?.GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920f, 1080f);
		return new Vector2(view.X - 84f, 96f);
	}

	private static void UpdateButton(ulong now, float dt)
	{
		if (_button == null || _icon == null || _ring == null || _rim == null || _glyph == null)
		{
			return;
		}
		bool hidden = NCapstoneContainer.Instance?.InUse ?? false;     // 暂停菜单 / 设置 / 牌组等全屏界面打开时藏起来
		_button.Visible = !hidden;
		if (hidden && _popup != null) ClosePopup();
		_button.Position = Anchor();
		bool playing = _state == State.Playing;
		float target = _hover ? 1.08f : 1f;
		_icon.Scale = _icon.Scale.Lerp(Vector2.One * target, Mathf.Min(1f, dt * 14f));
		_ring.Rotation += dt * (playing ? 0.9f : 0f);
		_ring.Modulate = new Color(1f, 1f, 1f, playing ? 1f : 0.35f);
		_rim.DefaultColor = new Color(Cyan, _hover ? 1f : playing ? 0.9f : 0.55f);
		_glyph.Modulate = new Color(1f, 1f, 1f, playing ? 1f : 0.7f);
		_glyph.Position = new Vector2(-26f, -28f + (playing ? 2f * Mathf.Sin(now / 1000f * 4f) : 0f));
		// 播放时偶尔飘出一个小音符
		if (playing && !hidden && now - _lastNote > 900)
		{
			_lastNote = now;
			FloatNote();
		}
		// 悬停：左侧显示当前歌名
		if (_hoverTitle != null)
		{
			string title = Songs.Count == 0 ? "（没有歌曲）" : Songs[_index].Title + (_state == State.Playing ? "" : _state == State.Paused ? "  (暂停)" : "  (左键播放 / 右键歌单)");
			_hoverTitle.Text = title;
			_hoverTitle.Size = new Vector2(520f, 30f);
			_hoverTitle.Position = _button.Position + new Vector2(-532f, 11f);
			float a = _hoverTitle.Modulate.A;
			_hoverTitle.Modulate = new Color(1f, 1f, 1f, Mathf.MoveToward(a, _hover && !hidden && _popup?.Visible != true ? 1f : 0f, dt * 6f));
		}
		if (_popup != null && _popup.Visible)
		{
			_popup.Position = _button.Position + new Vector2(52f - _popup.Size.X, 60f);
		}
	}

	private static void FloatNote()
	{
		if (_layer == null || _button == null) return;
		Label n = new() { Text = GD.Randi() % 2 == 0 ? "♪" : "♫", MouseFilter = Control.MouseFilterEnum.Ignore };
		Style(n, 16, Cyan);
		_layer.AddChild(n);
		Vector2 p0 = _button.Position + new Vector2(30f + (float)GD.RandRange(-6, 10), 0f);
		float drift = (float)GD.RandRange(-16, 4);
		Tween tw = n.CreateTween();
		tw.TweenMethod(Callable.From<float>(t =>
		{
			n.Position = p0 + new Vector2(drift * t + 4f * Mathf.Sin(t * 6f), -36f * t);
			n.Modulate = new Color(1f, 1f, 1f, Mathf.Sin(Mathf.Pi * t) * 0.85f);
		}), 0f, 1f, 1.4);
		tw.TweenCallback(Callable.From(n.QueueFree));
	}

	private static void ShowLyric(string text)
	{
		if (_lyric.Length < 2 || text == _lyricShown) return;
		_lyricShown = text;
		Label oldL = _lyric[_lyricSlot];
		_lyricSlot = 1 - _lyricSlot;
		Label l = _lyric[_lyricSlot];
		l.Text = text;
		Fade(oldL, 0f, 0.35f);
		Fade(l, text.Length > 0 ? 1f : 0f, 0.35f);
	}

	private static void Fade(CanvasItem c, float to, float seconds)
	{
		Tween tw = c.CreateTween();
		tw.TweenProperty(c, "modulate:a", to, seconds).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
	}

	private static void UpdateLyrics()
	{
		if (_button == null || _lyric.Length < 2) return;
		foreach (Label l in _lyric)
		{
			l.Position = _button.Position + new Vector2(52f - 640f, 62f);
			l.Visible = _button.Visible && _popup?.Visible != true;
		}
		if (!_prefs.Lyrics || _player == null || Songs.Count == 0 || _state == State.Stopped)
		{
			ShowLyric("");
			return;
		}
		if (_state == State.Paused)
		{
			return;
		}
		List<(double At, string Text)>? lines = Songs[_index].Lines;
		if (lines == null || lines.Count == 0)
		{
			ShowLyric("");
			return;
		}
		double pos = _player.GetPlaybackPosition() + AudioServer.GetTimeSinceLastMix();
		string cur = "";
		foreach (var (at, text) in lines)
		{
			if (at > pos) break;
			cur = text;
		}
		ShowLyric(cur);
	}

	// ───────────── 歌单 ─────────────

	private static void BuildPopup()
	{
		if (_layer == null) return;
		// 点空白处关闭
		_catcher = new Control { Visible = false, MouseFilter = Control.MouseFilterEnum.Stop, Name = "Catcher" };
		_catcher.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_layer.AddChild(_catcher);
		_catcher.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true }) ClosePopup(); };

		PanelContainer panel = new() { Visible = false, CustomMinimumSize = new Vector2(440f, 0f), Name = "Playlist" };
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = Navy, BorderColor = Cyan, BorderWidthTop = 3, BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
			ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 10, ContentMarginBottom = 12, ShadowColor = new Color(0f, 0f, 0f, 0.45f), ShadowSize = 10,
		});
		_layer.AddChild(panel);
		_popup = panel;
		VBoxContainer col = new();
		col.AddThemeConstantOverride("separation", 8);
		panel.AddChild(col);

		HBoxContainer head = new();
		head.AddThemeConstantOverride("separation", 8);
		col.AddChild(head);
		Label title = new() { Text = "MUSIC BOX", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		Style(title, 22, Cyan);
		head.AddChild(title);
		_modeButton = SmallButton(head, "", () => { _prefs.Mode = (_prefs.Mode + 1) % 3; SavePrefs(); RefreshList(); });
		_lyricsButton = SmallButton(head, "", () => { _prefs.Lyrics = !_prefs.Lyrics; SavePrefs(); RefreshList(); });

		_nowLabel = new Label { ClipText = true, CustomMinimumSize = new Vector2(400f, 0f) };
		Style(_nowLabel, 16, new Color(1f, 1f, 1f, 0.8f));
		col.AddChild(_nowLabel);

		HBoxContainer vol = new();
		vol.AddThemeConstantOverride("separation", 8);
		col.AddChild(vol);
		Label vl = new() { Text = "音量" };
		Style(vl, 15, new Color(1f, 1f, 1f, 0.8f));
		vol.AddChild(vl);
		HSlider slider = new() { MinValue = 0, MaxValue = 1, Step = 0.01, Value = _prefs.Volume, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, FocusMode = Control.FocusModeEnum.None };
		slider.ValueChanged += v => { _prefs.Volume = (float)v; SavePrefs(); };
		vol.AddChild(slider);

		ScrollContainer scroll = new() { CustomMinimumSize = new Vector2(412f, 360f), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		col.AddChild(scroll);
		_list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		_list.AddThemeConstantOverride("separation", 2);
		scroll.AddChild(_list);
		Label hint = new() { Text = "左键音乐盒：播放 / 暂停　右键：歌单" };
		Style(hint, 13, new Color(1f, 1f, 1f, 0.5f));
		col.AddChild(hint);
	}

	private static Button SmallButton(Control parent, string text, Action act)
	{
		Button b = new() { Text = text, FocusMode = Control.FocusModeEnum.None };
		if (_font != null) b.AddThemeFontOverride("font", _font);
		b.AddThemeFontSizeOverride("font_size", 14);
		b.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new Color(Blue, 0.6f), ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 2, ContentMarginBottom = 2 });
		b.AddThemeStyleboxOverride("hover", new StyleBoxFlat { BgColor = new Color(Cyan, 0.5f), ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 2, ContentMarginBottom = 2 });
		b.AddThemeStyleboxOverride("pressed", new StyleBoxFlat { BgColor = Cyan, ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 2, ContentMarginBottom = 2 });
		b.Pressed += () => Safe("button", act);
		parent.AddChild(b);
		return b;
	}

	private static void TogglePopup()
	{
		if (_popup == null || _catcher == null) return;
		if (_popup.Visible)
		{
			ClosePopup();
			return;
		}
		RefreshList();
		_catcher.Visible = true;
		_popup.Visible = true;
		_popup.Modulate = new Color(1f, 1f, 1f, 0f);
		Fade(_popup, 1f, 0.15f);
	}

	private static void ClosePopup()
	{
		if (_popup == null || _catcher == null) return;
		_popup.Visible = false;
		_catcher.Visible = false;
	}

	private static void RefreshList()
	{
		if (_list == null) return;
		if (_modeButton != null) _modeButton.Text = _prefs.Mode switch { 1 => "单曲循环", 2 => "随机播放", _ => "顺序播放" };
		if (_lyricsButton != null) _lyricsButton.Text = _prefs.Lyrics ? "歌词：开" : "歌词：关";
		if (_nowLabel != null)
		{
			_nowLabel.Text = Songs.Count == 0 ? $"没有歌曲：把 .ogg / .mp3 放进 {Path.Combine(_modDir, "music")}"
				: (_state == State.Playing ? "▶ " : _state == State.Paused ? "❚❚ " : "■ ") + Songs[_index].Title + (Songs[_index].Artist.Length > 0 ? " — " + Songs[_index].Artist : "");
		}
		foreach (Node c in _list.GetChildren())
		{
			_list.RemoveChild(c);
			c.QueueFree();
		}
		for (int i = 0; i < Songs.Count; i++)
		{
			int idx = i;
			bool cur = i == _index && _state != State.Stopped;
			Button b = new() { Text = $"{i + 1:00}  {Songs[i].Title}" + (Songs[i].Artist.Length > 0 ? $"  ·  {Songs[i].Artist}" : ""), Alignment = HorizontalAlignment.Left, FocusMode = Control.FocusModeEnum.None, ClipText = true, CustomMinimumSize = new Vector2(400f, 34f) };
			if (_font != null) b.AddThemeFontOverride("font", _font);
			b.AddThemeFontSizeOverride("font_size", 16);
			// P3R 菜单风格：当前曲目白底深蓝字，悬停青色
			b.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = cur ? new Color(0.96f, 0.98f, 1f) : new Color(0f, 0f, 0f, 0f), ContentMarginLeft = 10 });
			b.AddThemeStyleboxOverride("hover", new StyleBoxFlat { BgColor = cur ? Colors.White : new Color(Cyan, 0.35f), ContentMarginLeft = 10 });
			b.AddThemeStyleboxOverride("pressed", new StyleBoxFlat { BgColor = Cyan, ContentMarginLeft = 10 });
			b.AddThemeColorOverride("font_color", cur ? new Color(0.04f, 0.1f, 0.35f) : Colors.White);
			b.AddThemeColorOverride("font_hover_color", cur ? new Color(0.04f, 0.1f, 0.35f) : Colors.White);
			b.Pressed += () => Safe("pick", () => { Start(idx); });
			_list.AddChild(b);
		}
	}

	// ───────────── 设置保存 ─────────────

	private static void LoadPrefs()
	{
		try
		{
			if (File.Exists(PrefsPath))
			{
				_prefs = JsonSerializer.Deserialize<Prefs>(File.ReadAllText(PrefsPath)) ?? new Prefs();
			}
		}
		catch (Exception e)
		{
			GD.PrintErr($"{Tag} prefs load failed: {e.Message}");
			_prefs = new Prefs();
		}
	}

	private static void SavePrefs()
	{
		try
		{
			File.WriteAllText(PrefsPath, JsonSerializer.Serialize(_prefs));
		}
		catch (Exception e)
		{
			GD.PrintErr($"{Tag} prefs save failed: {e.Message}");
		}
	}
}
