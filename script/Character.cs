using Godot;

public partial class Character : CharacterBody2D
{
	[Export]
	public float HorizontalSpeed;
	
	[Export]
	public float VerticalSpeed;

	private AnimatedSprite2D _player;

	public override void _Ready()
	{
		_player = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector2 velocity = Velocity;

		if (Input.IsActionPressed("left"))
		{
			velocity.X = -HorizontalSpeed;
			_player.FlipH = true;
		}
		else if (Input.IsActionPressed("right"))
		{
			velocity.X = HorizontalSpeed;
			_player.FlipH = false;
		}
		else
		{
			velocity.X = 0;
		}

		if (Input.IsActionPressed("up"))
		{
			velocity.Y = -VerticalSpeed;
		}
		else if (Input.IsActionPressed("down"))
		{
			velocity.Y = VerticalSpeed;
		}
		else
		{
			velocity.Y = 0;
		}

		if (velocity.X == 0 && velocity.Y == 0)
		{
			_player.Play("idle");
		}
		else
		{
			_player.Play("run");
		}

		Velocity = velocity;
		MoveAndSlide();
	}
}
