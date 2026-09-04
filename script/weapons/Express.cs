using Godot;
using System.Collections.Generic;

/// <summary>
/// 纸·表达：掷出后沿发射方向惯性直线飞行（按“原来的路线”走，不受玩家移动牵连），
/// 不画连接线；松开按键自动直线飞回角色身边。路径上命中的所有烦恼都被上报（群体）。
/// <para>独立类，不继承公共武器基类。命中烦恼只上报信号，裁决由 GameManager 统一负责。</para>
/// </summary>
public partial class Express : Area2D, IWeapon
{
	/// <summary>命中一个烦恼时发出（参数为被命中的烦恼节点）。由 GameManager 订阅后裁决。</summary>
	[Signal]
	public delegate void WorryHitEventHandler(Node2D worry);

	[ExportGroup("冷却")]
	[Export] public float Cooldown { get; set; } = 0.6f;

	[ExportGroup("弹道")]
	/// <summary>掷出后的飞行速度（惯性，脱手后玩家移动不影响它的路线）</summary>
	[Export] public float ThrowSpeed { get; set; } = 420f;

	/// <summary>按住时可到达的最远距离，到点后悬停待命，等松手返航</summary>
	[Export] public float MaxDistance { get; set; } = 320f;

	/// <summary>松手后飞回角色的速度</summary>
	[Export] public float ReturnSpeed { get; set; } = 600f;

	[ExportGroup("外观（占位，可在编辑器里另挂 Sprite2D 绑定真贴图）")]
	[Export] public float PaperLength { get; set; } = 30f;
	[Export] public float PaperWidth { get; set; } = 16f;
	[Export] public Color PaperColor { get; set; } = new Color(0.92f, 0.97f, 1f);

	/// <summary>true=玩家按住 action 2（持续外飞）；false=松手（自动返回）。由持有者每帧写入。</summary>
	public bool IsHeld { get; set; }

	private Node2D _launcher;
	private Vector2 _dir = Vector2.Right;
	private Vector2 _origin;   // 掷出瞬间的起点，决定去程直线与射程上限
	private float _travel;     // 已飞行的去程距离
	private bool _returning;
	// 去程与回程都会扫过同一个烦恼，去重只上报一次
	private readonly HashSet<Node> _hit = new();

	public override void _Ready()
	{
		AreaEntered += OnAreaEntered;
	}

	public void Launch(Node2D owner, Vector2 direction)
	{
		_launcher = owner;
		_dir = direction == Vector2.Zero ? Vector2.Right : direction.Normalized();
		_origin = owner.GlobalPosition;
		_travel = 0f;
		_returning = false;
		_hit.Clear();
		GlobalPosition = _origin;
		QueueRedraw();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_launcher == null || !GodotObject.IsInstanceValid(_launcher))
		{
			QueueFree();
			return;
		}

		float d = (float)delta;

		if (!_returning)
		{
			if (IsHeld)
			{
				// 惯性去程：沿发射方向继续直线走，到射程尽头悬停待命
				_travel = Mathf.Min(_travel + ThrowSpeed * d, MaxDistance);
				GlobalPosition = _origin + _dir * _travel;
			}
			else
			{
				_returning = true; // 松手：自动回到角色
			}
		}
		else
		{
			// 返航：直接飞向角色当前位置
			Vector2 toLauncher = _launcher.GlobalPosition - GlobalPosition;
			float dist = toLauncher.Length();
			float step = ReturnSpeed * d;
			if (dist <= step)
			{
				QueueFree(); // 收回到身边
				return;
			}
			GlobalPosition += toLauncher / dist * step;
		}

		QueueRedraw();
	}

	private void OnAreaEntered(Area2D area)
	{
		if (!area.IsInGroup("worry"))
			return;
		if (!_hit.Add(area))
			return;
		EmitSignal(SignalName.WorryHit, area); // 群体：不销毁自身，路径上继续上报
	}

	public override void _Draw()
	{
		// 占位：只画纸片本身（不画连接线），正式贴图绑定后可去掉
		DrawRect(new Rect2(new Vector2(-PaperLength * 0.5f, -PaperWidth * 0.5f),
			new Vector2(PaperLength, PaperWidth)), PaperColor);
	}
}
