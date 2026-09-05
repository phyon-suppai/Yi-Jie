using Godot;

public partial class EndingOverlay : CanvasLayer
{
    public override void _Ready()
    {
        var btn = GetNode<Button>("Center/Panel/Margin/VBox/BackButton");
        btn.Pressed += OnBack;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_accept"))
        {
            OnBack();
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnBack()
    {
        GetTree().ChangeSceneToFile("res://scenes/title_screen.tscn");
    }
}
