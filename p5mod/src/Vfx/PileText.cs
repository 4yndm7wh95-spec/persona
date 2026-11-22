using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 牌堆界面的 P5 风格文字（用户 2026-11-22：UI 上固定的字都用 P5 那种字，不用普通字体）。
/// 字图：art/p5/ui/pile_text（Codex 照原版菜单字排的白字透明底，打包成 res://p5joker/fx/pile_t_&lt;名字&gt;.png，另有黑描边版 _ol）；
/// 数字和乘号用原版暂停菜单的小数字 / ×（P5CAMP_00SPD 00096–00105、00107 → pile_sd_0..9、pile_sx，也有 _ol）。
/// 白字图用 Modulate 上色；ol = true 时先垫一层黑描边。
/// </summary>
public static class PileText
{
	private static bool Zh => TranslationServer.GetLocale().StartsWith("zh");

	/// <summary>按语言选 _zh 版（没有中文版的词直接用英文名）。</summary>
	public static string Pick(string name) => Zh && Fx.Tex("pile_t_" + name + "_zh") != null ? name + "_zh" : name;

	/// <summary>一张字图，高 height 像素（1080p，已乘 k），左上角在 (0,0)。返回宽度。</summary>
	public static float Word(Node2D parent, string name, Vector2 at, float height, Color color, bool ol = false) =>
		Piece(parent, "pile_t_" + Pick(name), at, height, color, ol);

	/// <summary>指定哪一张就用哪一张（不按语言换成 _zh）。</summary>
	public static float Exact(Node2D parent, string name, Vector2 at, float height, Color color, bool ol = false) =>
		Piece(parent, "pile_t_" + name, at, height, color, ol);

	/// <summary>原版小数字拼成的数（可带前缀 ×）。返回宽度。</summary>
	public static float Number(Node2D parent, string text, Vector2 at, float height, Color color, bool ol = false)
	{
		float x = 0f;
		foreach (char c in text)
		{
			string tex = c == '×' ? "pile_sx" : c >= '0' && c <= '9' ? "pile_sd_" + c : "";
			if (tex.Length == 0) { x += height * 0.3f; continue; }
			x += Piece(parent, tex, at + new Vector2(x, 0f), height, color, ol) * 0.82f;   // 原版小数字之间有一点叠
		}
		return x;
	}

	/// <summary>一行：依次排字图 / 数字（"#123" 表示数字、"×5" 表示乘号 + 数字，其余是字图名），中间留 gap。返回总宽。</summary>
	public static float Row(Node2D parent, Vector2 at, float height, Color color, bool ol, float gap, params string[] parts)
	{
		float x = 0f;
		foreach (string p in parts)
		{
			float w = p.StartsWith('#') ? Number(parent, p[1..], at + new Vector2(x, height * 0.12f), height * 0.82f, color, ol)
				: p.StartsWith('×') ? Number(parent, p, at + new Vector2(x, height * 0.12f), height * 0.82f, color, ol)
				: Word(parent, p, at + new Vector2(x, 0f), height, color, ol);
			x += w + gap;
		}
		return x - gap;
	}

	/// <summary>先算一行的宽度（不画）。</summary>
	public static float RowWidth(float height, float gap, params string[] parts)
	{
		Node2D tmp = new();
		float w = Row(tmp, Vector2.Zero, height, Colors.White, false, gap, parts);
		tmp.Free();
		return w;
	}

	private static float Piece(Node2D parent, string tex, Vector2 at, float height, Color color, bool ol)
	{
		if (Fx.Tex(tex) is not Texture2D t) return 0f;
		float s = height / t.GetHeight();
		if (ol && Fx.Tex(tex + "_ol") is Texture2D o)
		{
			// 描边版四周多了 pad 像素（打包时加的）：按高度比例对齐中心
			float so = s * t.GetHeight() / (o.GetHeight() - 2f * OlPad) ;
			parent.AddChild(new Sprite2D { Texture = o, Centered = false, Position = at - new Vector2(OlPad, OlPad) * so, Scale = Vector2.One * so, Modulate = new Color(0.04f, 0.04f, 0.04f, color.A), TextureFilter = CanvasItem.TextureFilterEnum.Linear });
		}
		parent.AddChild(new Sprite2D { Texture = t, Centered = false, Position = at, Scale = Vector2.One * s, Modulate = color, TextureFilter = CanvasItem.TextureFilterEnum.Linear });
		return t.GetWidth() * s;
	}

	/// <summary>打包时描边版每边多出的像素（tools/p5/stage_p5.py 的 PILE_TEXT_OL）。</summary>
	public const float OlPad = 8f;

	/// <summary>预热用：全部字图和数字。</summary>
	public static IEnumerable<string> Textures()
	{
		foreach (string n in Names)
		{
			yield return "pile_t_" + n;
			yield return "pile_t_" + n + "_ol";
		}
		for (int i = 0; i < 10; i++) { yield return "pile_sd_" + i; yield return "pile_sd_" + i + "_ol"; }
		yield return "pile_sx";
		yield return "pile_sx_ol";
	}

	private static readonly string[] Names =
	{
		"attack", "attack_zh", "skill", "skill_zh", "power", "power_zh", "status", "status_zh", "curse", "curse_zh",
		"draw", "draw_zh", "exhaust", "exhaust_zh", "discard", "discard_zh", "deck", "deck_zh",
		"order", "order_zh", "bytype", "bytype_zh", "back", "back_zh", "upgrade", "upgrade_zh", "upbase", "upbase_zh",
		"upgraded", "upgraded_zh", "inpile", "inpile_zh", "cost", "cards_zh", "empty", "menu_exhaust", "menu_discard", "menu_deck",
	};
}

/// <summary>每个牌堆界面的版式（1920×1080 参考像素）和装饰（入场 / 退场动画、碰牌反馈）。</summary>
public interface IPileDecor
{
	float GridX { get; }
	float GridTop { get; }
	float GridBottom { get; }
	int Cols { get; }
	float CardW { get; }
	float CellW { get; }
	float CellH { get; }
	float CardsStart { get; }
	float ExitTime { get; }
	Vector2 FocusAt { get; }
	/// <summary>焦点特写时说明文字的左上角（1080p）。</summary>
	Vector2 InfoAt { get; }
	/// <summary>排列切换开关的位置（1080p，左上角）。</summary>
	Vector2 SwitchAt { get; }
	/// <summary>牌入场的起点偏移（1080p，从这里弹到格子里）。</summary>
	Vector2 EnterOffset { get; }
	/// <summary>退场时牌往哪儿去（1080p 偏移）。</summary>
	Vector2 ExitOffset { get; }
	void Update(float now, float dt);
	void BeginExit(float now);
	void Kick();
}
