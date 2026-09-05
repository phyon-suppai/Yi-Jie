using Godot;

/// <summary>
/// 身份色表:每个对象 = 同一色相的「亮框 Frame + 暗芯 Core」两档。
/// 改视觉只改这一处。武器色与其能消散的烦恼同色 → 「同色即克制」。
/// </summary>
public static class Palette
{
	public static readonly Color PlayerFrame = Color.FromHtml("#FFE05C");
	public static readonly Color PlayerCore = Color.FromHtml("#7A5A00");

	// 疑(绿) = 行动(绿)
	public static readonly Color DoubtFrame = Color.FromHtml("#7BFF9E");
	public static readonly Color DoubtCore = Color.FromHtml("#104D26");

	// 焦(红) = 接受(红)
	public static readonly Color PressureFrame = Color.FromHtml("#FF7A6B");
	public static readonly Color PressureCore = Color.FromHtml("#571711");

	// 孤(蓝) = 表达(蓝)
	public static readonly Color LonelinessFrame = Color.FromHtml("#6FC9FF");
	public static readonly Color LonelinessCore = Color.FromHtml("#12375E");

	// 紫系:门 / 成就条 / 生成器 / 边界
	public static readonly Color PurpleFrame = Color.FromHtml("#C58CFF");
	public static readonly Color PurpleCore = Color.FromHtml("#3C1F6E");

	// 环境
	public static readonly Color Background = Color.FromHtml("#140826"); // 画布清屏深紫
	public static readonly Color Ground = Color.FromHtml("#2C1257");     // 活动地面中紫
	public static readonly Color HudSlot = Color.FromHtml("#0F0620");    // HUD 槽底色

	// 功能反馈色
	public static readonly Color DissolveFlash = Color.FromHtml("#63F58F"); // 同色命中消散

	// 文字
	public static readonly Color TextMain = Color.FromHtml("#F1E9FF");
	public static readonly Color TextDim = Color.FromHtml("#B7A5D9");

	public static readonly Color TextPlayer = PlayerFrame;
	public static readonly Color TextWarn = Color.FromHtml("#FFB454"); // 倦怠/僵直提示

	// ---------------------------------------------------------------- 取色查询
	public static (Color Frame, Color Core) ForWorry(WorryType type)
	{
		return type switch
		{
			WorryType.Doubt => (DoubtFrame, DoubtCore),
			WorryType.Pressure => (PressureFrame, PressureCore),
			WorryType.Loneliness => (LonelinessFrame, LonelinessCore),
			_ => (PurpleFrame, PurpleCore)
		};
	}

	public static (Color Frame, Color Core) ForWeapon(WeaponType type)
	{
		return type switch
		{
			WeaponType.Act => (DoubtFrame, DoubtCore),       // 绿
			WeaponType.Accept => (PressureFrame, PressureCore), // 红
			WeaponType.Express => (LonelinessFrame, LonelinessCore), // 蓝
			_ => (PurpleFrame, PurpleCore)
		};
	}

	public static (Color Frame, Color Core) Player => (PlayerFrame, PlayerCore);
	public static (Color Frame, Color Core) Purple => (PurpleFrame, PurpleCore);
}
