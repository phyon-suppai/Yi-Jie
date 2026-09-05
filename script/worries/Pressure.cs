using Godot;

/// <summary>
/// 焦:过载,拖着任务箱。缓慢但执拗地逼近玩家——速度慢,却不讲道理地一直朝你来。
/// 任务箱小色块由基类 _Draw 里 Pressure 分支画出。
/// </summary>
public partial class Pressure : Worry
{
	public override WorryType Kind => WorryType.Pressure;
	// 移动即基类默认 Seek,由场景 MaxSpeed/Accel 决定其「沉重缓移」手感
}
