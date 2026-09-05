using Godot;

public partial class IntroOverlay : CanvasLayer
{
    [Signal]
    public delegate void ClosedEventHandler();

    public override void _Ready()
    {
        var btn = GetNode<Button>("Center/Panel/Margin/VBox/StartButton");
        btn.Pressed += OnStart;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_accept"))
        {
            OnStart();
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnStart()
    {
        EmitSignal(SignalName.Closed);
        QueueFree();
    }
}
