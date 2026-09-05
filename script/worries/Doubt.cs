using Godot;

/// <summary>
/// 疑:自我内耗,但也会主动逼近玩家。速度较慢,却不会让玩家轻易忽视。
/// </summary>
public partial class Doubt : Worry
{
	public override WorryType Kind => WorryType.Doubt;

	protected override void Move(float d)
	{
		Node2D target = PlayerNode();
		if (target == null) return;

		Vector2 to = target.GlobalPosition - GlobalPosition;
		// 主动朝玩家逼近:速度略慢,表达迟疑但黏人
		Seek(to, MaxSpeed * 0.65f, Accel * 0.8f, d);
	}
}
