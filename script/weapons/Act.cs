using Godot;
using System.Collections.Generic;

/// <summary>
/// 行动(绿):直线高速飞行、射程远、单体命中(碰到第一个烦恼即止)。
/// 命中烦恼只上报信号,裁决由 GameManager 统一负责。
/// </summary>
public partial class Act : Area2D, IWeapon
{
	[Signal]
	public delegate void WorryHitEventHandler(Node2D worry);

	public WeaponType Kind => WeaponType.Act;
	public bool IsReleased => true; // 行动是瞬发,发射后对象自行管理,持 slot 不保留它

	[ExportGroup("冷却 / 伤害")]
	[Export] public float Cooldown { get; set; } = ReactionTable.ActCooldown;

	[ExportGroup("弹道")]
	[Export] public float Speed { get; set; } = 950f;
	[Export] public float Range { get; set; } = 620f;
	[Export] public float ProbeRadius { get; set; } = 20f; // 命中判定的探测半径

	[ExportGroup("外观")]
	[Export] public float BodyLength { get; set; } = 42f;
	[Export] public float BodyWidth { get; set; } = 10f;

	private Vector2 _dir = Vector2.Right;
	private float _travelled;
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
		GlobalPosition = owner.GlobalPosition;
		Rotation = _dir.Angle();
		QueueRedraw();
	}

	public void Hold(float delta) { }
	public void Release() { }

	public override void _PhysicsProcess(double delta)
	{
		float step = Speed * (float)delta;
		GlobalPosition += _dir * step;
		_travelled += step;
		if (_travelled >= Range)
			QueueFree(); // 射程尽头消散
	}

	private void OnAreaEntered(Area2D area)
	{
		if (!area.IsInGroup("worry") || !_hit.Add(area))
			return;
		EmitSignal(SignalName.WorryHit, area);
		QueueFree(); // 单体:命中一个即止
	}

	public override void _Draw()
	{
		(Color frame, Color core) = Palette.ForWeapon(Kind);
		// 亮框细长条 + 暗芯 → 「行动」
		DrawRect(new Rect2(-BodyLength / 2f, -BodyWidth / 2f, BodyLength, BodyWidth), frame);
		float inset = BodyWidth * 0.3f;
		DrawRect(new Rect2(-BodyLength / 2f + 2f, -BodyWidth / 2f + inset, BodyLength - 4f, BodyWidth - inset * 2f), core);
	}
}
