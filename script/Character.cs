using System;
using Godot;

public partial class Character : CharacterBody2D
{
	[Export] public float HorizontalSpeed;
	[Export] public float VerticalSpeed;
	[Export] public float BleedRate;

	[Export] public float Hp;

	private Timer _timer;

	private AnimatedSprite2D _player;
	private Heart _heart;

	public override void _Ready()
	{
		_player = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_timer = new Timer();
		_timer.WaitTime = 1.0;
		_timer.Timeout += Bleed;
		AddChild(_timer);
		_timer.Start();
		_heart = GetNode<Heart>("Heart");
		_heart.Total = Hp;
		_heart.Current = Hp;
	}

	private void Bleed()
	{
		Hp -= BleedRate;
		_heart.Current = Hp;
		if (Hp <= 0)
		{
			throw new NotImplementedException();
		}
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
