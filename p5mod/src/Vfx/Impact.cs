using Godot;

namespace P5Joker;

/// <summary>命中的分量：决定顿帧、震屏、全屏冲击帧有多重。</summary>
public enum Weight
{
	Light,      // 小刀（子弹）的每一发、多段攻击的前几下
	Medium,     // 普通单发攻击、多段攻击的最后一下
	Heavy,      // 大招类攻击，或者一下打得特别重
	Kill,       // 打死一个敌人（还有别的敌人活着）
	Finale,     // 打死最后一个敌人，战斗结束
}

/// <summary>
/// 打击确认感（第四版，用户 2026-11-02：要有视觉冲击力、有确认感；但太频繁会花眼、看腻）。不画任何形状，全是引擎 / 着色器实时处理：
/// · 顿帧：HitStop.Request（时间流速压到 0.08，按档位停 40～110ms；0.35 秒内不重复）；
/// · 定向震屏：沿攻击方向推一下（Stage.ShakeDir），比随机抖更像"打进去"；
/// · 受击白闪 + 击退：目标本体 HDR 提亮，转红，再回原色；身体被打得往后一顿再弹回（击杀不做，交给死亡动画）；
/// · 全屏冲击帧（Heavy / Kill / Finale）：把画面瞬间变成 P5 式红 / 黑 / 白三色漫画帧（亮部白、中间调红带网点、暗部黑），
///   停几帧，接着色散 + 指向命中点的径向模糊衰减掉。
/// 所有计时用真实时间（不受顿帧影响），所以全场停住的那一刻冲击帧照样在闪。
///
/// 什么时候用哪一档见 docs/p5/FX_V4_PLAN.md「打击确认感的分档」。为了不花眼：
/// · 一次结算同时打中好几个敌人（群攻），白闪 / 击退每个敌人各自做，顿帧 / 震屏 / 冲击帧整组只放一次，按最重的那一下算；
/// · 红黑冲击帧 1 秒内最多一次（击杀 0.4 秒、战斗结束不限），超了只留顿帧和震屏；
/// · 轻击 0.3 秒内连着来（连斩、连射）只有第一下顿帧。
/// </summary>
public static class Impact
{
	private record struct Spec(int Stop, int Shake, float Flash, float Knock, float Ink, float InkHold, float Chroma, float Radial, float Life);

	private static Spec For(Weight w) => w switch
	{
		// 用户 2026-11-04：每次打击都晃、全屏扭一下，闪眼睛、头晕 → 轻 / 中不再有全屏色散和径向模糊，轻击不震屏；重 / 击杀 / 结束的全屏效果减半
		Weight.Light => new Spec(1, 0, 2.0f, 12f, 0f, 0f, 0f, 0f, 0.12f),
		Weight.Medium => new Spec(2, 1, 2.6f, 26f, 0f, 0f, 0f, 0f, 0.2f),
		Weight.Heavy => new Spec(3, 2, 3.0f, 42f, 1f, 0.07f, 6f, 0.25f, 0.4f),
		Weight.Kill => new Spec(3, 2, 0f, 0f, 1f, 0.07f, 6f, 0.28f, 0.4f),
		_ => new Spec(4, 3, 0f, 0f, 1f, 0.28f, 10f, 0.45f, 0.6f),
	};

	/// <summary>
	/// 按这一下的实际伤害升档（只升不降）：floor = 按牌定的底档；hpBefore = 这一下之前的血量。
	/// 轻击打出 12 点以上升到中；任何一下 25 点以上、或者 15 点以上且打掉当前血量三分之一，升到重。
	/// calm = 多段攻击的前几下：最多升到中，免得连斩每一下都闪。
	/// </summary>
	public static Weight Grade(Weight floor, int damage, int hpBefore, bool killed, bool finale, bool calm = false)
	{
		if (killed)
		{
			return finale ? Weight.Finale : Weight.Kill;
		}
		Weight w = floor;
		if (w < Weight.Medium && damage >= 12)
		{
			w = Weight.Medium;
		}
		if (!calm && w < Weight.Heavy && (damage >= 25 || (damage >= 15 && damage * 3 >= hpBefore)))
		{
			w = Weight.Heavy;
		}
		return calm && w > Weight.Medium ? Weight.Medium : w;
	}

	private static ulong _lastInk, _lastLightStop;
	private static bool _flushQueued;
	private static Weight _pendW;
	private static Vector2 _pendAt, _pendDir;
	private static Control? _pendLayer;

	/// <summary>
	/// 命中确认。layer = 特效层；body = 被打的人物（敌人的 Visuals，可为空）；at = 命中点（全局）；dir = 攻击方向（从攻击者指向目标）。
	/// </summary>
	public static void Confirm(Control layer, Node2D? body, Vector2 at, Vector2 dir, Weight w)
	{
		Spec s = For(w);
		dir = dir.LengthSquared() > 0.001f ? dir.Normalized() : Vector2.Right;
		if (body != null && GodotObject.IsInstanceValid(body) && w < Weight.Kill)
		{
			FlashBody(body, s.Flash);
			Knock(body, dir * s.Knock);
		}
		// 整屏的部分攒一下（同一次结算里的其他目标 30ms 内都会到），取最重的一下放一次
		if (!_flushQueued || w > _pendW)
		{
			_pendW = w;
			_pendAt = at;
			_pendDir = dir;
			_pendLayer = layer;
		}
		if (_flushQueued)
		{
			return;
		}
		_flushQueued = true;
		SceneTree? tree = layer.GetTree();
		if (tree == null)
		{
			Flush();
			return;
		}
		tree.CreateTimer(0.03, processAlways: true, processInPhysics: false, ignoreTimeScale: true).Timeout += () => Stage.Safe("Impact.Flush", Flush);
	}

	private static void Flush()
	{
		_flushQueued = false;
		if (_pendLayer is not Control layer || !GodotObject.IsInstanceValid(layer))
		{
			return;
		}
		Weight w = _pendW;
		Spec s = For(w);
		ulong now = Stage.NowMs();

		bool stop = true;
		if (w == Weight.Light)
		{
			stop = now - _lastLightStop >= 300;
			_lastLightStop = now;
		}
		if (stop)
		{
			HitStop.Request(s.Stop switch { <= 1 => 0.04, 2 => 0.06, 3 => 0.08, _ => 0.11 });
		}
		if (Stage.ShakeDir != null)
		{
			Stage.ShakeDir(s.Shake, Mathf.RadToDeg(_pendDir.Angle()));
		}
		else
		{
			Fx.Shake(s.Shake);
		}

		if (s.Ink > 0f)
		{
			ulong gap = w switch { Weight.Finale => 0, Weight.Kill => 400, _ => 1000 };
			if (now - _lastInk >= gap)
			{
				_lastInk = now;
			}
			else
			{
				s = s with { Ink = 0f, InkHold = 0f, Chroma = s.Chroma * 0.6f, Radial = s.Radial * 0.5f, Life = 0.2f };
			}
		}
		ScreenFrame(layer, _pendAt, _pendDir, s);
	}

	// ───────────────────────── 受击白闪 / 击退 ─────────────────────────

	private static void FlashBody(Node2D body, float peak)
	{
		Color baseColor = BodyTint.RestOf(body);                     // 和中毒染色共用「原色」，同时改也不会卡在半路
		Tween tw = body.CreateTween().SetIgnoreTimeScale(true);
		body.Modulate = new Color(peak, peak, peak, baseColor.A);
		tw.TweenProperty(body, "modulate", new Color(1.9f, 0.55f, 0.6f, baseColor.A), 0.06);
		tw.TweenProperty(body, "modulate", baseColor, 0.16).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
	}

	private static void Knock(Node2D body, Vector2 offset)
	{
		if (offset.LengthSquared() < 1f)
		{
			return;
		}
		Vector2 home = body.Position;
		Tween tw = body.CreateTween().SetIgnoreTimeScale(true);
		body.Position = home + offset;
		tw.TweenInterval(0.05);
		tw.TweenProperty(body, "position", home, 0.32).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
	}

	// ───────────────────────── 全屏冲击帧 ─────────────────────────

	private static Shader? _frame;

	/// <summary>
	/// 读屏幕：ink = P5 三色漫画帧的强度；chroma = 沿 dir 的色散像素；radial = 指向 center 的径向模糊；flash = 整体提白。
	/// </summary>
	public static Shader FrameShader => _frame ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform float ink = 0.0;
uniform float chroma = 0.0;
uniform vec2 dir = vec2(1.0, 0.0);
uniform float radial = 0.0;
uniform vec2 center = vec2(0.5);
uniform float flash = 0.0;
uniform vec3 red = vec3(0.92, 0.05, 0.09);

vec3 tap(vec2 uv, vec2 off) {
	return vec3(texture(screen_tex, uv + off).r, texture(screen_tex, uv).g, texture(screen_tex, uv - off).b);
}

void fragment() {
	vec2 uv = SCREEN_UV;
	vec2 off = dir * chroma * SCREEN_PIXEL_SIZE;
	vec3 col = vec3(0.0);
	float wsum = 0.0;
	for (int i = 0; i < 12; i++) {
		float t = float(i) / 11.0;
		float w = 1.0 - 0.6 * t;
		vec2 u = center + (uv - center) * (1.0 - radial * 0.09 * t);
		col += tap(u, off * (1.0 + t)) * w;
		wsum += w;
	}
	col /= wsum;

	// P5 三色帧：亮部白、中间调红（45° 网点）、暗部黑；命中点附近更亮，像被闪光打穿
	float lum = dot(col, vec3(0.299, 0.587, 0.114));
	float near = 1.0 - smoothstep(0.0, 0.07, distance(uv * vec2(1.7778, 1.0), center * vec2(1.7778, 1.0)));
	lum = lum + near * 0.6;
	vec2 g = FRAGCOORD.xy * mat2(vec2(0.7071, -0.7071), vec2(0.7071, 0.7071)) / 9.0;
	float dotr = length(fract(g) - 0.5);
	float dots = step(dotr, mix(0.12, 0.62, smoothstep(0.13, 0.5, lum)));
	// 用户 2026-11-03：原来的全屏红黑白三色帧是光污染 → 改成温和的「压暗 + 偏红」：画面整体暗下去、往红色偏，命中点附近一点红光，
	// 不再有大面积的白、网点和纯红；冲击感交给色散 + 径向模糊 + 顿帧
	vec3 graded = col * vec3(0.62, 0.24, 0.28) + red * 0.18 * near;
	col = mix(col, graded, ink);
	COLOR = vec4(col + flash, 1.0);
}" };

	/// <summary>预热（Warmup）：全屏帧的着色器先在一个看不见的层里画两帧，编译掉。</summary>
	public static void Warm(Control hidden)
	{
		ColorRect rect = new() { Material = new ShaderMaterial { Shader = FrameShader }, MouseFilter = Control.MouseFilterEnum.Ignore, Size = Fx.Screen(hidden) };
		BackBufferCopy copy = new() { CopyMode = BackBufferCopy.CopyModeEnum.Viewport };
		hidden.AddChild(copy);
		hidden.AddChild(rect);
		rect.GlobalPosition = Vector2.Zero;
		Fx.FreeAfter(rect, 0.2);
		Fx.FreeAfter(copy, 0.2);
	}

	private static void ScreenFrame(Control layer, Vector2 at, Vector2 dir, Spec s)
	{
		if (s.Chroma <= 0f && s.Ink <= 0f)
		{
			return;
		}
		Vector2 screen = Fx.Screen(layer);
		ShaderMaterial mat = new() { Shader = FrameShader };
		ColorRect rect = new()
		{
			Material = mat,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Size = screen,
			Name = "P5ImpactFrame",
		};
		// 先强制重新抓一次屏幕：同一帧里前面已经有读屏幕的着色器（Glow.Ripple 的空间扭曲）时，Godot 会复用那份更早的画面，
		// 这块全屏不透明的矩形就会把之后画的刀光、粒子、伤害数字盖掉
		BackBufferCopy copy = new() { CopyMode = BackBufferCopy.CopyModeEnum.Viewport, Name = "P5ImpactCopy" };
		layer.AddChild(copy);
		layer.AddChild(rect);
		rect.GlobalPosition = Vector2.Zero;
		mat.SetShaderParameter("center", new Vector2(at.X / screen.X, at.Y / screen.Y));
		mat.SetShaderParameter("dir", dir);

		void Set(float ink, float chroma, float radial, float flash)
		{
			mat.SetShaderParameter("ink", ink);
			mat.SetShaderParameter("chroma", chroma);
			mat.SetShaderParameter("radial", radial);
			mat.SetShaderParameter("flash", flash);
		}

		// 第 1 帧：白闪；然后三色帧停 InkHold；然后色散 + 径向模糊在 Life 内衰减
		Set(s.Ink, s.Chroma, s.Radial, s.Ink > 0f ? 0.06f : 0f);
		Tween tw = rect.CreateTween().SetIgnoreTimeScale(true);
		tw.TweenInterval(1.0 / 30.0);
		tw.TweenCallback(Callable.From(() => Set(s.Ink, s.Chroma, s.Radial, 0f)));
		if (s.InkHold > 0f)
		{
			tw.TweenInterval(s.InkHold);
		}
		tw.TweenMethod(Callable.From<float>(t =>
		{
			float k = 1f - t;
			// 红黑帧停够之后慢慢褪（用户：谋杀的红屏只有一瞬间、很不明显）
			Set(s.Ink * (1f - Mathf.SmoothStep(0f, 0.6f, t)), s.Chroma * k * k, s.Radial * k * k, 0f);
		}), 0f, 1f, s.Life);
		tw.TweenCallback(Callable.From(() => { rect.QueueFree(); copy.QueueFree(); }));
	}
}
