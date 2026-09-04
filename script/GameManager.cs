using Godot;

public partial class GameManager : Node
{
	private TextEdit _text;
	private Character _player;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_text = GetNode<TextEdit>("../HpText");
		_player = GetNode<Character>("../Player");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		float hp = _player.Hp;
		string text = $"HP = {hp}";
		_text.Text = text;
	}
}
