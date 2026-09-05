using Godot;

/// <summary>
/// 特殊事件方块:急速冲向玩家,接触后弹出剧情三选一。
/// 不被武器命中,不计入普通烦恼列表。
/// </summary>
public partial class SpecialEventBlock : Area2D
{
	[Export] public string EventId { get; set; }
	[Export] public float MaxSpeed { get; set; } = 420f;
	[Export] public float Accel { get; set; } = 1100f;
	[Export] public float TriggerDistance { get; set; } = 55f;

	private GameManager _gm;
	private Node2D _player;
	private Vector2 _velocity;
	private bool _triggered;

	public override void _Ready()
	{
		_gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
		_player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		AddToGroup("special_event_block");
		ZIndex = 50;
		QueueRedraw();
	}

	public override void _PhysicsProcess(double delta)
	{
		float d = (float)delta;
		if (_triggered || _player == null || !GodotObject.IsInstanceValid(_player)) return;

		Vector2 to = _player.GlobalPosition - GlobalPosition;
		float dist = to.Length();
		if (dist <= TriggerDistance)
		{
			_triggered = true;
			_gm?.OpenEvent(EventId);
			QueueFree();
			return;
		}

		Vector2 dir = dist > 0.001f ? to / dist : Vector2.Zero;
		_velocity = _velocity.MoveToward(dir * MaxSpeed, Accel * d);
		GlobalPosition += _velocity * d;
		RotationDegrees += 140f * d;
		QueueRedraw();
	}

	public override void _Draw()
	{
		float half = 30f;
		Color frame = new Color("#FFFFFF");
		Color core = new Color("#C58CFF");
		Color border = new Color("#FFE05C");

		DrawRect(new Rect2(-half, -half, half * 2f, half * 2f), frame);
		DrawRect(new Rect2(-half, -half, half * 2f, half * 2f), border, false, 5f);
		float inner = half * 0.55f;
		DrawRect(new Rect2(-inner, -inner, inner * 2f, inner * 2f), core);

		if (Engine.IsEditorHint()) return;
		Font font = new SystemFont
		{
			FontNames = new[] { "Noto Sans CJK SC", "Microsoft YaHei", "SimHei" }
		};
		Vector2 ts = font.GetStringSize("?", fontSize: 32);
		DrawString(font, new Vector2(-ts.X * 0.5f, ts.Y * 0.35f), "?",
			HorizontalAlignment.Left, -1, 32, new Color("#1A0F2E"));
	}
}
