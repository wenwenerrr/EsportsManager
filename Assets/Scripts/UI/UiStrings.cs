using System;
using EsportsManager.Matches;
using EsportsManager.Segments;

namespace EsportsManager.UI
{
    /// <summary>
    /// ALL player-facing text of the UI (Chinese), in one place so it can be extracted to a localization table later.
    /// UI code must reference these members and never contain string literals for the player.
    /// </summary>
    public static partial class UiStrings
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
        public const string ClickDimToTrain = "小条 = 专精";
        public const string PreviewFormat = "预计  压力 {0}  心情 {1}  直播收入 {2}";
        public const string Sponsor = "拉赞助";
        public const string UndoSponsor = "撤销赞助";
        public const string SponsorInfoFormat = "赞助尝试 {0}（剩余 {1}）· 空闲合约位 {2} · 成功率约 {3}";
        public const string ClearPlan = "清空计划";
        public const string Resolve = "结算本赛段";
        public const string OffersBadgeFormat = "商务邀约 {0}";
        public const string IncomeLineFormat = "直播收入 {0} · 商务分成 {1}";
        public const string Hint = "点“训练”选阶段练选手、或安排直播 / 休息，也可以拉赞助；完成后结算本赛段";
        public const string Added = "已加入计划：{0} {1}";
        public const string Undone = "已撤销 {0} 的最后一项行动";
        public const string NothingToUndo = "{0} 没有可撤销的行动";
        public const string Cleared = "计划已清空";

        // ---- growth (Story 012, design/gdd/growth.md): phases, focus, specialties, training pop-up
        public const string TargetEarly = "前期";
        public const string TargetMid = "团战";
        public const string TargetLate = "后期";
        public const string TargetCommand = "指挥";
        public const string PhaseStrengthItemFormat = "{0} {1}";
        public const string FocusTitle = "专精";
        public const string NearCapTag = "近上限";
        public const string SpecialtyTitle = "绝活";
        public const string SpecialtyNone = "暂无绝活";
        public const string SpecialtyGapPrefixFormat = "离「{0}」还差：";
        public const string SpecialtyGapDimFormat = "{0} +{1}";
        public const string SpecialtyGapFocusFormat = "{0}专精 +{1}";
        public const string SpecialtyGapJoin = "，";
        public const string SpecialtyUnlocked = "已解锁";
        public const string TrainButton = "训练";
        public const string TrainAddedFormat = "练{0}";
        public const string TrainAddedFocusFormat = "练{0}（重点 {1}）";
        public const string TrainTitleFormat = "训练 · {0}（{1}）";
        public const string TrainChooseTarget = "1. 选择要练的阶段（该阶段所有维度一起涨）";
        public const string TrainChooseFocus = "2. 重点维度（可选）：在本阶段内多分涨幅，阶段平均涨幅不变";
        public const string TrainNoFocus = "不指定";
        public const string TrainFocusSingle = "这个阶段只有一个维度，不需要指定重点";
        public const string TrainCommandNote = "指挥只练指挥能力，三个阶段的专精都会略降";
        public const string TrainPreviewTitle = "预计涨幅（训练有 ±15% 随机）";
        public const string TrainRangeFormat = "{0} +{1}～{2}";
        public const string TrainOtherFormat = "其他维度各约 +{0}～{1}";
        public const string TrainStateNote = "心情越好练得越快；压力超过 50 后越来越练不动；越接近上限涨得越慢";
        public const string TrainConfirm = "加入训练计划";
        public const string TrainClose = "关闭";
        public const string TrainPointsFormat = "该选手本赛段已用行动点 {0}/{1}";
        public const string TrainSummaryFormat = "{0}×{1}";
        public const string TrainDimsTotalFormat = "维度合计 {0}";
        public const string SpecialtyChanges = "绝活变化";
        public const string SpecialtyGainedFormat = "解锁绝活！{0} 获得「{1}」";
        public const string SpecialtyLostFormat = "{0} 失去了绝活「{1}」";
        public const string NoSpecialtyChange = "本赛段没有绝活变化";

        /// <summary>
        /// "离「X」还差：维度 +a，前期专精 +b" (growth.md UI: max(0, 76 - d) and max(0, 0.55 - focus)); a part that is already met is
        /// left out; both met = "已解锁" is shown by the caller for owned specialties.
        /// </summary>
        public static string SpecialtyGap(string name, string dimName, float dimGap, int phase, float focusGap)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (dimGap > 0.05f) parts.Add(F(SpecialtyGapDimFormat, dimName, dimGap.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)));
            if (phase >= 0 && focusGap > 0.005f) parts.Add(F(SpecialtyGapFocusFormat, GrowthPhaseName(phase), focusGap.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)));
            return F(SpecialtyGapPrefixFormat, name) + string.Join(SpecialtyGapJoin, parts);
        }

        /// <summary>Chinese name of a training target (前期 / 团战 / 后期 / 指挥).</summary>
        public static string TargetName(EsportsManager.Growth.TrainingTarget t)
            => t == EsportsManager.Growth.TrainingTarget.Early ? TargetEarly : t == EsportsManager.Growth.TrainingTarget.Mid ? TargetMid
                : t == EsportsManager.Growth.TrainingTarget.Late ? TargetLate : TargetCommand;

        /// <summary>Chinese name of a phase index 0-2 for growth / strength displays (前期 / 团战 / 后期).</summary>
        public static string GrowthPhaseName(int k) => TargetName((EsportsManager.Growth.TrainingTarget)k);

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

        // ---- random events (Story 006): choice pop-up, leave, settlement cards
        public const string ChoiceTitle = "需要你做决定";
        public const string ChoiceCountFormat = "（{0}/{1}）";
        public const string ChoiceDefaultTag = "（默认）";
        public const string ChoiceSelectedTag = "（已选择）";
        public const string ChoiceDefaultNote = "不做选择时，结算时按默认选项处理";
        public const string ChoiceLater = "稍后决定";
        public const string ChoiceAnsweredFormat = "已选择：{0}";
        public const string ChoicesBadgeFormat = "待决定 {0}";
        public const string ChoicesReview = "已做的决定";
        public const string OnLeaveTag = "请假中";
        public const string OnLeaveCardNote = "请假中，本赛段不能安排行动";
        public const string EventsTitle = "本赛段事件";
        public const string EventsCountFormat = "共 {0} 件";
        public const string NoEvents = "本赛段风平浪静，没有发生意外事件";
        public const string EventChoiceTakenFormat = "你的选择：{0}";
        public const string EventIncome = "事件收入";
        public const string EventExpenses = "事件支出";
        public const string SponsorVoidedFormat = "第 {0} 次：被放鸽子，作废";
        // effect sentences ({0} = signed value); prefixes name who is affected (per person / per pair)
        public const string EffMood = "{0}心情 {1}";
        public const string EffStress = "{0}压力 {1}";
        public const string EffPlayerPopularity = "{0}人气 {1}";
        public const string EffClubPopularity = "俱乐部人气 {0}";
        public const string EffFamiliarity = "{0}默契 {1}";
        public const string EffPotentialUp = "潜力上升";
        public const string EffPotentialDown = "潜力下降";
        public const string EffFunds = "资金 {0}";
        public const string EffTrainMultiplier = "训练收益 ×{0}";
        public const string EffTrainAdd = "本次训练项目 {0}";
        public const string EffStreamMultiplier = "直播收入 ×{0}";
        public const string EffStreamAdd = "直播收入 {0}";
        public const string EffRestMultiplier = "休息减压 ×{0}";
        public const string EffBusinessMultiplier = "商务分成 ×{0}";
        public const string EffSponsorChance = "赞助成功率 {0}";
        public const string EffVoidAction = "这次拉赞助作废";
        public const string EffOnLeave = "请假一个赛段（不能安排行动）";
        public const string EffNone = "没有其他影响";
        public const string EffPrefixRoster = "全队";
        public const string EffPrefixStarters = "首发";
        public const string EffPrefixPair = "两人";
        public const string EffPrefixWithStarters = "与其他首发";
        public const string EffJoin = "、";
        public const string MoneyWanFormat = "{0} 万";
        public const string MoneyYuanFormat = "{0} 元";
        public const string Plus = "+";
        public const string Minus = "−";

        // ---- plan errors (PlanErrorCode -> text)
        public const string ErrUnknownChoice = "这个事件已经过期了";
        public const string ErrChoiceOption = "没有这个选项";
        public const string ErrPlayerOnLeave = "{0} 本赛段请假回家了，不能安排行动";
        public const string ErrPlayerOverLimit = "{0} 本赛段已用满 {1} 个行动点（单人上限）";
        public const string ErrOverTotal = "全队行动点已用完（共 {0} 点）";
        public const string ErrSponsorOverLimit = "本赛段最多拉赞助 {0} 次";
        public const string ErrContractsFull = "赞助合约位已满（{0} 个），无法再拉赞助";
        public const string ErrSegmentNotOpen = "当前没有进行中的赛段";
        public const string ErrDuplicateOffer = "这份邀约已经接受";
        public const string ErrUnknownOffer = "这份邀约已失效";
        public const string ErrOfferPlayerLeft = "该选手已离队，邀约作废";
        public const string ErrDimension = "重点维度必须属于所练的阶段";
        public const string ErrUnknownTrainingTarget = "训练目标无效";
        public const string ErrTrainingUnavailable = "当前模式不能训练（缺少比赛配置）";
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
        // ---- Story 013: job offers, style tags, opponent info, league news
        public const string OffersTitle = "三份聘书";
        public const string OffersSubtitle = "联赛已经运行了一整年。三家俱乐部想请你当经理，选一家接手。";
        public const string OfferStrong = "强队";
        public const string OfferMid = "中游";
        public const string OfferWeak = "弱队";
        public const string OfferFunds = "资金";
        public const string OfferLastRankFormat = "去年第 {0} 名";
        public const string OfferStyleFormat = "风格：{0}";
        public const string OfferExpiringFormat = "本年到期合同 {0} 人";
        public const string OfferPick = "选这家";
        public const string OfferPicked = "已选择";
        public const string OfferNamePrompt = "队名（可改，最多 {0} 个字）";
        public const string OfferConfirm = "接手并开始";
        public const string OfferPickFirst = "先选择一家俱乐部";
        public const string OfferTakenFormat = "你接手了 {0}。新老板注资后开始第 1 年。";
        public const string OfferTopUpNote = "接手时新老板会把资金补到至少 6 个赛段的工资（弱队 8 个赛段）。";
        public const string StyleInvade = "入侵";
        public const string StyleTeamfight = "团战";
        public const string StyleLate = "后期";
        public const string StyleStar = "明星";
        public const string StyleTagFormat = "{0} · {1}";
        public const string OpponentTitle = "对手信息";
        public const string OpponentBenchFormat = "替补：{0}";
        public const string OpponentPick = "查看其他对手";
        public const string OpponentRecentTactics = "最近 5 场战术：";
        public const string OpponentNoTactics = "还没有比赛记录";
        public const string OpponentLastMeeting = "上次交手（我方 − 对方）：";
        public const string OpponentNeverMet = "还没交过手";
        public const string OpponentWon = "（我方胜）";
        public const string OpponentLost = "（我方负）";
        public const string OpponentPhaseFormat = "{0} {1}";
        public const string OpponentRadarNote = "雷达图三个角：前期（上）/ 中期 / 后期，所有俱乐部同一刻度";
        public const string NewsTitle = "联赛新闻";
        public const string NewsEmpty = "暂无新闻";
        public const string NewsWarmUp = "去年";
        public const string NewsYearFormat = "第 {0} 年";
        public const string NewsStyleFormat = "{0} 改打法：{1} → {2}";
        public const string NewsSigningFormat = "{0} 签下 {1}";
        public const string NewsFireSaleFormat = "{0} 为补资金把 {1} 卖给联盟（¥{2}）";
        public const string NewsLoanFormat = "{0} 资金耗尽，向联盟借款 ¥{1}";
        public const string NewsSpecialtyFormat = "{0} 的 {1} 练成绝活「{2}」";
        public const string NewsChampionFormat = "{0} 获得{1}冠军";

        /// <summary>Display name of a club style.</summary>
        public static string StyleName(EsportsManager.Leagues.ClubStyle s)
        {
            switch (s)
            {
                case EsportsManager.Leagues.ClubStyle.Invade: return StyleInvade;
                case EsportsManager.Leagues.ClubStyle.Teamfight: return StyleTeamfight;
                case EsportsManager.Leagues.ClubStyle.Late: return StyleLate;
                default: return StyleStar;
            }
        }

        /// <summary>Offer tier title.</summary>
        public static string TierName(EsportsManager.Leagues.OfferTier t)
            => t == EsportsManager.Leagues.OfferTier.Strong ? OfferStrong : t == EsportsManager.Leagues.OfferTier.Mid ? OfferMid : OfferWeak;

        /// <summary>Phase name by index.</summary>
        public static string PhaseName(int k) => k == 0 ? PhaseEarly : k == 1 ? PhaseMid : PhaseLate;

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

        // ---- report reasons (Story 011, design/gdd/match.md): MatchFactor + sign -> sentence; {0} = "name（lane）", {1} = our tactic, {2} = their tactic
        public const string SubjectFormat = "{0}（{1}）";
        public const string ReasonValueFormat = "（{0} 分）";
        public const string ReasonEarlyUs = "前期对线我方更强";
        public const string ReasonEarlyUsSubject = "前期对线我方更强，{0}优势最大";
        public const string ReasonEarlyThem = "前期对线对方更强";
        public const string ReasonEarlyThemSubject = "前期对线对方更强，{0}吃亏最多";
        public const string ReasonEarlyEven = "双方前期不相上下";
        public const string ReasonMidUs = "中期团战我方更强（团战能力和默契）";
        public const string ReasonMidUsSubject = "中期团战我方更强，{0}发挥最突出";
        public const string ReasonMidThem = "中期团战对方更强（团战能力和默契）";
        public const string ReasonMidThemSubject = "中期团战对方更强，{0}差距最大";
        public const string ReasonMidEven = "双方中期团战不相上下";
        public const string ReasonLateUs = "后期我方核心输出更强";
        public const string ReasonLateUsSubject = "后期我方核心输出更强，{0}最关键";
        public const string ReasonLateThem = "后期对方核心输出更强";
        public const string ReasonLateThemSubject = "后期对方核心输出更强，{0}差距最大";
        public const string ReasonLateEven = "双方后期不相上下";
        public const string ReasonStarUs = "我方有明星选手打出了单人优势";
        public const string ReasonStarUsSubject = "我方{0}是场上明星，单人优势远超全队平均";
        public const string ReasonStarThem = "对方有明星选手打出了单人优势";
        public const string ReasonStarThemSubject = "对方明星选手在这一路压制了我方{0}";
        public const string ReasonStarEven = "双方都没有明显的明星选手";
        public const string ReasonCycleUs = "阶段克制对我方有利：对方的优势阶段被我方的强项压住了";
        public const string ReasonCycleThem = "阶段克制对对方有利：我方的优势阶段被对方的强项压住了";
        public const string ReasonCycleEven = "阶段克制对双方影响差不多";
        public const string ReasonTacticUs = "战术选择更合适（我方“{1}”对对方“{2}”）";
        public const string ReasonTacticThem = "战术选择吃亏（我方“{1}”对对方“{2}”）";
        public const string ReasonTacticEven = "双方战术选择差不多";
        public const string ReasonSpecialtyUs = "我方选手的绝活起了作用";
        public const string ReasonSpecialtyThem = "对方选手的绝活起了作用";
        public const string ReasonSpecialtyEven = "绝活对比赛影响不大";
        public const string ReasonLuckUs = "我方临场发挥更好";
        public const string ReasonLuckThem = "对方临场发挥更好，我方状态起伏";
        public const string ReasonLuckEven = "双方临场发挥都很正常";
        public const string LuckUnluckyFormat = "运气：第 {0} 局我方本更有把握（{1}%），却输了";
        public const string LuckLuckyFormat = "运气：第 {0} 局对方本更有把握（{1}%），我方运气好拿下";
        // report notes (even, cycle, tactic advice)
        public const string NotesTitle = "比赛走势";
        public const string EvenFormat = "两队势均力敌（我方平均把握 {0}%），胜负差在细节";
        public const string CycleEarlyEndUs = "我方前期领先，把比赛提前结束，对方的后期优势没完全兑现";
        public const string CycleEarlyEndThem = "对方前期领先，提前结束了比赛，我方的后期优势没完全兑现";
        public const string CycleTeamfightUs = "我方团战更强，一波团战抹平了对方的前期优势";
        public const string CycleTeamfightThem = "对方团战更强，一波团战抹平了我方的前期优势";
        public const string CycleLateUs = "我方后期更强，拖到后期避开了对方擅长的团战";
        public const string CycleLateThem = "对方后期更强，拖到后期避开了我方擅长的团战";
        public const string TacticBetterFormat = "换成“{0}”本可 +{1} 分（每局约 +{2}%）";

        // ---- pre-match estimate (Story 011)
        public const string OurStrengthTitle = "我方实力（当前首发与战术）";
        public const string PhaseEarly = "前期";
        public const string PhaseMid = "中期";
        public const string PhaseLate = "后期";
        public const string PhaseStrengthFormat = "{0}";
        public const string WeakestFormat = "短板：{0} {1}（{2}）";
        public const string ExecutionLabel = "执行力";
        public const string ExecutionNote = "由默契和指挥决定，影响战术发挥";
        public const string FocusHintFormat = "战术专注：{0}。选择自己领先的阶段有小幅加成";
        public const string FocusFormat = "{0}→{1}";
        public const string PreviewUnavailable = "首发补齐后显示我方实力";

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

        /// <summary>Chinese name of a phase.</summary>
        public static string PhaseName(MatchPhase k)
            => k == MatchPhase.Early ? PhaseEarly : k == MatchPhase.Mid ? PhaseMid : PhaseLate;

        /// <summary>Display name of a tactic.</summary>
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
                case PlanErrorCode.UnknownTrainingTarget: return ErrUnknownTrainingTarget;
                case PlanErrorCode.TrainingUnavailable: return ErrTrainingUnavailable;
                case PlanErrorCode.UnknownPlayer: return ErrUnknownPlayer;
                case PlanErrorCode.IncompleteLineup: return string.Format(ErrIncompleteLineup, e.Used ?? 0, e.Limit ?? 0);
                case PlanErrorCode.UnknownTactic: return ErrUnknownTactic;
                case PlanErrorCode.EventPending: return ErrEventPending;
                case PlanErrorCode.WrongEventPhase: return ErrWrongEventPhase;
                case PlanErrorCode.LineupLocked: return ErrLineupLocked;
                case PlanErrorCode.PlanNull: return ErrPlanNull;
                case PlanErrorCode.UnknownChoice: return ErrUnknownChoice;
                case PlanErrorCode.ChoiceOptionOutOfRange: return ErrChoiceOption;
                case PlanErrorCode.PlayerOnLeave: return string.Format(ErrPlayerOnLeave, who);
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
