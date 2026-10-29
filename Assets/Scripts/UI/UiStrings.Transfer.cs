using EsportsManager.Leagues;

namespace EsportsManager.UI
{
    /// <summary>Story 005 texts: transfer window, salary breakdown, champion fatigue, transfer news.</summary>
    public static partial class UiStrings
    {
        // ---- window frame
        public const string XferTitleFormat = "转会期 · 第 {0} 年结束";
        public const string XferRunwayFormat = "资金 {0} · 还能撑 {1} 个赛段";
        public const string XferRunwayInfinite = "资金 {0} · 无工资支出";
        public const string XferContinue = "下一步";
        public const string XferUnanswered = "还有选手没有处理";
        public const string XferWatching = "其他俱乐部正在操作……";
        public static readonly string[] XferSteps = { "更替", "重谈", "续约", "挖角", "三选一", "拍卖", "补签" };
        public const string XferRolloverFormat = "年度更替：{0} 人退役，{1} 名新人进入自由选手池；联赛工资指数 {2} → {3}";
        public const string XferNothing = "本步没有需要你处理的事项";

        // ---- salary breakdown
        public const string BreakdownFormat = "基础 {0} + 行情 {1} + 荣誉 {2} + 绝活 {3} + 年限 {4} = {5}";

        // ---- renegotiation
        public const string RenegTitle = "重谈：这些选手认为自己被低估了";
        public const string RenegHint = "同意：年薪改为市场价，合同年限不变。拒绝：心情 −15、想转会，并在本转会期被自动挂牌";
        public const string RenegLineFormat = "现年薪 {0} → 要求 {1}（合同剩 {2} 年）";
        public const string Agree = "同意";
        public const string Refuse = "拒绝";
        public const string Agreed = "已同意";
        public const string Refused = "已拒绝（将挂牌）";

        // ---- renewal
        public const string RenewTitle = "续约：合同到期的选手";
        public const string RenewHint = "年轻选手长约更贵，老将长约打折；放走的选手进入自由选手池";
        public const string RenewYearsFormat = "{0} 年 {1}";
        public const string ReleaseBtn = "放走";
        public const string RenewedFormat = "已续约 {0} 年，年薪 {1}";
        public const string Released = "已放走";
        public const string RenewOldFormat = "原年薪 {0}";

        // ---- poaching
        public const string PoachTitle = "挖角：向其他俱乐部的首发报价";
        public const string PoachHintFormat = "最多报价 {0} 次（已选 {1}）；转会费 = 身价 × 1.0–1.3，付得起才能报价。对方可以加薪留人";
        public const string PoachLineFormat = "{0} · 评分 {1} · 荣誉 {2} · 转会费 {3} · 来我队年薪 {4}";
        public const string PoachRosterFull = "名单已满 8 人，不能再挖人";
        public const string PoachQueue = "报价";
        public const string PoachUnqueue = "撤回";
        public const string PoachNone = "现在没有你付得起的挖角对象";
        public const string PoachStart = "发出报价并继续";
        public const string PoachPopupTitle = "收到挖角报价";
        public const string PoachPopupBodyFormat = "{0} 想要 {1}（{2}）";
        public const string PoachFeeFormat = "转会费：{0}";
        public const string PoachRetainFormat = "加薪留人：年薪 {0} → {1}，合同 +1 年（每个合同期只能留一次）";
        public const string PoachValueFormat = "当前身价 {0} · 薪资满意度 {1}";
        public const string PoachAccept = "接受转会费";
        public const string PoachRetain = "加薪留人";
        public const string PoachRetainUsed = "本合同期已经加薪留过一次，只能接受";
        public const string SatisfactionLow = "被低估";
        public const string SatisfactionOk = "合理";
        public const string SatisfactionHigh = "偏高";

        // ---- three-choice
        public const string XferChoiceTitle = "三选一：本转会期你要做什么？";
        public const string ChoiceAuction = "参加拍卖会";
        public const string ChoiceAuctionHint = "可以竞拍任意件；挂牌选手、2 名年度新人和 1 名成熟自由选手上拍";
        public const string ChoiceScout = "全国选拔";
        public const string ChoiceScoutHintFormat = "费用 {0}，生成 1 名 17–20 岁新人直接签入（3 年）；名单 8 人时不能选";
        public const string ChoiceSell = "出售（挂牌）";
        public const string ChoiceSellHint = "挂牌 1–2 名选手，不能竞拍；本年新签和分路唯一首发不能挂";
        public const string ChoiceSellConfirmFormat = "挂牌 {0} 人";
        public const string ChoiceSkip = "跳过";
        public const string ScoutNeedHigh = "这个位置比较缺人";
        public const string ScoutNeedLow = "不缺人";
        public const string LotsPreview = "本次拍品（联赛）";
        public const string LotLineFormat = "{0} · {1} · {2} 岁 · 潜力 {3} · 身价 {4} · 起拍 {5}";
        public const string LotKindYouth = "新人";
        public const string LotKindFree = "自由";
        public const string LotKindListedFormat = "{0} 挂牌";
        public const string LotKindForcedFormat = "{0} 强制挂牌";

        // ---- auction
        public const string AuctionTitle = "拍卖会";
        public const string AuctionPriceFormat = "当前价 {0} · 下一口 {1}";
        public const string AuctionTopFormat = "最高出价：{0}";
        public const string AuctionNobody = "还没有人出价";
        public const string AuctionInFormat = "在场：{0}";
        public const string AuctionRaiseFormat = "加价到 {0}";
        public const string AuctionExit = "退出";
        public const string AuctionAuto = "自动跟价到上限";
        public const string AuctionAutoLabel = "上限（元）";
        public const string AuctionSoldFormat = "成交 {0} → {1}";
        public const string AuctionUnsold = "流拍";
        public const string AuctionPending = "待拍";
        public const string AuctionLive = "竞拍中";
        public const string AuctionCantBid = "资金不足或名单已满 8 人，只能退出";
        public const string LeagueName = "联赛";

        // ---- re-sign
        public const string ResignTitle = "补签：从自由选手池签人（2 年合同）";
        public const string ResignHintFormat = "名单 {0} 人（5–8 人）；空缺分路：{1}";
        public const string ResignNoMissing = "无";
        public const string ResignSign = "签下";
        public const string ResignLineFormat = "{0} · {1} · {2} 岁 · 评分 {3} · 潜力 {4} · 年薪 {5}";
        public const string ResignFinish = "结束转会期";
        public const string ResignBlocked = "名单少于 5 人或有空缺分路，不能结束转会期";
        public const string ResignMissingTag = "补空缺";
        public const string RosterTitle = "我的名单";

        // ---- errors
        public static string XferError(TransferError e)
        {
            switch (e)
            {
                case TransferError.Funds: return "资金不足";
                case TransferError.RosterFull: return "名单已满 8 人";
                case TransferError.RosterGuard: return "挂牌后名单不能少于 5 人，也不能挂分路唯一的主分路首发";
                case TransferError.IncompleteRoster: return ResignBlocked;
                case TransferError.NotAllowed: return "这名选手现在不能这样处理（本年新签 / 已报价 / 已加薪留过 / 流拍锁定）";
                case TransferError.OfferLimit: return "今年的挖角报价次数已用完";
                case TransferError.Unanswered: return XferUnanswered;
                case TransferError.BadYears: return "合同年限只能是 1–3 年";
                default: return "现在不能这样操作";
            }
        }

        public const string SellPick = "选择";
        public const string SellPicked = "已选";
        public const string ListSeparator = "、";
        public const string EmptyLane = "—";
        public const string XferStepFormat = "{0} {1}";
        public const string PotentialShortFormat = "潜力 {0}";

        // ---- champion fatigue (main screen)
        public const string FatigueTitle = "冠军疲劳";
        public const string FatigueFormat = "剩 {0} 段 · 首发压力 +{1}/段 · 媒体直播 {2} 点（已预留）";

        // ---- news
        public const string NewsPoachFormat = "{0} 从 {1} 挖走 {2}（转会费 ¥{3}）";
        public const string NewsAuctionLeagueFormat = "{0} 在拍卖会买下 {1}（¥{2}）";
        public const string NewsAuctionListedFormat = "{0} 从 {1} 买下挂牌选手 {2}（¥{3}）";
        public const string NewsForcedListingFormat = "{0} 被迫挂牌 {1}";
        public const string NewsRetainFormat = "{0} 加薪留住 {1}（拒绝了 {2} 的报价）";
    }
}
