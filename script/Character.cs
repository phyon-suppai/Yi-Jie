using Godot;

public partial class Character : CharacterBody2D
{
	[Export]
	public float HorizontalSpeed;
	
	[Export]
	public float VerticalSpeed;

	public override void _PhysicsProcess(double delta)
	{
		Vector2 velocity = Velocity;

		if (Input.IsActionPressed("left"))
		{
			velocity.X = -HorizontalSpeed;
		}
		else if (Input.IsActionPressed("right"))
		{
			velocity.X = HorizontalSpeed;
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

		Velocity = velocity;
		MoveAndSlide();
	}
}
