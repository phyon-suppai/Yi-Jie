using Godot;

/// <summary>
/// 橡皮·接纳：以玩家为圆心，按住扩张半径、松手收缩归零。
/// <para>
/// 伤害与「接触时间」成正比：圈内的烦恼每帧按 delta 上报接触时长，
/// 持续贴着就持续掉血；扩张得越大、罩住越久，总伤害越高。
/// </para>
/// </summary>
public partial class Accept : Area2D, IWeapon
{
	/// <summary>接触烦恼时发出（参数：被命中的烦恼、本帧接触时长）。由 GameManager 订阅后裁决。</summary>
	[Signal]
	public delegate void WorryHitEventHandler(Node2D worry, float amount);

	[ExportGroup("冷却")]
	[Export] public float Cooldown { get; set; } = 1.2f;

	[ExportGroup("伤害")]
	// 每秒接触造成的伤害（DPS）。实际伤害 = 本系数 × 接触秒数。
	// 参考：60 表示贴满 1 秒造成 60 点伤害，圈内所有烦恼同时结算
	[Export(PropertyHint.Range, "0,300,1")]
	public float Damage { get; set; } = 60f;

	[ExportGroup("范围")]
	[Export] public float StartRadius { get; set; } = 0f;
	[Export] public float ExpandSpeed { get; set; } = 320f;
	// 伤害靠接触时间累积，半径太小玩家根本贴不上，故默认给到 180
	[Export] public float MaxRadius { get; set; } = 180f;
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

	public void Launch(Node2D owner, Vector2 direction)
	{
		_launcher = owner;
		_radius = StartRadius;
		_shrinking = false;

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
		BurnContacts(d);
	}

	/// <summary>圈内的烦恼按本帧接触时长上报伤害量（烦恼中心落在圈内即算接触）。</summary>
	private void BurnContacts(float d)
	{
		if (_radius <= 0f)
			return;

		foreach (Node node in GetTree().GetNodesInGroup("worry"))
		{
			if (node is not Worry worry || !GodotObject.IsInstanceValid(worry) || worry.IsQueuedForDeletion())
				continue;

			if (worry.GlobalPosition.DistanceTo(GlobalPosition) <= _radius)
				EmitSignal(SignalName.WorryHit, worry, d);
		}
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
