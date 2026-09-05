using Godot;

public partial class DefeatPanel : CanvasLayer
{
    public override void _Ready()
    {
        var btn = GetNode<Button>("Center/Panel/Margin/VBox/RetryButton");
        btn.Pressed += OnDeepBreath;
    }

    private void OnDeepBreath()
    {
        GetTree().ChangeSceneToFile("res://scenes/title_screen.tscn");
    }
}
