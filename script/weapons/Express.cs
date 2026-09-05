using Godot;

/// <summary>
/// 纸·表达：掷出后沿发射方向惯性直线飞行（按“原来的路线”走，不受玩家移动牵连），
/// 不画连接线；松开按键自动直线飞回角色身边。
/// <para>
/// 伤害与「划过烦恼的线段长度」成正比：每帧用移动线段与烦恼的圆求交，
/// 按重叠弦长上报伤害量。去程与回程各自结算，悬停不动时不造成伤害。
/// </para>
/// </summary>
public partial class Express : Area2D, IWeapon
{
	/// <summary>划过烦恼时发出（参数：被命中的烦恼、本帧划过的弦长）。由 GameManager 订阅后裁决。</summary>
	[Signal]
	public delegate void WorryHitEventHandler(Node2D worry, float amount);

	[ExportGroup("冷却")]
	[Export] public float Cooldown { get; set; } = 0.6f;

	[ExportGroup("伤害")]
	// 每划过 1 世界单位长度造成的伤害。实际伤害 = 本系数 × 划过长度。
	// 参考：划过半径 80 的烦恼（直径约 160）单程 ≈ 26 点；大烦恼划过的弦更长，伤害更高。
	[Export(PropertyHint.Range, "0,1,0.005")]
	public float Damage { get; set; } = 0.16f;

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

	public void Launch(Node2D owner, Vector2 direction)
	{
		_launcher = owner;
		_dir = direction == Vector2.Zero ? Vector2.Right : direction.Normalized();
		_origin = owner.GlobalPosition;
		_travel = 0f;
		_returning = false;
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
		Vector2 prev = GlobalPosition; // 本帧起点，用于算划过的线段

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
				GlobalPosition = _launcher.GlobalPosition;
				SweepHits(prev); // 收尾这最后一段也要结算
				QueueFree();
				return;
			}
			GlobalPosition += toLauncher / dist * step;
		}

		SweepHits(prev);
		QueueRedraw();
	}

	/// <summary>用本帧的移动线段与每个烦恼求交，按划过的弦长上报伤害量。</summary>
	private void SweepHits(Vector2 from)
	{
		Vector2 move = GlobalPosition - from;
		float len = move.Length();
		if (len <= 0.0001f)
			return; // 悬停不动＝没有划过，不造成伤害

		Vector2 dir = move / len;

		foreach (Node node in GetTree().GetNodesInGroup("worry"))
		{
			if (node is not Worry worry || !GodotObject.IsInstanceValid(worry) || worry.IsQueuedForDeletion())
				continue;

			float overlap = SegmentCircleOverlap(from, dir, len, worry.GlobalPosition, Mathf.Max(worry.Radius, 1f));
			if (overlap > 0f)
				EmitSignal(SignalName.WorryHit, worry, overlap);
		}
	}

	/// <summary>线段 [from, from + dir × len] 与圆 (center, radius) 的重叠长度（弦长）。</summary>
	private static float SegmentCircleOverlap(Vector2 from, Vector2 dir, float len, Vector2 center, float radius)
	{
		// 圆心在线段所在直线上的投影参数（夹到线段内）
		float t = Mathf.Clamp((center - from).Dot(dir), 0f, len);
		Vector2 closest = from + dir * t;
		float dist = (center - closest).Length();

		if (dist >= radius)
			return 0f;

		// 弦长的一半：由最近点向两侧展开，再与 [0, len] 取交集
		float half = Mathf.Sqrt(radius * radius - dist * dist);
		float lo = Mathf.Max(t - half, 0f);
		float hi = Mathf.Min(t + half, len);
		return Mathf.Max(hi - lo, 0f);
	}

	public override void _Draw()
	{
		// 占位：只画纸片本身（不画连接线），正式贴图绑定后可去掉
		DrawRect(new Rect2(new Vector2(-PaperLength * 0.5f, -PaperWidth * 0.5f),
			new Vector2(PaperLength, PaperWidth)), PaperColor);
	}
}
