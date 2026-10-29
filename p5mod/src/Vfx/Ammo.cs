using Godot;

namespace P5Joker;

/// <summary>
/// 小刀弹药（用户 2026-10-29：P5R 有子弹数，杀戮尖塔没有）：能量球旁边一个左轮弹巢，手牌里有几张小刀牌就亮几发。
/// 着色器实时画（不贴图）：深色转轮 + 红色描边 + 六个弹膛，装弹的弹膛是白热的弹头 + 红晕，空的是黑洞。
/// 多一张：转轮转一格、新装的那发一闪；少一张（打出）：那发"弹出"一亮再熄、转轮倒转一格。超过 6 张时旁边显示 ×N。
/// Set(count) 只记目标，Tick(delta) 每帧推进动画（游戏里由 NEnergyCounter._Process 驱动，预览里由预览驱动）。
/// </summary>
public sealed class AmmoView
{
	private static Shader? _shader;

	private static Shader Cylinder => _shader ??= new Shader { Code = @"
shader_type canvas_item;
uniform float loaded = 0.0;      // 亮着的弹膛数（0~6，可以是小数：正在装 / 退）
uniform float spin = 0.0;        // 转轮角度
uniform float flash = 0.0;       // 刚装 / 刚退的那一发闪光
uniform float flash_slot = 0.0;
uniform float dim = 1.0;         // 0 发时整体压暗

float ring(float d, float r, float w) { return smoothstep(w, 0.0, abs(d - r)); }
void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	float d = length(p);
	float a = atan(p.y, p.x) - spin;
	// 转轮本体
	float body = smoothstep(0.93, 0.90, d);
	vec3 col = mix(vec3(0.11, 0.02, 0.04), vec3(0.03, 0.0, 0.01), d);
	float alpha = body;
	// 弹膛
	float shown = 0.0, glow = 0.0, hole = 0.0, rimc = 0.0;
	for (int i = 0; i < 6; i++) {
		float ang = spin - TAU * 0.25 + float(i) * TAU / 6.0;
		vec2 c = 0.55 * vec2(cos(ang), sin(ang));
		float dc = length(p - c);
		float on = clamp(loaded - float(i), 0.0, 1.0);
		float f = flash * (1.0 - step(0.5, abs(float(i) - flash_slot)));
		hole += smoothstep(0.2, 0.17, dc);
		rimc += ring(dc, 0.2, 0.025);
		shown += on * smoothstep(0.15, 0.11, dc);                  // 弹头
		shown += on * ring(dc, 0.07, 0.03) * 0.6;                  // 底火
		glow += (on * 0.6 + f * 1.5) * smoothstep(0.32, 0.0, dc);
	}
	col = mix(col, vec3(0.0), clamp(hole, 0.0, 1.0));
	col += vec3(1.0, 0.1, 0.12) * clamp(rimc, 0.0, 1.0) * 0.8;
	col = mix(col, vec3(1.0, 0.93, 0.85), clamp(shown, 0.0, 1.0));
	col += vec3(1.0, 0.08, 0.12) * glow * 0.9;
	// 外圈红描边 + 中心轴
	float rim = ring(d, 0.91, 0.035);
	col = mix(col, vec3(1.0, 0.08, 0.14), rim);
	col = mix(col, vec3(0.9, 0.85, 0.8), smoothstep(0.1, 0.07, d));
	alpha = max(alpha, rim) + glow * 0.4 * (1.0 - body);
	COLOR = vec4(col * mix(0.45, 1.0, dim), clamp(alpha, 0.0, 1.0) * mix(0.55, 1.0, dim));
}" };

	public readonly Control Root;
	private readonly ColorRect _rect;
	private readonly Label _more;
	private int _target, _shownCount;
	private float _loaded, _spin, _spinTarget, _flash, _flashSlot, _dim = 1f;

	public AmmoView(Node parent, float size = 96f)
	{
		Root = new Control { Name = "P5Ammo", MouseFilter = Control.MouseFilterEnum.Ignore, Size = new Vector2(size, size) };
		parent.AddChild(Root);
		_rect = new ColorRect
		{
			Size = new Vector2(size, size), MouseFilter = Control.MouseFilterEnum.Ignore,
			Material = new ShaderMaterial { Shader = Cylinder },
		};
		Root.AddChild(_rect);
		_more = Fx.Text(Root, "", 30, Pal5.Hot, new Color(0f, 0f, 0f), 8);
		_more.Position = new Vector2(size * 0.78f, size * 0.62f);
		_more.Rotation = -0.15f;
	}

	public void Set(int count) => _target = Mathf.Max(0, count);

	public void Tick(float delta)
	{
		if (_target != _shownCount)
		{
			// 一发一发地装 / 退：每发转一格、闪一下
			int step = _target > _shownCount ? 1 : -1;
			_shownCount += step;
			_spinTarget += step * Mathf.Tau / 6f;
			_flash = 1f;
			_flashSlot = Mathf.Clamp(step > 0 ? _shownCount - 1 : _shownCount, 0, 5);
		}
		float k = 1f - Mathf.Exp(-delta * 18f);
		_loaded = Mathf.Lerp(_loaded, Mathf.Min(_shownCount, 6), k);
		_spin = Mathf.Lerp(_spin, _spinTarget, 1f - Mathf.Exp(-delta * 14f));
		_flash = Mathf.Max(0f, _flash - delta * 4f);
		_dim = Mathf.Lerp(_dim, _shownCount > 0 ? 1f : 0f, k);
		var m = (ShaderMaterial)_rect.Material;
		m.SetShaderParameter("loaded", _loaded);
		m.SetShaderParameter("spin", _spin);
		m.SetShaderParameter("flash", _flash);
		m.SetShaderParameter("flash_slot", _flashSlot);
		m.SetShaderParameter("dim", _dim);
		_more.Text = _shownCount > 6 ? $"×{_shownCount}" : "";
		Root.Scale = Vector2.One * (1f + 0.08f * _flash);
		Root.PivotOffset = Root.Size / 2f;
	}
}
