using System;
using System.Threading.Tasks;
using Godot;

namespace P3TestMod;

/// <summary>
/// 模拟真人鼠标：光标沿一条略弯的曲线、先快后慢地移动（每帧移动真实系统光标 + 发一个鼠标移动事件），
/// 按下 / 松开发真实的鼠标按键事件。游戏走的是和玩家手动操作完全相同的出牌流程（悬停抬牌、拖动、瞄准箭头、松手打出）。
/// 坐标一律是游戏视口坐标（1920×1080 基准，和截图一致）。录制时不要碰鼠标。
/// </summary>
public static class Mouse
{
	private static Vector2? _pos;
	private static bool _leftDown;

	private static SceneTree Tree => (SceneTree)Engine.GetMainLoop();
	private static Window Root => Tree.Root;

	public static Vector2 Pos => _pos ?? Root.GetMousePosition();
	public static Vector2 ViewSize => Root.GetVisibleRect().Size;

	/// <summary>视口坐标 → 窗口像素（输入事件用窗口像素；全屏 1920×1080 时两者相同）。</summary>
	private static Vector2 ToWindow(Vector2 p) => Root.GetFinalTransform() * p;

	public static void Set(Vector2 p)
	{
		Vector2 size = ViewSize;
		p = new Vector2(Mathf.Clamp(p.X, 1f, size.X - 2f), Mathf.Clamp(p.Y, 1f, size.Y - 2f));
		Vector2 last = Pos;
		_pos = p;
		if (Input.MouseMode != Input.MouseModeEnum.Visible)
		{
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}
		Root.WarpMouse(p);
		Vector2 w = ToWindow(p);
		Input.ParseInputEvent(new InputEventMouseMotion
		{
			Position = w,
			GlobalPosition = w,
			Relative = w - ToWindow(last),
			ButtonMask = _leftDown ? MouseButtonMask.Left : 0,
		});
	}

	public static void Button(bool pressed)
	{
		_leftDown = pressed;
		Vector2 w = ToWindow(Pos);
		Input.ParseInputEvent(new InputEventMouseButton
		{
			ButtonIndex = MouseButton.Left,
			Pressed = pressed,
			Position = w,
			GlobalPosition = w,
			ButtonMask = pressed ? MouseButtonMask.Left : 0,
		});
	}

	/// <summary>移动到目标（目标每帧重新取，跟得上会动的牌）。arc：弯曲程度（相对距离），0 为直线。</summary>
	public static async Task MoveTo(Func<Vector2> target, int ms, float arc = 0.08f)
	{
		Vector2 from = Pos;
		float bend = GD.Randf() < 0.5f ? -arc : arc;
		ulong t0 = Time.GetTicksMsec();
		while (true)
		{
			float u = ms <= 0 ? 1f : Mathf.Clamp((Time.GetTicksMsec() - t0) / (float)ms, 0f, 1f);
			Vector2 to = target();
			Vector2 d = to - from;
			Vector2 ctrl = (from + to) * 0.5f + new Vector2(-d.Y, d.X) * bend;
			float e = u * u * u * (u * (u * 6f - 15f) + 10f);              // smootherstep：起步、收尾都柔和
			float k = 1f - e;
			Set(from * (k * k) + ctrl * (2f * k * e) + to * (e * e));
			if (u >= 1f)
			{
				return;
			}
			await Frame();
		}
	}

	public static Task MoveTo(Vector2 target, int ms, float arc = 0.08f) => MoveTo(() => target, ms, arc);

	public static async Task Click(int holdMs = 80)
	{
		Button(true);
		await Sleep(holdMs);
		Button(false);
	}

	/// <summary>按距离算一个自然的移动时长（ms）。</summary>
	public static int Duration(Vector2 to, double speed, int min = 260, int max = 820)
	{
		float dist = Pos.DistanceTo(to);
		return (int)(Mathf.Clamp(220f + dist * 0.42f, min, max) / Math.Max(0.1, speed));
	}

	public static async Task Frame() => await Tree.ToSignal(Tree, SceneTree.SignalName.ProcessFrame);

	public static async Task Sleep(int ms)
	{
		ulong end = Time.GetTicksMsec() + (ulong)Math.Max(0, ms);
		do
		{
			await Frame();
		}
		while (Time.GetTicksMsec() < end);
	}

	/// <summary>Control 在视口坐标里的中心点（含所在画布 / 镜头的变换）。</summary>
	public static Vector2 Center(Control c) => c.GetGlobalTransformWithCanvas() * (c.Size * 0.5f);
}
