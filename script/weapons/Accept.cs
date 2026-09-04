using Godot;
using System.Collections.Generic;

/// <summary>
/// 橡皮·接纳：以玩家为圆心，按住扩张半径、松手收缩归零；
/// 圈内命中的所有烦恼都被上报（群体范围）。无朝向概念。
/// <para>独立类，不继承公共武器基类。命中烦恼只上报信号，裁决由 GameManager 统一负责。</para>
/// </summary>
public partial class Accept : Area2D, IWeapon
{
	/// <summary>命中一个烦恼时发出（参数为被命中的烦恼节点）。由 GameManager 订阅后裁决。</summary>
	[Signal]
	public delegate void WorryHitEventHandler(Node2D worry);

	[ExportGroup("冷却")]
	[Export] public float Cooldown { get; set; } = 1.2f;

	[ExportGroup("范围")]
	[Export] public float StartRadius { get; set; } = 0f;
	[Export] public float ExpandSpeed { get; set; } = 320f;
	[Export] public float MaxRadius { get; set; } = 130f;
	[Export] public float ShrinkSpeed { get; set; } = 420f;

	[ExportGroup("外观（占位，可在编辑器里另挂 Sprite2D 绑定真贴图）")]
	[Export] public Color RingColor { get; set; } = new Color(1f, 0.75f, 0.85f, 0.8f);
	[Export] public float RingWidth { get; set; } = 4f;
	[Export] public Color FillColor { get; set; } = new Color(1f, 0.75f, 0.85f, 0.15f);

	/// <summary>true=玩家按住 action 3（扩张）；false=松手（收缩）。由持有者每帧写入。</summary>
	public bool IsHeld { get; set; }

	private Node2D _launcher;
	private CircleShape2D _shape; // 每次发射新建一个实例，避免实例间共享形状
	private float _radius;
	private bool _shrinking;
	private readonly HashSet<Node> _hit = new();

	public override void _Ready()
	{
		AreaEntered += OnAreaEntered;
	}

	public void Launch(Node2D owner, Vector2 direction)
	{
		_launcher = owner;
		_radius = StartRadius;
		_shrinking = false;
		_hit.Clear();

		// 用独立的碰撞圆，半径随 IsHeld 实时变化
		var cs = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (cs != null)
		{
			_shape?.Dispose();
			_shape = new CircleShape2D();
			cs.Shape = _shape;
		}

		SyncPosition();
		ApplyRadius();
	}

	public override void _PhysicsProcess(double delta)
	{
		float d = (float)delta;
		if (!_shrinking)
		{
			if (IsHeld)
				_radius = Mathf.Min(_radius + ExpandSpeed * d, MaxRadius); // 按住：扩张，封顶待命
			else
				_shrinking = true; // 松手：收缩归零
		}
		else
		{
			_radius -= ShrinkSpeed * d;
			if (_radius <= 0f)
			{
				QueueFree(); // 缩到没有即结束
				return;
			}
		}

		SyncPosition();
		ApplyRadius();
	}

	private void SyncPosition()
	{
		if (_launcher == null || !GodotObject.IsInstanceValid(_launcher))
		{
			QueueFree();
			return;
		}
		GlobalPosition = _launcher.GlobalPosition;
	}

	private void ApplyRadius()
	{
		if (_shape != null)
			_shape.Radius = Mathf.Max(_radius, 1f); // 保持最小半径，碰撞检测才有效
		QueueRedraw();
	}

	private void OnAreaEntered(Area2D area)
	{
		if (!area.IsInGroup("worry"))
			return;
		if (!_hit.Add(area))
			return;
		EmitSignal(SignalName.WorryHit, area); // 群体：范围持续期内继续上报新进入的烦恼
	}

	public override void _Draw()
	{
		// 占位：一圈扩大的光环，正式贴图绑定后可去掉
		if (_radius > 0f)
		{
			DrawArc(Vector2.Zero, _radius, 0f, Mathf.Tau, 48, RingColor, RingWidth);
			DrawCircle(Vector2.Zero, _radius, FillColor);
		}
	}

	public override void _ExitTree()
	{
		_shape?.Dispose();
		_shape = null;
	}
}
