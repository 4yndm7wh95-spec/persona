using System;
using System.Linq;
using Godot;

namespace P5Joker;

/// <summary>
/// 牌堆排列切换（PileScreen 用，预览也用）：白 / 黑两半剪报纸片，左「原版顺序」右「按类型」（P5 字图），红色滑块滑到选中的一侧；点一下切换。
/// 原版顺序 = 尖塔原版牌堆界面的顺序（PileScreen.VanillaOrder：直接读原版网格），按类型 = 攻击 / 技能 / 能力…分段；每个牌堆各自记住（P5Settings）。
/// </summary>
public sealed class PileModeSwitch
{
	private static readonly Color Ink = PileDraw.Ink, Paper = PileDraw.Paper, Red = new(0.9f, 0.1f, 0.11f);
	private const float W = 300f, H = 46f;
	public Action<bool>? Changed;
	private Node2D root = null!;
	private AaPoly knob = null!;
	private Node2D left = null!, right = null!;
	private float k;
	private bool order;

	public static PileModeSwitch Build(Node2D parent, Vector2 at, float k, bool order)
	{
		PileModeSwitch m = new() { k = k, order = order };
		m.root = new Node2D { Position = at, Rotation = Mathf.DegToRad(-2f) };
		parent.AddChild(m.root);
		// 白边：消耗堆 / 弃牌堆的切换放在黑块上，没有白边就和黑底糊成一片（同段标题的白边）
		PileDraw.Poly(m.root, new[] { new Vector2(-4f, 0f), new Vector2(W + 4f, -4f), new Vector2(W + 8f, H + 4f), new Vector2(-2f, H + 8f) }.Select(p => p * k).ToArray(), Paper);
		PileDraw.Poly(m.root, new[] { new Vector2(0f, 4f), new Vector2(W, 0f), new Vector2(W + 4f, H), new Vector2(2f, H + 4f) }.Select(p => p * k).ToArray(), Ink);
		m.knob = new AaPoly { Color = Red, Polygon = new[] { new Vector2(4f, 6f), new Vector2(W / 2f, 3f), new Vector2(W / 2f + 2f, H - 2f), new Vector2(6f, H) }.Select(p => p * k).ToArray() };
		m.root.AddChild(m.knob);
		// 文字：P5 字图（原版顺序 / 按类型），各自在半边里居中
		m.left = new Node2D();
		m.right = new Node2D();
		m.root.AddChild(m.left);
		m.root.AddChild(m.right);
		float th = 30f * k;
		foreach ((Node2D n, string key, float cx) in new[] { (m.left, "order", W * 0.25f), (m.right, "bytype", W * 0.75f) })
		{
			float tw = PileText.RowWidth(th, 0f, PileText.Pick(key));
			PileText.Word(n, key, new Vector2(cx * k - tw * 0.5f, (H * k - th) * 0.5f + 2f * k), th, Colors.White);
		}
		Control hit = new() { Size = new Vector2(W, H) * k, MouseFilter = Control.MouseFilterEnum.Stop, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
		m.root.AddChild(hit);
		hit.MouseEntered += () => m.root.Modulate = new Color(1.12f, 1.12f, 1.12f);
		hit.MouseExited += () => m.root.Modulate = Colors.White;
		hit.GuiInput += ev => Stage.Safe("pile mode switch", () =>
		{
			if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
			{
				hit.AcceptEvent();
				bool want = mb.Position.X < W * k / 2f;
				m.Changed?.Invoke(want == m.order ? !m.order : want);
			}
		});
		m.Paint(1f);
		return m;
	}

	public void Set(bool toOrder)
	{
		order = toOrder;
		Fx.Animate(knob, 0, 0.32, t => Paint(Ease5.OutExpo(t)));
	}

	private void Paint(float t)
	{
		float from = order ? W / 2f : 0f, to = order ? 0f : W / 2f;
		knob.Position = new Vector2(Mathf.Lerp(from, to, t) * k, 0f);
		left.Modulate = order ? Paper : new Color(0.55f, 0.55f, 0.55f);
		right.Modulate = order ? new Color(0.55f, 0.55f, 0.55f) : Paper;
	}
}
