using System;
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
        public const string YearFormat = "（每年 {0} 赛段）";
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

        // ---- assignment
        public const string Train = "训练";
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
        public const string SponsorInfoFormat = "赞助尝试 {0}（剩余 {1}）· 空闲合约位 {2} · 成功率约 {3:P0}";
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
        public const string ChangeFormat = "{0} → {1}（{2}）";
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
        public const string SponsorAttemptFormat = "第 {0} 次：成功率 {1:P0} → {2}";
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
        public const string ErrGeneric = "计划无效（{0}）";

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
                default: return string.Format(ErrGeneric, e.Code);
            }
        }
    }
}
