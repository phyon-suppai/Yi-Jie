using Godot;

/// <summary>
/// HUD(纯代码自绘,零贴图/零 UI 场景):左上精力条 + 成就条 + 状态名 + debuff 标签;
/// 左下三枚武器冷却格(1 行动/2 表达/3 接受,按各自身份色)。由 GameManager 每帧喂数据。
/// </summary>
public partial class Hud : Node2D
{
	private float _energyFrac;
	private float _achieveFrac;
	private readonly float[] _cd = new float[3]; // 0 就绪;>0 剩余冷却
	private Font _font;

	public override void _Ready()
	{
		ZIndex = 200;
		_font = new SystemFont
		{
			FontNames = new[] { "Noto Sans CJK SC", "Microsoft YaHei", "SimHei", "PingFang SC", "Source Han Sans SC" }
		};
	}

	/// <summary>GameManager 每帧调一次;仅在可视数值变化时重绘。</summary>
	public void Refresh(float energyFrac, float achieveFrac, float cdAct, float cdExpress, float cdAccept)
	{
		_energyFrac = energyFrac;
		_achieveFrac = achieveFrac;
		_cd[0] = cdAct;
		_cd[1] = cdExpress;
		_cd[2] = cdAccept;
		QueueRedraw();
	}

	public override void _Draw()
	{
		Vector2 vp = GetViewportRect().Size;
		float barW = Mathf.Min(vp.X * 0.30f, 380f);
		float barH = 20f;
		float x0 = 28f, y0 = 30f;

		DrawBar(new Vector2(x0, y0), new Vector2(barW, barH), _energyFrac,
			Palette.PlayerFrame, Palette.PlayerCore, "精力");
		DrawBar(new Vector2(x0, y0 + barH + 10f), new Vector2(barW, barH), _achieveFrac,
			Palette.PurpleFrame, Palette.PurpleCore, "成就");

		DrawWeaponSlots(vp);
		DrawLegend(vp);
	}

	private void DrawBar(Vector2 pos, Vector2 size, float frac, Color frame, Color core, string label)
	{
		// 槽底
		DrawRect(new Rect2(pos, size), Palette.HudSlot);
		DrawRect(new Rect2(pos, size), frame, false, 3f); // 亮框
		// 填充:亮色(暗芯只做槽底与边框对比,填充直接用亮档可读性好)
		float fill = Mathf.Clamp(frac, 0f, 1f) * (size.X - 8f);
		if (fill > 1f)
			DrawRect(new Rect2(pos.X + 4f, pos.Y + 4f, fill, size.Y - 8f), frame.Lerp(core, 0.35f));
		// 标签放在进度条右侧
		DrawString(_font, new Vector2(pos.X + size.X + 8f, pos.Y + size.Y - 2f),
			label, HorizontalAlignment.Left, 80f, 13, Palette.TextDim);
	}

	private void DrawWeaponSlots(Vector2 vp)
	{
		float s = 46f, gap = 12f;
		float x = 28f;
		float y = vp.Y - s - 24f;
		string[] keys = { "J", "K", "L" };
		var colors = new (Color f, Color c)[]
		{
			Palette.ForWeapon(WeaponType.Act),
			Palette.ForWeapon(WeaponType.Express),
			Palette.ForWeapon(WeaponType.Accept)
		};
		float[] cooldowns = { ReactionTable.ActCooldown, ReactionTable.ExpressCooldown, ReactionTable.AcceptCooldown };

		for (int i = 0; i < 3; i++)
		{
			Vector2 o = new Vector2(x + i * (s + gap), y);
			(Color f, Color c) = colors[i];
			bool ready = _cd[i] <= 0.001f;
			DrawRect(new Rect2(o, new Vector2(s, s)), c);
			if (!ready)
			{
				// 冷却中:亮框熄灭,只剩暗芯 + 剩余进度细线
				float p = Mathf.Clamp(1f - _cd[i] / Mathf.Max(cooldowns[i], 0.01f), 0f, 1f);
				DrawRect(new Rect2(o, new Vector2(s, s)), f, false, 2f);
				if (p > 0.02f)
					DrawRect(new Rect2(o.X + 4f, o.Y + 4f, (s - 8f) * p, 5f), f);
			}
			else
			{
				DrawRect(new Rect2(o, new Vector2(s, s)), f, false, 5f);
			}
			DrawString(_font, o + new Vector2(9f, s * 0.62f),
				keys[i], HorizontalAlignment.Left, 40f, 22, Palette.TextMain);
		}
	}

	private void DrawLegend(Vector2 vp)
	{
		float sq = 16f, gap = 8f, rowH = 20f, rowGap = 6f;
		int fontSize = 13;
		float x = 28f;
		float weaponY = vp.Y - 46f - 24f;
		float y = weaponY - 3f * (rowH + rowGap) - 4f;

		// 按颜色对齐：每行 = 武器色块 + 武器名/键 + 克 + 烦恼名 + 烦恼色块
		(WeaponType w, string weapon, string key, WorryType t, string worry)[] rows =
		{
			(WeaponType.Act, "行动", "J", WorryType.Doubt, "疑"),
			(WeaponType.Accept, "接受", "L", WorryType.Pressure, "焦"),
			(WeaponType.Express, "表达", "K", WorryType.Loneliness, "孤")
		};

		for (int i = 0; i < rows.Length; i++)
		{
			float rowY = y + i * (rowH + rowGap);
			float cx = x;

			// 武器色块
			(Color wf, Color wc) = Palette.ForWeapon(rows[i].w);
			DrawRect(new Rect2(cx, rowY, sq, sq), wc);
			DrawRect(new Rect2(cx, rowY, sq, sq), wf, false, 2f);
			cx += sq + gap;

			// 武器名+键
			string wText = $"{rows[i].weapon}({rows[i].key})";
			DrawString(_font, new Vector2(cx, rowY + sq * 0.7f), wText,
				HorizontalAlignment.Left, -1, fontSize, Palette.TextMain);
			cx += _font.GetStringSize(wText, fontSize: fontSize).X + gap;

			// 克制符号
			DrawString(_font, new Vector2(cx, rowY + sq * 0.7f), "克",
				HorizontalAlignment.Left, -1, fontSize, Palette.TextDim);
			cx += _font.GetStringSize("克", fontSize: fontSize).X + gap;

			// 烦恼名
			DrawString(_font, new Vector2(cx, rowY + sq * 0.7f), rows[i].worry,
				HorizontalAlignment.Left, -1, fontSize, Palette.TextMain);
			cx += _font.GetStringSize(rows[i].worry, fontSize: fontSize).X + gap;

			// 烦恼色块
			(Color nf, Color nc) = Palette.ForWorry(rows[i].t);
			DrawRect(new Rect2(cx, rowY, sq, sq), nc);
			DrawRect(new Rect2(cx, rowY, sq, sq), nf, false, 2f);
		}
	}
}
