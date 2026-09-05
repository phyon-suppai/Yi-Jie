/// <summary>特殊事件方块弹出的剧情选项数据。</summary>
public class SpecialEventData
{
	public string Id { get; set; }
	public string Title { get; set; }
	public string Description { get; set; }
	public EventOption[] Options { get; set; }

	public SpecialEventData(string id, string title, string description, EventOption[] options)
	{
		Id = id;
		Title = title;
		Description = description;
		Options = options;
	}
}

public class EventOption
{
	/// <summary>选项文案(不含 A/B/C 前缀)。</summary>
	public string Label { get; set; }
	/// <summary>精力变化:正确为 +20,错误为 -30/-45。</summary>
	public int EnergyDelta { get; set; }
	/// <summary>错误选项在玩家周围刷新的焦虑方块数量。</summary>
	public int SpawnPressureCount { get; set; }
	/// <summary>选择后的内心独白。</summary>
	public string InnerMonologue { get; set; }
	/// <summary>是否为正确选项。</summary>
	public bool IsCorrect { get; set; }

	public EventOption(string label, int energyDelta, int spawnPressureCount, string innerMonologue, bool isCorrect)
	{
		Label = label;
		EnergyDelta = energyDelta;
		SpawnPressureCount = spawnPressureCount;
		InnerMonologue = innerMonologue;
		IsCorrect = isCorrect;
	}
}
