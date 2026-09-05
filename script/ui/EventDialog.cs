using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 特殊事件三选一弹窗(游戏不暂停)。
/// 第一行标题+题目,第二行 J/K/L 三个选项左右中平分。
/// 选择后,选中的选项像烦恼消散那样快速渐亮再渐隐消失。
/// </summary>
public partial class EventDialog : CanvasLayer
{
	private SpecialEventData _data;
	private Action<int> _onChoice;

	private Label _title;
	private Label _desc;
	private readonly List<Label> _optionLabels = new();
	private readonly List<PanelContainer> _optionPanels = new();
	private readonly List<StyleBoxFlat> _optionStyles = new();

	private bool _resolving;
	private int _chosenIndex = -1;
	private double _flashT;
	private const double FlashDuration = 0.55;

	private Color _idleOptionBg = new Color("#2C1257");
	private Color _idleOptionBorder = new Color("#7A5FD0");
	private Color _correctColor = new Color("#39FF88"); // 荧光绿
	private Color _wrongColor = new Color("#FF3B3B");   // 荧光红

	public override void _Ready()
	{
		Layer = 100;

		var panel = new Panel();
		panel.SetAnchorsPreset(Control.LayoutPreset.Center);
		panel.Size = new Vector2(1180, 480);
		panel.Position = new Vector2(-590, -240);
		panel.AddThemeStyleboxOverride("panel", MakeStyle(new Color("#1A0F2E"), new Color("#C58CFF"), 5));
		AddChild(panel);

		Font font = new SystemFont
		{
			FontNames = new[] { "Noto Sans CJK SC", "Microsoft YaHei", "SimHei", "PingFang SC", "Source Han Sans SC" }
		};

		_title = new Label();
		_title.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		_title.OffsetLeft = 30; _title.OffsetTop = 20;
		_title.OffsetRight = -30; _title.OffsetBottom = 84;
		_title.HorizontalAlignment = HorizontalAlignment.Center;
		_title.VerticalAlignment = VerticalAlignment.Center;
		_title.AutowrapMode = TextServer.AutowrapMode.Word;
		_title.AddThemeFontOverride("font", font);
		_title.AddThemeFontSizeOverride("font_size", 34);
		_title.AddThemeColorOverride("font_color", new Color("#FFE05C"));
		panel.AddChild(_title);

		_desc = new Label();
		_desc.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		_desc.OffsetLeft = 60; _desc.OffsetTop = 92;
		_desc.OffsetRight = -60; _desc.OffsetBottom = 190;
		_desc.HorizontalAlignment = HorizontalAlignment.Center;
		_desc.VerticalAlignment = VerticalAlignment.Center;
		_desc.AutowrapMode = TextServer.AutowrapMode.Word;
		_desc.AddThemeFontOverride("font", font);
		_desc.AddThemeFontSizeOverride("font_size", 22);
		_desc.AddThemeColorOverride("font_color", new Color("#F1E9FF"));
		panel.AddChild(_desc);

		float areaTop = 210, areaBottom = 440;
		float areaLeft = 36, areaRight = panel.Size.X - 36;
		float width = areaRight - areaLeft;
		for (int i = 0; i < 3; i++)
		{
			var box = new PanelContainer();
			float x0 = areaLeft + i * width / 3f;
			float x1 = areaLeft + (i + 1) * width / 3f;
			box.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
			box.Position = new Vector2(x0, areaTop);
			box.Size = new Vector2(x1 - x0 - 12f, areaBottom - areaTop);

			var style = MakeStyle(_idleOptionBg, _idleOptionBorder, 3);
			box.AddThemeStyleboxOverride("panel", style);

			var lbl = new Label();
			lbl.HorizontalAlignment = HorizontalAlignment.Center;
			lbl.VerticalAlignment = VerticalAlignment.Center;
			lbl.AutowrapMode = TextServer.AutowrapMode.Word;
			lbl.AddThemeFontOverride("font", font);
			lbl.AddThemeFontSizeOverride("font_size", 19);
			lbl.AddThemeColorOverride("font_color", new Color("#FFFFFF"));
			box.AddChild(lbl);

			panel.AddChild(box);
			_optionPanels.Add(box);
			_optionStyles.Add(style);
			_optionLabels.Add(lbl);
		}
	}

	private static StyleBoxFlat MakeStyle(Color bg, Color border, int borderWidth)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
			BorderColor = border,
			BorderWidthBottom = borderWidth,
			BorderWidthLeft = borderWidth,
			BorderWidthRight = borderWidth,
			BorderWidthTop = borderWidth,
			CornerRadiusBottomLeft = 12,
			CornerRadiusBottomRight = 12,
			CornerRadiusTopLeft = 12,
			CornerRadiusTopRight = 12
		};
	}

	public void ShowEvent(SpecialEventData data, Action<int> onChoice)
	{
		_data = data;
		_onChoice = onChoice;
		_resolving = false;
		_chosenIndex = -1;

		_title.Text = data.Title;
		_desc.Text = data.Description;

		for (int i = 0; i < 3; i++)
		{
			string key = i == 0 ? "J" : i == 1 ? "K" : "L";
			string prefix = i == 0 ? "A" : i == 1 ? "B" : "C";
			_optionLabels[i].Text = $"[{key}] {prefix}：{data.Options[i].Label}";
			_optionPanels[i].Show();
			_optionPanels[i].Modulate = Colors.White;
			_optionStyles[i].BgColor = _idleOptionBg;
			_optionStyles[i].BorderColor = _idleOptionBorder;
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (_resolving || _data == null) return;

		if (@event.IsActionPressed("action 1")) { Choose(0); GetViewport().SetInputAsHandled(); }
		else if (@event.IsActionPressed("action 2")) { Choose(1); GetViewport().SetInputAsHandled(); }
		else if (@event.IsActionPressed("action 3")) { Choose(2); GetViewport().SetInputAsHandled(); }
	}

	private void Choose(int index)
	{
		_resolving = true;
		_chosenIndex = index;
		_flashT = 0.0;

		// 把另外两项变成灰色并压低透明度,让玩家只看选中项
		for (int i = 0; i < 3; i++)
		{
			if (i == index)
			{
				bool correct = _data.Options[i].IsCorrect;
				_optionStyles[i].BgColor = correct ? _correctColor : _wrongColor;
				_optionStyles[i].BorderColor = Colors.White;
				_optionLabels[i].AddThemeColorOverride("font_color", new Color("#1A0F2E"));
			}
			else
			{
				_optionPanels[i].Modulate = new Color(0.3f, 0.3f, 0.3f, 0.35f);
			}
		}

		_onChoice?.Invoke(index);
	}

	public override void _Process(double delta)
	{
		if (!_resolving || _chosenIndex < 0) return;

		_flashT += delta;
		double half = FlashDuration * 0.5;
		float alpha;
		if (_flashT < half)
		{
			// 前半段渐亮
			alpha = Mathf.Clamp((float)(_flashT / half), 0f, 1f);
		}
		else
		{
			// 后半段渐隐消失
			alpha = Mathf.Clamp(1f - (float)((_flashT - half) / half), 0f, 1f);
		}

		var chosen = _optionPanels[_chosenIndex];
		chosen.Modulate = new Color(
			Mathf.Min(1f, chosen.Modulate.R + 0.5f),
			Mathf.Min(1f, chosen.Modulate.G + 0.5f),
			Mathf.Min(1f, chosen.Modulate.B + 0.5f),
			alpha);

		if (_flashT >= FlashDuration)
			QueueFree();
	}
}
