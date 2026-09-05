using Godot;

/// <summary>
/// 孤:失落,半透明幽灵体。不再游离后退,而是像被忽视的存在感一样主动贴近玩家。
/// </summary>
public partial class Loneliness : Worry
{
	public override WorryType Kind => WorryType.Loneliness;

	protected override void Move(float d)
	{
		Node2D target = PlayerNode();
		if (target == null) return;

		Vector2 to = target.GlobalPosition - GlobalPosition;
		// 主动贴近玩家:幽灵般不紧不慢,但持续逼近
		Seek(to, MaxSpeed * 0.9f, Accel * 0.9f, d);
	}
}
