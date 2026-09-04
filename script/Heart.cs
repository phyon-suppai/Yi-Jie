using Godot;

public partial class Heart : Node2D
{
	public float Total;
	public float Current;

	private AnimatedSprite2D _sprite;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_sprite.Play("default");
		_sprite.Stop();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		float percentage = Current / Total * 100;
		_sprite.Frame = percentage switch
		{
			>= 80 => 0,
			>= 60 => 1,
			>= 40 => 2,
			>= 20 => 3,
			>= 00 => 4,
			_ => _sprite.Frame
		};
	}
}
