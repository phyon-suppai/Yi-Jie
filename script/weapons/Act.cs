using Godot;
using System.Collections.Generic;

/// <summary>
/// 笔·行动：直线高速飞行、射程远、单体命中（命中第一个烦恼即消散）。
/// <para>独立类，不继承公共武器基类。命中烦恼只上报信号，裁决由 GameManager 统一负责。</para>
/// </summary>
public partial class Act : Area2D, IWeapon
{
	/// <summary>命中一个烦恼时发出（参数：被命中的烦恼、伤害量）。由 GameManager 订阅后裁决。</summary>
	[Signal]
	public delegate void WorryHitEventHandler(Node2D worry, float amount);

	[ExportGroup("冷却")]
	[Export] public float Cooldown { get; set; } = 0.4f;

	[ExportGroup("伤害")]
	// 伤害恒定：每次命中上报 amount = 1，实际伤害就是这个值本身。
	// 单体高伤、射程远：冷却仅 0.4 秒，理论 60 点/秒
	[Export(PropertyHint.Range, "0,200,0.5")]
	public float Damage { get; set; } = 24f;

	[ExportGroup("弹道")]
	[Export] public float Speed { get; set; } = 900f;
	[Export] public float Range { get; set; } = 560f;

	[ExportGroup("外观（占位，可在编辑器里另挂 Sprite2D 绑定真贴图）")]
	[Export] public float TrailLength { get; set; } = 22f;
	[Export] public float TrailWidth { get; set; } = 3f;
	[Export] public Color TrailColor { get; set; } = new Color(1f, 0.93f, 0.6f);
	[Export] public float TipRadius { get; set; } = 4f;
	[Export] public Color TipColor { get; set; } = Colors.White;

	/// <summary>笔按下即发射，不使用按住状态，保留接口实现。</summary>
	public bool IsHeld { get; set; }

	private Vector2 _dir = Vector2.Right;
	private float _travelled;
	// 本弹命中过的目标去重（命中即销毁，正常最多一次；防御同帧重复回调）
	private readonly HashSet<Node> _hit = new();

	public override void _Ready()
	{
		AreaEntered += OnAreaEntered;
	}

	public void Launch(Node2D owner, Vector2 direction)
	{
		_dir = direction == Vector2.Zero ? Vector2.Right : direction.Normalized();
		_travelled = 0f;
		_hit.Clear();
		Rotation = _dir.Angle();
		QueueRedraw();
	}

	public override void _PhysicsProcess(double delta)
	{
		float step = Speed * (float)delta;
		GlobalPosition += _dir * step;
		_travelled += step;
		if (_travelled >= Range)
			QueueFree(); // 未命中任何烦恼，飞到射程尽头消散
	}

	private void OnAreaEntered(Area2D area)
	{
		if (!area.IsInGroup("worry"))
			return;
		if (!_hit.Add(area))
			return;
		EmitSignal(SignalName.WorryHit, area, 1f); // 固定量 1：伤害恒定
		QueueFree(); // 单体：命中一个即止
	}

	public override void _Draw()
	{
		// 占位画一根「笔尖」，正式贴图绑定后可去掉
		DrawLine(Vector2.Zero, new Vector2(TrailLength, 0), TrailColor, TrailWidth);
		DrawCircle(new Vector2(TrailLength, 0), TipRadius, TipColor);
	}
}
