using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 特殊事件三选一弹窗(游戏不暂停)。
/// 视觉学习示例图:深色面板 + 白色标题 + 垂直大按钮 + 底部细白倒计时条。
/// 选项默认灰色底,选择后:正确闪绿/错误闪红,其余变暗,弹窗快速消失。
/// </summary>
public partial class EventDialog : CanvasLayer
{
	private SpecialEventData _data;
	private Action<int> _onChoice;
	private Action _onClosed;

	private Label _title;
	private Label _desc;
	private readonly List<Button> _optionButtons = new();
	private readonly List<StyleBoxFlat> _optionStyles = new();
	private ProgressBar _timerBar;

	private bool _resolving;
	private int _chosenIndex = -1;
	private double _flashT;
	private double _elapsed;

	/// <summary>显示槽 i 实际指向 Options 的原始下标(每次 ShowEvent 洗牌一次,实现 J/K/L 选项随机排列)。</summary>
	private readonly int[] _displayMap = new int[3];
	private static readonly System.Random _rng = new();
	private const double FlashDuration = 0.55;
	private const double ChoiceTimeout = 6.0; // 超时自动选 A(J)

	private Color _idleBg = new Color("#1E1E2E");
	private Color _idleBorder = new Color("#5A5A6E");
	private Color _correctBorder = new Color("#4DFF88");
	private Color _correctGlow = new Color("#4DFF88", 0.55f);
	private Color _wrongBorder = new Color("#FF4D6D");
	private Color _wrongGlow = new Color("#FF4D6D", 0.55f);

	public override void _Ready()
	{
		Layer = 100;

		var panel = new Panel();
		panel.SetAnchorsPreset(Control.LayoutPreset.Center);
		panel.Size = new Vector2(860, 520);
		panel.Position = new Vector2(-430, -260);
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color("#101018"),
			BorderColor = new Color("#6A6A8A"),
			BorderWidthBottom = 2,
			BorderWidthLeft = 2,
			BorderWidthRight = 2,
			BorderWidthTop = 2,
			CornerRadiusBottomLeft = 16,
			CornerRadiusBottomRight = 16,
			CornerRadiusTopLeft = 16,
			CornerRadiusTopRight = 16
		});
		AddChild(panel);

		var column = new VBoxContainer();
		column.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		column.OffsetLeft = 42;
		column.OffsetTop = 32;
		column.OffsetRight = -42;
		column.OffsetBottom = -32;
		column.AddThemeConstantOverride("separation", 18);
		panel.AddChild(column);

		Font font = new SystemFont
		{
			FontNames = new[] { "Noto Sans CJK SC", "Microsoft YaHei", "SimHei", "PingFang SC", "Source Han Sans SC" }
		};

		_title = new Label();
		_title.HorizontalAlignment = HorizontalAlignment.Center;
		_title.VerticalAlignment = VerticalAlignment.Center;
		_title.AutowrapMode = TextServer.AutowrapMode.Word;
		_title.AddThemeFontOverride("font", font);
		_title.AddThemeFontSizeOverride("font_size", 30);
		_title.AddThemeColorOverride("font_color", new Color("#FFFFFF"));
		column.AddChild(_title);

		_desc = new Label();
		_desc.HorizontalAlignment = HorizontalAlignment.Center;
		_desc.VerticalAlignment = VerticalAlignment.Center;
		_desc.AutowrapMode = TextServer.AutowrapMode.Word;
		_desc.AddThemeFontOverride("font", font);
		_desc.AddThemeFontSizeOverride("font_size", 18);
		_desc.AddThemeColorOverride("font_color", new Color("#CCCCDD"));
		column.AddChild(_desc);

		var buttonColumn = new VBoxContainer();
		buttonColumn.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		buttonColumn.AddThemeConstantOverride("separation", 18);
		column.AddChild(buttonColumn);

		for (int i = 0; i < 3; i++)
		{
			var btn = new Button();
			btn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
			btn.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			btn.CustomMinimumSize = new Vector2(776, 0);
			btn.AddThemeFontOverride("font", font);
			btn.AddThemeFontSizeOverride("font_size", 22);
			btn.AddThemeColorOverride("font_color", new Color("#FFFFFF"));
			btn.AddThemeColorOverride("font_hover_color", new Color("#FFFFFF"));
			btn.AddThemeColorOverride("font_pressed_color", new Color("#FFFFFF"));

			var style = MakeGlowStyle(_idleBg, _idleBorder, new Color("#000000", 0f));
			btn.AddThemeStyleboxOverride("normal", style);
			btn.AddThemeStyleboxOverride("hover", style);
			btn.AddThemeStyleboxOverride("pressed", style);
			buttonColumn.AddChild(btn);

			int captured = i;
			btn.Pressed += () => Choose(captured);
			_optionButtons.Add(btn);
			_optionStyles.Add(style);
		}

		_timerBar = new ProgressBar();
		_timerBar.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		_timerBar.CustomMinimumSize = new Vector2(776, 2);
		_timerBar.MaxValue = ChoiceTimeout;
		_timerBar.Value = ChoiceTimeout;
		_timerBar.ShowPercentage = false;
		_timerBar.AddThemeStyleboxOverride("background", new StyleBoxFlat
		{
			BgColor = new Color("#303040"),
			CornerRadiusBottomLeft = 1,
			CornerRadiusBottomRight = 1,
			CornerRadiusTopLeft = 1,
			CornerRadiusTopRight = 1
		});
		_timerBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat
		{
			BgColor = new Color("#FFFFFF"),
			CornerRadiusBottomLeft = 1,
			CornerRadiusBottomRight = 1,
			CornerRadiusTopLeft = 1,
			CornerRadiusTopRight = 1
		});
		column.AddChild(_timerBar);
	}

	private static StyleBoxFlat MakeGlowStyle(Color bg, Color border, Color glow)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
			BorderColor = border,
			BorderWidthBottom = 2,
			BorderWidthLeft = 2,
			BorderWidthRight = 2,
			BorderWidthTop = 2,
			CornerRadiusBottomLeft = 10,
			CornerRadiusBottomRight = 10,
			CornerRadiusTopLeft = 10,
			CornerRadiusTopRight = 10,
			ShadowColor = glow,
			ShadowSize = glow.A > 0.01f ? 10 : 0,
			ShadowOffset = new Vector2(0, 0)
		};
	}

	public void ShowEvent(SpecialEventData data, Action<int> onChoice, Action onClosed)
	{
		_data = data;
		_onChoice = onChoice;
		_onClosed = onClosed;
		_resolving = false;
		_chosenIndex = -1;
		_elapsed = 0.0;
		_timerBar.Value = ChoiceTimeout;

		_title.Text = data.Title;
		_desc.Text = data.Description;

		// 洗牌显示顺序:每个问题出现时,J/K/L 对应的选项都重新随机排列
		for (int i = 0; i < 3; i++) _displayMap[i] = i;
		for (int i = 2; i > 0; i--)
		{
			int j = _rng.Next(i + 1);
			(_displayMap[i], _displayMap[j]) = (_displayMap[j], _displayMap[i]);
		}

		for (int i = 0; i < 3; i++)
		{
			string key = i == 0 ? "J" : i == 1 ? "K" : "L";
			int src = _displayMap[i];
			_optionButtons[i].Text = $"{key}  {data.Options[src].Label}";
			_optionButtons[i].MouseFilter = Control.MouseFilterEnum.Stop;
			_optionButtons[i].FocusMode = Control.FocusModeEnum.All;
			_optionButtons[i].Show();
			_optionButtons[i].SelfModulate = Colors.White;

			_optionStyles[i].BgColor = _idleBg;
			_optionStyles[i].BorderColor = _idleBorder;
			_optionStyles[i].ShadowColor = new Color("#000000", 0f);
			_optionStyles[i].ShadowSize = 0;
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
		if (_resolving) return;
		_resolving = true;
		_chosenIndex = index;
		_flashT = 0.0;

		// 全部设为对应颜色(正确绿/错误红),然后只让选中项闪烁
		for (int i = 0; i < 3; i++)
		{
			_optionButtons[i].MouseFilter = Control.MouseFilterEnum.Ignore;
			_optionButtons[i].FocusMode = Control.FocusModeEnum.None;
			int src = _displayMap[i];
			bool correct = _data.Options[src].IsCorrect;
			_optionStyles[i].BgColor = correct ? new Color("#0B3D1F") : new Color("#4A0F1A");
			_optionStyles[i].BorderColor = correct ? _correctBorder : _wrongBorder;
			_optionStyles[i].ShadowColor = correct ? _correctGlow : _wrongGlow;
			_optionStyles[i].ShadowSize = correct ? 12 : 10;

			if (i != index)
				_optionButtons[i].SelfModulate = new Color(0.35f, 0.35f, 0.35f);
		}

		// 回调始终传原始下标,保证 GameManager 取到的选项与玩家所见一致
		_onChoice?.Invoke(_displayMap[index]);
	}

	public override void _Process(double delta)
	{
		if (_resolving)
		{
			_flashT += delta;
			double half = FlashDuration * 0.5;
			float t;
			if (_flashT < half)
				t = Mathf.Clamp((float)(_flashT / half), 0f, 1f);
			else
				t = Mathf.Clamp(1f - (float)((_flashT - half) / half), 0f, 1f);

			// 选中项 SelfModulate 做亮度脉冲:1.0 -> 1.5 -> 1.0,不透明度不变
			float bright = 1f + t * 0.55f;
			var chosen = _optionButtons[_chosenIndex];
			chosen.SelfModulate = new Color(bright, bright, bright);

			if (_flashT >= FlashDuration)
			{
				_onClosed?.Invoke();
				QueueFree();
			}
		}
		else
		{
			_elapsed += delta;
			_timerBar.Value = Mathf.Max(ChoiceTimeout - _elapsed, 0.0);
			if (_elapsed >= ChoiceTimeout)
				Choose(0);
		}
	}
}
