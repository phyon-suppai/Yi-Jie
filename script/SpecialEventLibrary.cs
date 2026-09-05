using System.Collections.Generic;

/// <summary>特殊事件库:第一阶段学生、第二阶段职场、第三阶段老年,共 9 个特殊方块。</summary>
public static class SpecialEventLibrary
{
	public static readonly Dictionary<string, SpecialEventData> All = new();

	static SpecialEventLibrary()
	{
		// 第一阶段:学生时期
		All["student_expectation"] = new SpecialEventData(
			"student_expectation",
			"【过度期待方块】",
			"耳边又是熟悉的对比与期许,所有人都希望你变得更优秀、更完美,追赶别人的脚步。面对扑面而来的期待压力,你选择?",
			new EventOption[]
			{
				new EventOption("接纳期待,但不盲从,按自己的节奏慢慢进步", 20, 0,
					"我不必活成别人的标准答案,认真走好自己的路,就是最好的成长。", true),
				new EventOption("默默抵触、自我怀疑,觉得自己永远达不到期待", -30, 2,
					"无尽的对比让我喘不过气,我好像永远都不够好,负面情绪不断蔓延。", false),
				new EventOption("把不满向外发泄,顶撞家人,将压力全部归咎于他人的要求", -45, 3,
					"都是你们对我要求太高才变成这样,全部都是你们的错,我一点问题都没有。", false)
			}
		);

		All["student_exam"] = new SpecialEventData(
			"student_exam",
			"【考试焦虑方块】",
			"考试将至,堆积的知识点、未知的结果让你彻夜难安,恐惧失误、恐惧掉队的情绪不断放大。你选择如何面对这份焦虑?",
			new EventOption[]
			{
				new EventOption("接纳不完美,踏实复盘,尽力即可,不纠结结果", 20, 0,
					"人生从不是一次定输赢,稳步前行、接纳缺憾,就是与自己和解。", true),
				new EventOption("过度紧绷熬夜内耗,过度焦虑自我施压,逃避拖延", -30, 2,
					"我必须做到最好,一旦出错就是全盘皆输,焦虑一点点吞噬我的状态。", false),
				new EventOption("迁怒外界,抱怨考题、抱怨环境,甚至迁怒同学,不愿正视自身的问题", -45, 3,
					"题目太难、环境不好,都怪周围的一切,才让我没有办法好好发挥。", false)
			}
		);

		All["student_peer"] = new SpecialEventData(
			"student_peer",
			"【同学攀比方块】",
			"身边同学成绩优异、进步飞快,反观自己的进度平平无奇,落差感不断滋生。面对同辈压力,你选择?",
			new EventOption[]
			{
				new EventOption("专注自我成长,借鉴他人优势,不盲目攀比内耗", 20, 0,
					"每个人都有自己的花期,不必跟风赶路,超越昨天的自己就足够了。", true),
				new EventOption("陷入自卑嫉妒,否定自我,跟风内卷透支状态", -30, 2,
					"别人都在发光,只有我停滞不前,差距越来越大,自我价值感不断崩塌。", false),
				new EventOption("嫉妒排挤优秀的同龄人,贬低对方的成果,把自己的失意归咎于别人太过耀眼", -45, 3,
					"要不是他那么突出,我也不会显得这么差劲,是他抢走了本该属于我的关注。", false)
			}
		);

		// 第二阶段:职场时期
		All["work_overwork"] = new SpecialEventData(
			"work_overwork",
			"【盲目内卷方块】",
			"身边同事纷纷加班内卷,所有人都在被动消耗,不跟风就会被定义为“不努力”。面对职场内卷洪流,你选择?",
			new EventOption[]
			{
				new EventOption("坚守自我节奏,高效完成工作,拒绝无效透支身体", 20, 0,
					"努力的意义是成长,不是消耗,守住节奏,才是成年人的清醒。", true),
				new EventOption("跟风内卷熬夜加班,透支身心,陷入无意义的内耗竞争", -30, 2,
					"大家都在拼,我不敢停下,哪怕身心俱疲,也只能硬着头皮追赶。", false),
				new EventOption("内心积攒怨气,表面维持和气,转头对弱者、亲近的人发泄积攒的社交负面情绪", -45, 3,
					"在外面我不得不忍让,回到别处,我总要找个出口把憋住的火气宣泄出去。", false)
			}
		);

		All["work_people_pleasing"] = new SpecialEventData(
			"work_people_pleasing",
			"【人际讨好方块】",
			"职场人际繁杂,为了维系关系、避免矛盾,你常常委屈自己、迁就他人。面对社交内耗,你选择?",
			new EventOption[]
			{
				new EventOption("保持分寸社交,允许自己独处,拒绝无底线讨好", 20, 0,
					"成年人的社交,舒服最重要,不必取悦所有人,善待自己才是底色。", true),
				new EventOption("一味妥协讨好,压抑自我情绪,强行维系所有人际关系", -30, 2,
					"我不敢拒绝,只能不断委屈自己,疲惫感一点点堆满生活。", false),
				new EventOption("内心积攒怨气,表面维持和气,转头对弱者、亲近的人发泄积攒的社交负面情绪", -45, 3,
					"在外面我不得不忍让,回到别处,我总要找个出口把憋住的火气宣泄出去。", false)
			}
		);

		All["work_responsibility"] = new SpecialEventData(
			"work_responsibility",
			"【责任焦虑方块】",
			"工作任务、生活责任层层叠加,担子越来越重,你时常感到无力承压、身心俱疲。面对堆积的责任压力,你选择?",
			new EventOption[]
			{
				new EventOption("拆分压力分步解决,接纳自身局限,不过度苛责自己", 20, 0,
					"生活本就负重前行,慢慢来,扛不住就歇一歇,尽力即是圆满。", true),
				new EventOption("全盘焦虑自我施压,纠结完美结果,陷入无力内耗", -30, 2,
					"所有担子都压在我身上,我必须全部扛住,一点差错都不能有。", false),
				new EventOption("怨怼身边的人,指责家人同事不能够分担,把重担带来的痛苦发泄到身边人身上", -45, 3,
					"凭什么只有我一个人扛下所有,你们都该替我分担,都是你们不够体谅我。", false)
			}
		);

		// 第三阶段:老年时期
		All["elder_regret"] = new SpecialEventData(
			"elder_regret",
			"【过往遗憾方块】",
			"回望一生,有太多错过、亏欠与未圆满,遗憾的情绪反复涌上心头,久久无法释怀。你选择如何面对过往?",
			new EventOption[]
			{
				new EventOption("接纳人生不圆满,与遗憾和解,珍惜当下余生", 20, 0,
					"人生本就是半满半缺,遗憾是常态,放过过往,就是放过自己。", true),
				new EventOption("沉湎过往遗憾,反复追忆内耗,悔恨终生", -30, 2,
					"如果当初再勇敢一点、再努力一点,人生会不会不一样,遗憾始终无法消解。", false),
				new EventOption("把遗憾归咎于他人,反复向晚辈诉苦追责,把当年的失意怪罪到旁人头上", -45, 3,
					"要不是当年别人阻碍我,我本该拥有不一样的人生,如今的不如意,都是别人造成的。", false)
			}
		);

		All["elder_loneliness"] = new SpecialEventData(
			"elder_loneliness",
			"【岁月孤独方块】",
			"岁月更迭,身边人来人往,热闹散尽,晚年的独处与孤独愈发明显。面对独处的孤寂,你选择?",
			new EventOption[]
			{
				new EventOption("接纳独处,享受安静的余生,与孤独温柔共处", 20, 0,
					"人终要学会与自己相伴,安静的岁月,亦是圆满的风景。", true),
				new EventOption("抗拒孤独,沉溺落寞,感叹岁月荒芜、人生冷清", -30, 2,
					"热闹再也回不来了,余生只剩孤寂,岁月漫长却毫无温度。", false),
				new EventOption("把孤独归咎于家人陪伴不够,反复索取关注,把冷清感发泄到身边人身上", -45, 3,
					"你们都不在我身边,我才这么孤单,都是你们不关心我。", false)
			}
		);

		All["elder_worry_young"] = new SpecialEventData(
			"elder_worry_young",
			"【担忧晚辈方块】",
			"看着后辈在外奔波打拼,总担心他们受累、吃亏、受挫,满心牵挂却无力帮忙,满心焦虑。你选择?",
			new EventOption[]
			{
				new EventOption("适度放手,相信后辈的成长,安享自己的晚年生活", 20, 0,
					"儿孙自有儿孙福,学会放手,才是晚年最好的释然。", true),
				new EventOption("过度操心、日夜牵挂,终日为后辈焦虑内耗", -30, 2,
					"我放不下心,怕他们过得不好,这份牵挂日日折磨自己。", false),
				new EventOption("不断干涉晚辈的人生选择,把自己的焦虑反复输出给后辈,用担忧裹挟对方听从自己", -45, 3,
					"我都是为了你好,你不听我的就一定会吃苦,我的焦虑全部都是你的选择造成的。", false)
			}
		);
	}
}
