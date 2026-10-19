using System;
using EsportsManager.Matches;
using EsportsManager.Segments;

namespace EsportsManager.UI
{
    /// <summary>
    /// ALL player-facing text of the UI (Chinese), in one place so it can be extracted to a localization table later.
    /// UI code must reference these members and never contain string literals for the player.
    /// </summary>
    public static class UiStrings
    {
        // ---- main screen header
        public const string Strength = "实力";
        public const string Popularity = "人气";
        public const string Funds = "资金";
        public const string Chemistry = "战队默契";
        public const string ActionPoints = "行动点余量";
        public const string SegmentFormat = "第 {0} 赛段";
        public const string Yuan = "¥";

        // ---- player card
        public const string Starter = "首发";
        public const string Bench = "替补";
        public const string AgeFormat = "{0} 岁";
        public const string Mood = "心情";
        public const string Stress = "压力";
        public const string Looks = "颜值";
        public const string PopularityShort = "人气";
        public const string Salary = "薪资";
        public const string SalaryPerYearFormat = "¥{0}/年";
        public const string ContractLeft = "合同剩余";
        public const string ContractYearsFormat = "{0} 年";
        public const string PotentialEstimate = "潜力估计";
        public const string PotentialRangeFormat = "{0}–{1}";
        public const string TraitSeparator = " · ";
        public const string PairFormat = "{0} / {1}";
        public const string BeforeAfterFormat = "{0} → {1}";
        public const string PartSeparator = "  ";
        public const string ActionSeparator = " ";

        // ---- assignment
        public const string Train = "训练";
        public const string TrainDimFormat = "训练 {0}";
        public const string Stream = "直播";
        public const string Rest = "休息";
        public const string Business = "商务";
        public const string Undo = "撤销";
        public const string NoActions = "未分配行动";
        public const string ActionCountFormat = "{0}×{1}";
        public const string PlayerPointsFormat = "行动点 {0}/{1}";
        public const string ClickDimToTrain = "点维度条 = 训练该维度";
        public const string PreviewFormat = "预计  压力 {0}  心情 {1}  直播收入 {2}";
        public const string Sponsor = "拉赞助";
        public const string UndoSponsor = "撤销赞助";
        public const string SponsorInfoFormat = "赞助尝试 {0}（剩余 {1}）· 空闲合约位 {2} · 成功率约 {3}";
        public const string ClearPlan = "清空计划";
        public const string Resolve = "结算本赛段";
        public const string OffersBadgeFormat = "商务邀约 {0}";
        public const string IncomeLineFormat = "直播收入 {0} · 商务分成 {1}";
        public const string Hint = "点维度条训练、或给选手安排直播 / 休息，也可以拉赞助；完成后结算本赛段";
        public const string Added = "已加入计划：{0} {1}";
        public const string Undone = "已撤销 {0} 的最后一项行动";
        public const string NothingToUndo = "{0} 没有可撤销的行动";
        public const string Cleared = "计划已清空";

        // ---- business pop-up
        public const string OfferTitle = "商务邀约";
        public const string OfferCountFormat = "（{0}/{1}）";
        public const string OfferBodyFormat = "选手 {0} 收到一份商务邀约";
        public const string OfferAmountFormat = "商务金额 {0}，俱乐部分成 {1}";
        public const string OfferCostFormat = "接受需要占用该选手 1 个行动点（已用 {0}/{1}，全队剩余 {2}）";
        public const string Accept = "接受";
        public const string Decline = "拒绝";
        public const string Later = "稍后处理";
        public const string OfferAccepted = "已接受 {0} 的商务邀约";
        public const string OfferDeclined = "已拒绝 {0} 的商务邀约";

        // ---- settlement summary
        public const string SummaryTitleFormat = "第 {0} 赛段结算";
        public const string NextSegment = "下一赛段";
        public const string TeamChanges = "战队变化";
        public const string MoneyLines = "收支";
        public const string Income = "收入合计";
        public const string Expenses = "支出合计";
        public const string SalaryShare = "其中薪资";
        public const string Net = "净额";
        public const string LivestreamIncome = "直播收入";
        public const string BusinessIncome = "商务分成";
        public const string SponsorIncome = "赞助收入";
        public const string PrizeMoney = "奖金";
        public const string SponsorResults = "赞助";
        public const string NoSponsorAttempt = "本赛段没有拉赞助";
        public const string SponsorAttemptFormat = "第 {0} 次：成功率 {1} → {2}";
        public const string Success = "成功";
        public const string Failure = "失败";
        public const string NewContractFormat = "新合约 {0}/赛段，共 {1} 赛段";
        public const string Offers = "商务";
        public const string NoOffers = "本赛段没有商务邀约";
        public const string OfferAcceptedFormat = "接受：{0} {1}（俱乐部 {2}）";
        public const string OfferDeclinedFormat = "拒绝：{0} {1}";
        public const string OfferVoidedFormat = "作废：{0} {1}（选手已离队）";
        public const string PlayerChanges = "选手变化";
        public const string ColPlayer = "选手";
        public const string ColActions = "行动";
        public const string ColMood = "心情";
        public const string ColStress = "压力";
        public const string ColPopularity = "人气";
        public const string ColTraining = "训练收益";
        public const string TrainingTargetFormat = "{0} {1}";
        public const string TrainingTotalFormat = "六维合计 {0}";
        public const string NoTraining = "—";
        public const string PointsUnusedFormat = "使用行动点 {0}，浪费 {1}";

        // ---- plan errors (PlanErrorCode -> text)
        public const string ErrPlayerOverLimit = "{0} 本赛段已用满 {1} 个行动点（单人上限）";
        public const string ErrOverTotal = "全队行动点已用完（共 {0} 点）";
        public const string ErrSponsorOverLimit = "本赛段最多拉赞助 {0} 次";
        public const string ErrContractsFull = "赞助合约位已满（{0} 个），无法再拉赞助";
        public const string ErrSegmentNotOpen = "当前没有进行中的赛段";
        public const string ErrDuplicateOffer = "这份邀约已经接受";
        public const string ErrUnknownOffer = "这份邀约已失效";
        public const string ErrOfferPlayerLeft = "该选手已离队，邀约作废";
        public const string ErrDimension = "训练维度无效";
        public const string ErrUnknownPlayer = "选手不在阵中";
        public const string ErrGeneric = "这份计划暂时无法执行";
        public const string ErrIncompleteLineup = "首发只有 {0}/{1} 人：在替补卡片上点“上场”补齐后才能结算";
        public const string ErrUnknownTactic = "请选择一个战术倾向";
        public const string ErrEventPending = "赛事还没结束，先打完并确认赛果";
        public const string ErrWrongEventPhase = "现在不能进行这一步赛事操作";
        public const string ErrLineupLocked = "现在不能调整首发和战术";
        public static string EventName(string key)
        {
            switch (key)
            {
                case "spring": return "春季赛";
                case "summer": return "夏季赛";
                case "finals": return "年度总决赛";
                default: return key;
            }
        }
        public const string ErrPlanNull = "计划为空";
        public const string ErrBadContext = "赛段数据异常，请重新开始游戏";
        public const string ErrNegativeSalary = "{0} 的薪资为负数";
        public const string ErrUnknownAction = "{0} 的行动无效";

        // ---- new club (Story 004)
        public const string NewClubTitle = "新建战队";
        public const string NewClubPromptFormat = "给你的俱乐部起个名字（最多 {0} 个字）";
        public const string NewClubDefaultFormat = "不填写则使用默认名：{0}";
        public const string NewClubConfirm = "开始经营";
        public const string ClubNamedFormat = "欢迎来到联赛，{0}！";

        // ---- lineup & tactic (Story 004)
        public const string StarterToggleOff = "首发 · 换下";
        public const string BenchToggleOn = "替补 · 上场";
        public const string StarterToggled = "{0} 已换下，他的位置空出";
        public const string BenchToggled = "{0} 已上场（{1}）";
        public const string OurTactic = "我方战术";
        public const string TacticSet = "战术已改为 {0}";
        public const string CounterJoin = " · ";
        public const string CounterFormat = "{0}克{1}";
        public const string TacticInvade = "入侵";
        public const string TacticDevelop = "发育";
        public const string TacticTeamfight = "团战";

        // ---- match report (Story 004)
        public const string Continue = "继续";
        public const string SeriesTacticsFormat = "我方战术 {0} · 对方战术 {1}";
        public const string SeriesScoreFormat = "{0} : {1}";
        public const string SeriesWon = "胜";
        public const string SeriesLost = "负";
        public const string GameLineFormat = "第 {0} 局 {1}";
        public const string GameWon = "胜";
        public const string GameLost = "负";
        public const string DeciderSuffix = "（决胜局）";
        public const string ReasonsTitle = "主要原因";
        public const string ReasonBullet = "· ";

        // ---- report reasons (MatchFactor + sign -> sentence); {0} = player name, {1} = lane, {2} = our tactic, {3} = their tactic
        public const string SubjectFormat = "{0}（{1}）";
        public const string ReasonSkillUs = "我方选手个人技术整体更强";
        public const string ReasonSkillUsSubjectAhead = "我方选手个人技术整体更强，{0}优势最明显";
        public const string ReasonSkillUsSubjectBehind = "我方选手个人技术整体更强，但{0}不如对面同位置选手";
        public const string ReasonSkillThem = "对方选手个人技术整体更强";
        public const string ReasonSkillThemSubjectBehind = "对方选手个人技术整体更强，{0}差距最大";
        public const string ReasonSkillThemSubjectAhead = "对方选手个人技术整体更强，只有{0}占了上风";
        public const string ReasonSkillEven = "双方个人技术不相上下";
        public const string ReasonChemistryUs = "我方队员配合更默契";
        public const string ReasonChemistryThem = "我方默契不足，配合不如对方";
        public const string ReasonChemistryEven = "双方默契不相上下";
        public const string ReasonStyleUs = "我方指挥更出色，选手都打在擅长的位置";
        public const string ReasonStyleThem = "对方指挥更出色，选手位置安排更合适";
        public const string ReasonStyleEven = "双方指挥和位置安排不相上下";
        public const string ReasonTacticUs = "我方战术“{0}”克制了对方的“{1}”";
        public const string ReasonTacticThem = "我方战术“{0}”被对方的“{1}”克制";
        public const string ReasonTacticEven = "双方战术互不克制";
        public const string ReasonStressUs = "对方发挥起伏更大，我方打得更稳";
        public const string ReasonStressThem = "我方队员压力偏高，发挥忽好忽坏";
        public const string ReasonStressThemSubject = "{0}压力过高，发挥忽好忽坏";
        public const string ReasonStressEven = "双方发挥都很稳定";
        public const string ReasonFormHot = "对手近期状态火热";
        public const string ReasonFormSlump = "对手近期状态低迷";
        public const string ReasonFormEven = "对手近期状态平稳";

        // ---- calendar & events (Story 004 stage C2, numbers.md 4.18)
        public const string YearFormat = "第 {0} 年";
        public const string YearTitleFormat = "第 {0} 年 {1}";
        public const string YearSegmentFormat = "第 {0} 年 · 第 {1}/{2} 赛段";
        public const string NextEventFormat = "距离 {0} 还有 {1} 个赛段";
        public const string NextEventAfterThisFormat = "本赛段结算后开始 {0}";
        public const string EventRunningFormat = "{0} 进行中";
        public const string NoEventLeft = "本年度赛事已全部结束";
        public const string StandingsButton = "积分榜";
        public const string StandingsButtonFormat = "积分榜 · 第 {0} 名";
        public const string NextStepFormat = "下一步：{0}赛前准备";
        public const string NewYearFormat = "第 {0} 年开始！转会期之后，新赛季的备战开始了";
        public const string PrepTitleFormat = "{0} · 赛前准备";
        public const string PlayoffPrepTitleFormat = "{0} · 季后赛前调整";
        public const string FinalsWatchTitleFormat = "{0} · 未获得参赛资格";
        public const string FormatSplitFormat = "常规赛 {0} 场 BO{1}，前 {2} 名进季后赛（半决赛 BO{3}，决赛 BO{4}）";
        public const string FormatSplitNoSemiFormat = "常规赛 {0} 场 BO{1}，前 {2} 名进季后赛（决赛 BO{3}）";
        public const string FormatPlayoffFormat = "季后赛：半决赛 1 号对 4 号、2 号对 3 号（BO{0}），决赛 BO{1}";
        public const string FormatPlayoffNoSemiFormat = "季后赛：前 2 名直接进行决赛（BO{0}）";
        public const string FormatFinalsFormat = "年度总决赛：两季积分前 {0} 名参加，单败淘汰，半决赛 BO{1}，决赛 BO{2}";
        public const string FormatFinalsNoSemiFormat = "年度总决赛：两季积分前 {0} 名参加，决赛 BO{1}";
        public const string PrepNoteRegular = "这套首发和战术会用于整个常规赛；季后赛前还能再调整一次";
        public const string PrepNotePlayoff = "进入季后赛前最后一次调整首发和战术的机会";
        public const string PrepNoteFinals = "这套首发和战术会用于整个总决赛";
        public const string PrepHint = "选好首发和战术后点“开赛”";
        public const string StartEvent = "开赛";
        public const string WatchFinals = "观看总决赛";
        public const string NotQualifiedFinals = "你的俱乐部没有进入年度总决赛，今年的总决赛由下面晋级的俱乐部进行";
        public const string PrepLineupIncompleteFormat = "首发只有 {0}/{1} 人：在替补选手上点“上场”补齐后才能开赛";
        public const string LineupTitle = "首发与替补";
        public const string RatingFormat = "评分 {0}";
        public const string MoodFormat = "心情 {0}";
        public const string StressFormat = "压力 {0}";
        public const string TacticTitle = "战术倾向";
        public const string HighStressFormat = "{0} 压力偏高：连续比赛会让压力上升，可以考虑换上替补";
        public const string NameJoin = "、";
        public const string QualificationTitle = "年度总决赛资格积分";
        public const string ColTotalPoints = "总积分";
        public const string ColQualified = "资格";
        public const string Qualified = "晋级";
        /// <summary>Top places while the year still has splits to play (not yet decided).</summary>
        public const string QualifiedProvisional = "暂列前 4";
        public const string NoValue = "—";
        public const string QualificationNoteFormat = "前 {0} 名进入年度总决赛 · 两季最终名次积分（第 1 名起）：{1} · 同分先比常规赛胜场";
        public const string PointsJoin = "/";

        // ---- regular season result
        public const string RegularTitleFormat = "{0} · 常规赛结果";
        public const string YourSeriesTitleFormat = "你的 {0} 场常规赛";
        public const string RoundFormat = "第 {0} 轮";
        public const string RegularStandingsTitle = "常规赛积分榜";
        public const string MadePlayoffsFormat = "常规赛第 {0} 名，进入季后赛！第一轮对手：{1}";
        public const string MissedPlayoffsFormat = "常规赛第 {0} 名，没能进入季后赛（前 {1} 名晋级）";
        public const string ToPlayoffPrep = "调整阵容并进入季后赛";
        public const string WatchPlayoffs = "查看季后赛";

        // ---- bracket / event result
        public const string BracketTitleFormat = "{0} · 最终结果";
        public const string BracketTitle = "淘汰赛对阵";
        public const string StageRegular = "常规赛";
        public const string StageSemifinal = "半决赛";
        public const string StageFinal = "决赛";
        public const string SeedFormat = "{0} 号";
        public const string ChampionFormat = "冠军 {0}";
        public const string PlacingsTitle = "最终名次与奖金";
        public const string YourResultTitle = "你的俱乐部";
        public const string PlacingChampionFormat = "夺得冠军！奖金 {0}";
        public const string PlacingFormat = "最终第 {0} 名，奖金 {1}";
        public const string NotInFinals = "没有进入年度总决赛，没有奖金";
        public const string FundsChangeFormat = "资金 {0} → {1}";
        public const string RecentWinRateFormat = "近期战绩（本次赛事系列赛胜率）{0}%";
        public const string PlayerEffectsTitle = "选手状态变化";
        public const string ColPlayed = "出场（胜-负）";
        public const string PlayedFormat = "{0} 场（{1}-{2}）";
        public const string NoEffects = "本次赛事没有选手出场";
        public const string ReportsTitle = "详细战报";
        public const string NoBracketSeries = "你的俱乐部没有参加淘汰赛";
        public const string SeriesTitleFormat = "{0} · 对手 {1}";
        public const string StandingsOfFormat = "第 {0} 年 {1} 常规赛";
        public const string NoSplitYet = "还没有打过常规赛，积分榜按俱乐部顺序显示";

        // ---- standings (Story 004)
        public const string StandingsTitle = "积分榜";
        public const string Back = "返回";
        public const string ColRank = "名次";
        public const string ColClub = "俱乐部";
        public const string ColSeries = "系列赛 胜-负";
        public const string ColGames = "小分 胜-负";
        public const string ColGameDiff = "净胜局";
        public const string WinLossFormat = "{0} - {1}";
        public const string RankFormat = "第 {0} 名";
        public const string RankChangeFormat = "名次 第 {0} → 第 {1} 名（{2}）";
        public const string RankUpFormat = "上升 {0}";
        public const string RankDownFormat = "下降 {0}";
        public const string RankSame = "持平";
        public const string SignedIntFormat = "+0;-0;0";
        public const string StandingsNoteFormat = "共 {0} 支俱乐部 · 先比系列赛胜场，再比净胜局 · 你的俱乐部高亮显示";

        /// <summary>string.Format with the invariant culture (numbers never change with the OS locale).</summary>
        public static string F(string format, params object[] args) => string.Format(System.Globalization.CultureInfo.InvariantCulture, format, args);

        /// <summary>Chinese name of a tactic.</summary>
        public static string TacticName(MatchTactic t)
        {
            switch (t)
            {
                case MatchTactic.Invade: return TacticInvade;
                case MatchTactic.Develop: return TacticDevelop;
                default: return TacticTeamfight;
            }
        }

        // The enum name is for developers only: it goes to the log, never on screen.
        private static void LogCode(PlanError e) => UnityEngine.Debug.Log("PlanError " + e.Code);

        /// <summary>Chinese text for a plan error; <paramref name="name"/> maps a player id to his name.</summary>
        public static string PlanError(PlanError e, Func<long, string> name)
        {
            string who = e.PlayerId.HasValue ? name(e.PlayerId.Value) : "";
            switch (e.Code)
            {
                case PlanErrorCode.PlayerOverLimit: return string.Format(ErrPlayerOverLimit, who, e.Limit);
                case PlanErrorCode.OverTotalPoints: return string.Format(ErrOverTotal, e.Limit);
                case PlanErrorCode.SponsorAttemptsOverLimit: return string.Format(ErrSponsorOverLimit, e.Limit);
                case PlanErrorCode.ContractsFull: return string.Format(ErrContractsFull, e.Limit);
                case PlanErrorCode.SegmentNotOpen: return ErrSegmentNotOpen;
                case PlanErrorCode.DuplicateOffer: return ErrDuplicateOffer;
                case PlanErrorCode.UnknownOffer: return ErrUnknownOffer;
                case PlanErrorCode.OfferPlayerLeft: return ErrOfferPlayerLeft;
                case PlanErrorCode.DimensionOutOfRange: return ErrDimension;
                case PlanErrorCode.UnknownPlayer: return ErrUnknownPlayer;
                case PlanErrorCode.IncompleteLineup: return string.Format(ErrIncompleteLineup, e.Used ?? 0, e.Limit ?? 0);
                case PlanErrorCode.UnknownTactic: return ErrUnknownTactic;
                case PlanErrorCode.EventPending: return ErrEventPending;
                case PlanErrorCode.WrongEventPhase: return ErrWrongEventPhase;
                case PlanErrorCode.LineupLocked: return ErrLineupLocked;
                case PlanErrorCode.PlanNull: return ErrPlanNull;
                case PlanErrorCode.NegativeSalary: return string.Format(ErrNegativeSalary, who);
                case PlanErrorCode.UnknownAction: return string.Format(ErrUnknownAction, who);
                case PlanErrorCode.BadSegmentsPerYear:
                case PlanErrorCode.BadSegmentIndex:
                case PlanErrorCode.BadSeasonRankScore:
                case PlanErrorCode.NegativePrizeMoney:
                case PlanErrorCode.NegativeSponsorPoints: LogCode(e); return ErrBadContext;
                default: LogCode(e); return ErrGeneric;
            }
        }
    }
}
