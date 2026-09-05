using Godot;

public partial class TitleScreen : Control
{
    private Button _startButton;

    public override void _Ready()
    {
        _startButton = GetNode<Button>("Root/StartButton");
        _startButton.Pressed += ShowIntro;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_accept"))
        {
            ShowIntro();
            GetViewport().SetInputAsHandled();
        }
    }

    private void ShowIntro()
    {
        _startButton.Pressed -= ShowIntro;

        var intro = GD.Load<PackedScene>("res://scenes/ui/intro_overlay.tscn");
        var overlay = intro.Instantiate<IntroOverlay>();
        overlay.Closed += StartGame;
        AddChild(overlay);
    }

    private void StartGame()
    {
        GetTree().ChangeSceneToFile("res://scenes/play/level_01_doubt.tscn");
    }
}
