using Godot;
using System.Collections.Generic;

/// <summary>
/// 接受(红):以玩家自身为圆心的范围攻击,按住期间脉冲持续向外扩张。
/// 扩张速度随时间越来越慢;松开按键后脉冲停止扩张并逐渐淡出消失。
/// 冷却为 0,但只有当上一轮脉冲完全消散后,才能再次发动。
/// </summary>
public partial class Accept : Area2D, IWeapon
{
	[Signal]
	public delegate void WorryHitEventHandler(Node2D worry);

	public WeaponType Kind => WeaponType.Accept;
	public bool IsReleased => _released && _fade <= 0f;

	[ExportGroup("冷却 / 范围")]
	[Export] public float Cooldown { get; set; } = 0f; // 按住型:无冷却,由 IsReleased 控制再发
	[Export] public float MaxRadius { get; set; } = 420f;     // 最大可能半径
	[Export] public float InitialRadius { get; set; } = 30f;   // 刚按下时的初始半径
	[Export] public float ExpandSpeed { get; set; } = 450f;   // 初始扩张速度(像素/秒)
	[Export(PropertyHint.Range, "0.900,0.999,0.001")] public float SpeedDecay { get; set; } = 0.97f; // 每秒扩张速度衰减
	[Export] public float FadeTime { get; set; } = 0.18f;     // 松开后淡出时间

	private Node2D _launcher;
	private float _radius;
	private float _speed;
	private bool _holding;
	private bool _released;
	private float _fade;
	private readonly HashSet<Worry> _hit = new(); // 同一次脉冲内每个烦恼只命中一次

	public void Launch(Node2D owner, Vector2 _)
	{
		_launcher = owner;
		GlobalPosition = owner.GlobalPosition;
		_radius = InitialRadius;
		_speed = ExpandSpeed;
		_holding = false;
		_released = false;
		_fade = FadeTime;
		_hit.Clear();
		ApplyOverlap();
		QueueRedraw();
	}

	public void Hold(float delta)
	{
		if (_released) return;
		_holding = true;
	}

	public void Release()
	{
		if (_released) return;
		_holding = false;
		_released = true;
	}

	public override void _PhysicsProcess(double delta)
	{
		float d = (float)delta;

		if (_released)
		{
			// 松开后的淡出消散
			_fade -= d;
			if (_fade <= 0f)
			{
				QueueFree();
				return;
			}
			QueueRedraw();
			return;
		}

		if (_launcher != null && GodotObject.IsInstanceValid(_launcher))
			GlobalPosition = _launcher.GlobalPosition; // 脉冲始终跟随玩家中心

		if (_holding)
		{
			// 持续扩张,速度越来越慢
			_speed = Mathf.Max(_speed * Mathf.Pow(SpeedDecay, d), 10f);
			_radius += _speed * d;
			if (_radius >= MaxRadius)
			{
				_radius = MaxRadius;
				// 到达最大范围后自动释放
				Release();
			}
			ApplyOverlap();
		}
		else
		{
			// 点按(没按住):直接进入淡出消散
			Release();
		}

		QueueRedraw();
	}

	/// <summary>对范围内尚未命中过的烦恼各上报一次。</summary>
	private void ApplyOverlap()
	{
		foreach (Node node in GetTree().GetNodesInGroup("worry"))
		{
			if (node is not Worry worry || !GodotObject.IsInstanceValid(worry) || worry.IsQueuedForDeletion())
				continue;
			if (_hit.Contains(worry)) continue;
			if (worry.GlobalPosition.DistanceTo(GlobalPosition) <= Mathf.Max(worry.Radius, 1f) + _radius)
			{
				_hit.Add(worry);
				EmitSignal(SignalName.WorryHit, worry);
			}
		}
	}

	public override void _Draw()
	{
		(Color frame, Color core) = Palette.ForWeapon(Kind);
		float a = _released ? Mathf.Clamp(_fade / FadeTime, 0f, 1f) : 1f;
		frame.A *= a;
		core.A *= a;

		// 半透明填充圆盘
		Color fill = frame;
		fill.A = 0.18f * a;
		DrawCircle(Vector2.Zero, _radius, fill);

		// 外圈粗环
		DrawArc(Vector2.Zero, _radius, 0f, Mathf.Tau, 64, frame, 7f, true);
		// 内圈细环(暗芯色)
		if (_radius > 14f)
			DrawArc(Vector2.Zero, _radius - 12f, 0f, Mathf.Tau, 64, core, 4f, true);
	}
}
