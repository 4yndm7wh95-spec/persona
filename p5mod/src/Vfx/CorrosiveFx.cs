using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 腐蚀波（稀有，毒流重点牌：开了之后这回合每抽一张牌都给全体上毒）。
/// 2026-11-18 第六版「咒爪齐撕」。第五版（贴地的咒毒潮、一滩一滩的毒）被否：用户要挂毒那一下有冲击力、有打击感，不要偏在地上。
/// · 每抽一张牌（Strike）：所有敌人同一刻被撕开三道咒怨爪痕（致命毒药那套用户认可的笔触），咒毒从伤口里炸开 ——
///   涂毒那团翻滚的咒毒在它身上一胀再爆开、往外迸碎光、毒液往下滴，身上白闪 + 暗红、身体暗一下；顿帧、震屏。
///   连抽时一下接一下（间隔 0.12s），只有每批的第一下最重。
/// · 打出（Plunge）：亚森出手的那一刻，Joker 身前空中被撕开同样的三道大爪痕、咒毒炸开（这回合「爪子」已经亮出来了）。
/// · 持续（Aura）：Joker 身上隔一会儿泛一下很淡的暗红（不放在地上）。
/// </summary>
public static class CorrosiveFx
{
	/// <summary>一个敌人：中心、头顶、宽度、本体（染色用）。</summary>
	public record struct Target(Vector2 At, Vector2 Top, float Width, Node2D? Body);

	private static ulong _suppressUntil, _nextWaveAt, _auraNext;

	/// <summary>抽牌带来的上毒正在由咒爪表现：PoisonFx.Apply 这段时间不放普通落点。</summary>
	public static bool Suppressing => Stage.NowMs() < _suppressUntil;

	private static readonly Color Hot = new(1f, 0.95f, 0.94f), Red = new(1f, 0.1f, 0.18f), Deep = new(0.45f, 0.02f, 0.08f);

	private static float U(Control layer) => Fx.Screen(layer).Y / 1080f;

	/// <summary>咒毒炸开：涂毒那团翻滚的咒毒在 at 一下胀起来（0.07s）再炸开散掉；碎光往外迸、毒液往下滴到 groundY。size = 直径。</summary>
	private static void Burst(Control layer, Vector2 at, float size, float groundY, float power)
	{
		float u = U(layer);
		Node2D root = Fx.Root(layer, at, "P5VenomBurst", 0.5);
		ColorRect q = CardSkillFx.VenomBlob(root, size);
		ShaderMaterial m = (ShaderMaterial)q.Material;
		float seed = Fx.Rand(0f, 30f);
		Fx.Animate(root, 0, 0.42, t =>
		{
			float s = t * 0.42f;
			float swell = Ease5.OutExpo(Mathf.Min(1f, s / 0.07f));
			float pop = Ease5.Seg(s, 0.07f, 0.3f);
			root.Scale = Vector2.One * (0.6f + 0.5f * swell + 0.7f * Fx.OutCubic(pop));
			m.SetShaderParameter("clock", seed + s * 4f);
			m.SetShaderParameter("grow", swell);
			m.SetShaderParameter("alpha", 1f - Fx.OutCubic(pop));
		});
		Glow.Flash(layer, at, Hot, size * 0.55f, 0.1f);
		Glow.Flash(layer, at, new Color(0.9f, 0.05f, 0.14f), size * 1.5f, 0.35f);
		Phantom.Shards(layer, at, (int)(12 * power), 380f * u, 1000f * u * power, Vector2.Up, 180f, 0.4f, 900f * u, 18f * u * power, 0.06f);
		for (int i = 0; i < 3; i++)
			CardSkillFx.Drop(layer, at + new Vector2(Fx.Rand(-0.25f, 0.25f) * size, Fx.Rand(0f, 0.2f) * size), groundY, 0.1f + 0.08f * i, size * 2f, u);
	}

	/// <summary>打出：亚森出手的一刻，Joker 身前空中撕开三道大咒爪、咒毒炸开。claw = 亚森爪尖（没有就用空中一点），ground = Joker 身前地面。</summary>
	public static void Plunge(Control layer, Vector2 claw, Vector2 ground)
	{
		float u = U(layer);
		Vector2 at = ground + new Vector2((ground.X >= claw.X ? 1f : -1f) * 220f * u, -330f * u);   // 往前挪，别盖住 Joker 自己
		Vector2 dir = at - claw;
		if (dir.LengthSquared() < 1f) dir = Vector2.Right;
		Sfx.Play(Sfx.Slam, 0.6f);
		Sfx.Play(Sfx.SubBoom, 0.6f);
		Sfx.Play(Sfx.Curse, 0.45f);
		PoisonFx.ClawRake(layer, at, 360f * u, dir);
		Fx.After(layer, PoisonFx.RakeAt, () =>
		{
			HitStop.Request(0.07);
			Stage.ShakeDir?.Invoke(3, 90f);
			Burst(layer, at, 300f * u, ground.Y, 1.4f);
			SkillRedo5.Cross(layer, at, 320f * u);
		});
	}

	/// <summary>持续：每帧调用（腐蚀波生效时）；约每 0.8s 在 Joker 身上泛一下很淡的暗红（不放在地上）。</summary>
	public static void Aura(Control layer, Vector2 feet)
	{
		ulong now = Stage.NowMs();
		if (now < _auraNext)
		{
			return;
		}
		_auraNext = now + 800;
		float u = U(layer);
		Glow.Flash(layer, feet + new Vector2(0f, -260f * u), Deep, 420f * u, 0.7f);
	}

	/// <summary>
	/// 抽到一张牌（CorrosiveDrawPatch，在原版给全体上毒之前）：排一下咒爪齐撕。
	/// 连抽时一下接一下（间隔 0.12s）；之后 0.35s 内（加上排队时间）的上毒都由这里表现。
	/// </summary>
	public static void OnDraw(Control layer, Vector2 feet, float facing, IReadOnlyList<Target> targets)
	{
		ulong now = Stage.NowMs();
		ulong start = _nextWaveAt > now ? _nextWaveAt : now;
		_nextWaveAt = start + 120;
		_suppressUntil = System.Math.Max(_suppressUntil, start + 350);
		bool first = start == now;
		Fx.After(layer, (start - now) / 1000.0, () => Strike(layer, facing, targets, first));
	}

	private static void Strike(Control layer, float facing, IReadOnlyList<Target> targets, bool first)
	{
		if (targets.Count == 0)
		{
			return;
		}
		float u = U(layer);
		// 声音（用户：腐蚀波刺耳）：每下只有一声挥动 + 低频 + 一声柔和的咒怨闷响
		Sfx.Play(Sfx.SwishLow, first ? 0.5f : 0.35f);
		Sfx.Play(Sfx.SubBoom, first ? 0.45f : 0.28f);
		Fx.After(layer, PoisonFx.RakeAt, () =>
		{
			Sfx.Play(Sfx.CurseSoft, first ? 0.6f : 0.42f);
			HitStop.Request(first ? 0.06 : 0.025);
			Stage.ShakeDir?.Invoke(first ? 2 : 1, 0f);
		});
		foreach (Target t in targets)
		{
			Target tt = t;
			float h = Mathf.Max(80f, (t.At - t.Top).Length());
			float k = Mathf.Clamp(t.Width / 240f, 0.6f, 1.3f);
			Vector2 at = t.At + new Vector2(0f, -h * 0.1f);
			PoisonFx.ClawRake(layer, at, t.Width, new Vector2(facing, 0.3f));
			Fx.After(layer, PoisonFx.RakeAt, () =>
			{
				Burst(layer, at, Mathf.Max(tt.Width * 0.9f, 160f * u) * (first ? 1f : 0.8f), tt.At.Y + h * 0.75f, first ? 1f : 0.7f);
				BodyTint.Pulse(tt.Body, new Color(0.72f, 0.45f, 0.55f), 0.1, 0.35);
			});
		}
	}
}
