using Godot;
using System.Collections.Generic;

/// <summary>
/// 表达(蓝):按住期间持续向前飞出,飞得越远速度越慢;松开则弧线折返回到发射者身边。
/// 往返全程扫掠命中范围内的烦恼(群体),同一烦恼整趟飞行只命中一次。
/// 冷却为 0,但只有当表达完全收回后,才能发射下一个。
/// </summary>
public partial class Express : Area2D, IWeapon
{
	[Signal]
	public delegate void WorryHitEventHandler(Node2D worry);

	public WeaponType Kind => WeaponType.Express;
	public bool IsReleased => _released;

	[ExportGroup("冷却 / 伤害")]
	[Export] public float Cooldown { get; set; } = 0f; // 按住型:无冷却,由 IsReleased 控制再发

	[ExportGroup("弹道")]
	[Export] public float OutSpeed { get; set; } = 900f;      // 初始飞出速度
	[Export] public float Range { get; set; } = 620f;       // 最大射程(按住到顶也会自动返回)
	[Export] public float ReturnSpeed { get; set; } = 1300f; // 折返速度
	[Export] public float ProbeRadius { get; set; } = 26f;   // 扫掠判定半径
	[Export(PropertyHint.Range, "0.900,0.999,0.001")] public float SpeedDecay { get; set; } = 0.97f; // 每秒速度衰减系数
	[Export] public float MinSpeed { get; set; } = 120f;     // 最慢速度下限

	[ExportGroup("外观")]
	[Export] public float Size { get; set; } = 26f; // 菱形边长

	private Node2D _launcher;
	private Vector2 _dir = Vector2.Right;
	private Vector2 _origin;
	private bool _returning;
	private bool _released;
	private bool _holding;
	private float _currentSpeed;
	private float _traveled;
	private Vector2 _lastPos;
	private readonly HashSet<Worry> _hit = new(); // 整趟飞行命中去重

	public void Launch(Node2D owner, Vector2 direction)
	{
		_launcher = owner;
		_dir = direction == Vector2.Zero ? Vector2.Right : direction.Normalized();
		_origin = owner.GlobalPosition;
		_returning = false;
		_released = false;
		_holding = false;
		_currentSpeed = OutSpeed;
		_traveled = 0f;
		_lastPos = _origin;
		_hit.Clear();
		GlobalPosition = _origin;
		Rotation = _dir.Angle();
		QueueRedraw();
	}

	public void Hold(float delta)
	{
		if (_released || _returning) return;
		_holding = true;
	}

	public void Release()
	{
		if (_released) return;
		_holding = false;
		_returning = true;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_launcher == null || !GodotObject.IsInstanceValid(_launcher))
		{
			QueueFree();
			return;
		}

		float d = (float)delta;
		if (_returning)
		{
			// 回程:沿弧线朝发射者当前位置折返
			Vector2 toLauncher = _launcher.GlobalPosition - GlobalPosition;
			float dist = toLauncher.Length();
			float step = ReturnSpeed * d;
			if (dist <= step)
			{
				_released = true;
				SweepHit();
				QueueFree();
				return;
			}
			Vector2 retDir = toLauncher / dist;
			Vector2 blend = (_dir + retDir * 1.6f).Normalized();
			GlobalPosition += blend * step;
		}
		else if (_holding)
		{
			// 去程:按住期间持续向前,速度越来越慢
			float step = _currentSpeed * d;
			_traveled += step;
			GlobalPosition = _origin + _dir * _traveled;

			_currentSpeed = Mathf.Max(_currentSpeed * Mathf.Pow(SpeedDecay, d), MinSpeed);

			if (_traveled >= Range)
			{
				// 到达最大射程,强制返回
				_holding = false;
				_returning = true;
			}
		}
		else
		{
			// 生成后还没按住就松开了(点按),立即返回
			_returning = true;
		}

		SweepHit();
		Rotation = Mathf.LerpAngle(Rotation, (GlobalPosition - _lastPos).Angle(), 0.4f);
		_lastPos = GlobalPosition;
		QueueRedraw();
	}

	/// <summary>每帧扫掠:把行进路径圆内的烦恼全部计入(一次飞行去重)。</summary>
	private void SweepHit()
	{
		Vector2 center = GlobalPosition;
		foreach (Node node in GetTree().GetNodesInGroup("worry"))
		{
			if (node is not Worry worry || !GodotObject.IsInstanceValid(worry) || worry.IsQueuedForDeletion())
				continue;
			if (!_hit.Contains(worry) &&
				worry.GlobalPosition.DistanceTo(center) <= Mathf.Max(worry.Radius, 1f) + ProbeRadius)
			{
				_hit.Add(worry);
				EmitSignal(SignalName.WorryHit, worry);
			}
		}
	}

	public override void _Draw()
	{
		(Color frame, Color core) = Palette.ForWeapon(Kind);
		DrawDiamond(frame, Size);
		DrawDiamond(core, Size * 0.55f);
	}

	private void DrawDiamond(Color c, float half)
	{
		Vector2[] pts =
		{
			new Vector2(half, 0f),
			new Vector2(0f, half),
			new Vector2(-half, 0f),
			new Vector2(0f, -half)
		};
		DrawColoredPolygon(pts, c);
	}
}
