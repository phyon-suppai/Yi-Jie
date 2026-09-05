using Godot;
using System;

/// <summary>
/// 与心魔/神秘方块对话的二选一弹窗(受 dialogue_box.gd 启发,纯 C# 代码自绘)。
/// A=回避(默认/超时), B=面对。弹出后 300ms 输入防抖,避免移动键秒选。
/// </summary>
public partial class DialogueBox : CanvasLayer
{
	[Signal]
	public delegate void ChoiceMadeEventHandler(string line, bool choseB);

	public const float ChoiceTimeout = 3.0f;
	public const int InputGraceMs = 300;

	private Label _lineLabel;
	private Button _optionAButton;
	private Button _optionBButton;
	private ProgressBar _timerBar;

	private KnotData _data;
	private double _elapsed;
	private bool _active;
	private ulong _inputLockedUntil;

	public bool IsActive => _active;

	public override void _Ready()
	{
		Layer = 100;
		Visible = false;
		SetProcess(false);

		BuildUi();

		_optionAButton.Pressed += () => { if (_active) Resolve(false); };
		_optionBButton.Pressed += () => { if (_active) Resolve(true); };
	}

	private void BuildUi()
	{
		var panel = new Panel();
		panel.SetAnchorsPreset(Control.LayoutPreset.Center);
		panel.Size = new Vector2(720, 320);
		panel.Position = new Vector2(-360, -160);
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color("#1A0F2E"),
			BorderColor = new Color("#C58CFF"),
			BorderWidthBottom = 4,
			BorderWidthLeft = 4,
			BorderWidthRight = 4,
			BorderWidthTop = 4,
			CornerRadiusBottomLeft = 12,
			CornerRadiusBottomRight = 12,
			CornerRadiusTopLeft = 12,
			CornerRadiusTopRight = 12
		});
		AddChild(panel);

		var vbox = new VBoxContainer();
		vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		vbox.OffsetLeft = 24;
		vbox.OffsetTop = 24;
		vbox.OffsetRight = -24;
		vbox.OffsetBottom = -24;
		vbox.AddThemeConstantOverride("separation", 14);
		panel.AddChild(vbox);

		Font font = new SystemFont
		{
			FontNames = new[] { "Noto Sans CJK SC", "Microsoft YaHei", "SimHei", "PingFang SC", "Source Han Sans SC" }
		};

		_lineLabel = new Label();
		_lineLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_lineLabel.VerticalAlignment = VerticalAlignment.Center;
		_lineLabel.AutowrapMode = TextServer.AutowrapMode.Word;
		_lineLabel.AddThemeFontOverride("font", font);
		_lineLabel.AddThemeFontSizeOverride("font_size", 22);
		_lineLabel.AddThemeColorOverride("font_color", new Color("#F1E9FF"));
		vbox.AddChild(_lineLabel);

		var hbox = new HBoxContainer();
		hbox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		hbox.Alignment = BoxContainer.AlignmentMode.Center;
		hbox.AddThemeConstantOverride("separation", 24);
		vbox.AddChild(hbox);

		_optionAButton = new Button();
		_optionAButton.Size = new Vector2(220, 70);
		_optionAButton.AddThemeFontOverride("font", font);
		_optionAButton.AddThemeFontSizeOverride("font_size", 20);
		_optionAButton.AddThemeColorOverride("font_color", new Color("#FFFFFF"));
		_optionAButton.AddThemeColorOverride("font_hover_color", new Color("#FFE05C"));
		_optionAButton.AddThemeStyleboxOverride("normal", MakeButtonStyle(new Color("#2C1257")));
		_optionAButton.AddThemeStyleboxOverride("hover", MakeButtonStyle(new Color("#4A2080")));
		_optionAButton.AddThemeStyleboxOverride("pressed", MakeButtonStyle(new Color("#6B32B8")));
		hbox.AddChild(_optionAButton);

		_optionBButton = new Button();
		_optionBButton.Size = new Vector2(220, 70);
		_optionBButton.AddThemeFontOverride("font", font);
		_optionBButton.AddThemeFontSizeOverride("font_size", 20);
		_optionBButton.AddThemeColorOverride("font_color", new Color("#FFFFFF"));
		_optionBButton.AddThemeColorOverride("font_hover_color", new Color("#FFE05C"));
		_optionBButton.AddThemeStyleboxOverride("normal", MakeButtonStyle(new Color("#2C1257")));
		_optionBButton.AddThemeStyleboxOverride("hover", MakeButtonStyle(new Color("#4A2080")));
		_optionBButton.AddThemeStyleboxOverride("pressed", MakeButtonStyle(new Color("#6B32B8")));
		hbox.AddChild(_optionBButton);

		_timerBar = new ProgressBar();
		_timerBar.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		_timerBar.CustomMinimumSize = new Vector2(400, 12);
		_timerBar.MaxValue = ChoiceTimeout;
		_timerBar.Value = ChoiceTimeout;
		_timerBar.ShowPercentage = false;
		var barStyle = new StyleBoxFlat
		{
			BgColor = new Color("#2C1257"),
			BorderWidthBottom = 0,
			BorderWidthLeft = 0,
			BorderWidthRight = 0,
			BorderWidthTop = 0,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6
		};
		var fillStyle = new StyleBoxFlat
		{
			BgColor = new Color("#FFE05C"),
			BorderWidthBottom = 0,
			BorderWidthLeft = 0,
			BorderWidthRight = 0,
			BorderWidthTop = 0,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6
		};
		_timerBar.AddThemeStyleboxOverride("background", barStyle);
		_timerBar.AddThemeStyleboxOverride("fill", fillStyle);
		vbox.AddChild(_timerBar);
	}

	private static StyleBoxFlat MakeButtonStyle(Color bg)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
			BorderColor = new Color("#7A5FD0"),
			BorderWidthBottom = 3,
			BorderWidthLeft = 3,
			BorderWidthRight = 3,
			BorderWidthTop = 3,
			CornerRadiusBottomLeft = 10,
			CornerRadiusBottomRight = 10,
			CornerRadiusTopLeft = 10,
			CornerRadiusTopRight = 10
		};
	}

	public void Open(KnotData data)
	{
		if (_active || data == null) return;
		_data = data;
		_lineLabel.Text = data.Line;
		_optionAButton.Text = "A  " + data.OptionA;
		_optionBButton.Text = "B  " + data.OptionB;
		_elapsed = 0.0;
		_timerBar.Value = ChoiceTimeout;
		_active = true;
		Visible = true;
		SetProcess(true);
		_inputLockedUntil = Time.GetTicksMsec() + (ulong)InputGraceMs;
	}

	public override void _Process(double delta)
	{
		if (!_active) return;
		_elapsed += delta;
		_timerBar.Value = Mathf.Max(ChoiceTimeout - _elapsed, 0.0);
		if (_elapsed >= ChoiceTimeout)
			Resolve(false);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!_active) return;

		bool pressedA = @event.IsActionPressed("choice_a");
		bool pressedB = @event.IsActionPressed("choice_b");
		if (!pressedA && !pressedB) return;

		GetViewport().SetInputAsHandled();
		if (Time.GetTicksMsec() < _inputLockedUntil) return;

		Resolve(pressedB);
	}

	/// <summary>被更高优先级事件打断时静默关闭,不发出 ChoiceMade 信号。</summary>
	public void Close()
	{
		if (!_active) return;
		_active = false;
		Visible = false;
		SetProcess(false);
		_data = null;
	}

	private void Resolve(bool choseB)
	{
		if (!_active) return;
		_active = false;
		Visible = false;
		SetProcess(false);
		EmitSignal("ChoiceMade", _data.Line, choseB);
		_data = null;
	}
}

/// <summary>对话节点数据:一行题干 + A/B 两个选项。</summary>
public class KnotData
{
	public string Line { get; set; }
	public string OptionA { get; set; }
	public string OptionB { get; set; }

	public KnotData(string line, string optionA, string optionB)
	{
		Line = line;
		OptionA = optionA;
		OptionB = optionB;
	}
}
