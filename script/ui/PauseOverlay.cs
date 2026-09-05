using Godot;

public partial class PauseOverlay : CanvasLayer
{
	public override void _Ready()
	{
		var closeBtn = GetNode<Button>("Center/Panel/Margin/VBox/HeaderRow/CloseButton");
		var quitBtn = GetNode<Button>("Center/Panel/Margin/VBox/QuitButton");

		closeBtn.Pressed += Resume;
		quitBtn.Pressed += QuitGame;
	}

	public override void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel"))
		{
			Resume();
			GetViewport().SetInputAsHandled();
		}
	}

	private void Resume()
	{
		GetTree().Paused = false;
		QueueFree();
	}

	private void QuitGame()
	{
		GetTree().Quit();
	}
}
