using System;
using System.Collections.Generic;

namespace EsportsManager.Players
{
    /// <summary>
    /// Whitelist of personality effect keys (numbers.md 3.2 v2). <see cref="PlayerConfig.Validate"/>
    /// rejects any effect-table entry whose key is not listed here, so a typo in the config cannot
    /// silently turn an effect off. Keys are identifiers, not tuning numbers.
    /// Story 008 code reads <see cref="TrainingGain"/>, <see cref="AskPrice"/>, <see cref="OffLaneDiscount"/>,
    /// <see cref="NegativeCompatImpact"/> and <see cref="FamiliarityGain"/>; the rest are for later stories.
    /// </summary>
    /// <example><code>float m = player.GetTraitMultiplier(PlayerEffectKeys.TrainingGain);</code></example>
    public static class PlayerEffectKeys
    {
        /// <summary>Training gain (numbers.md 4.1). 单纯/认真, 勤奋, 懒惰, 聪明.</summary>
        public const string TrainingGain = "training_gain";
        /// <summary>Asking salary at signing/renewal (4.4). 自私, 老实.</summary>
        public const string AskPrice = "ask_price";
        /// <summary>Asking salary at renewal only (Story 005). 义气.</summary>
        public const string RenewalAskPrice = "renewal_ask_price";
        /// <summary>Multiplier on the off-lane discount. 聪明.</summary>
        public const string OffLaneDiscount = "off_lane_discount";
        /// <summary>Multiplier on negative compatibility terms in H(i,j) (3.3). 圆滑.</summary>
        public const string NegativeCompatImpact = "negative_compat_impact";
        /// <summary>Pair familiarity growth (4.8, Story 002). 温和.</summary>
        public const string FamiliarityGain = "familiarity_gain";
        /// <summary>Mood drop while benched. 自私.</summary>
        public const string BenchMoodDrop = "bench_mood_drop";
        /// <summary>Fans gained by streaming. 内向/沉稳, 活泼.</summary>
        public const string StreamFans = "stream_fans";
        /// <summary>Stress increase. 内向/沉稳, 懒惰.</summary>
        public const string StressGain = "stress_gain";
        /// <summary>Business offer trigger rate. 活泼, 单纯/认真.</summary>
        public const string BusinessRate = "business_rate";
        /// <summary>Business offer amount. 圆滑.</summary>
        public const string BusinessAmount = "business_amount";
        /// <summary>In-match command ability. 强势.</summary>
        public const string Command = "command";
        /// <summary>Negative mood changes. 乐观, 悲观.</summary>
        public const string NegativeMoodChange = "negative_mood_change";
        /// <summary>Effect of stress on performance variance. 坚毅.</summary>
        public const string StressVarianceImpact = "stress_variance_impact";
        /// <summary>Mood drop after a loss. 脆弱/依赖.</summary>
        public const string LossMoodDrop = "loss_mood_drop";
        /// <summary>Mood gained from a win. 理想.</summary>
        public const string WinMoodGain = "win_mood_gain";
        /// <summary>Weight of salary satisfaction in mood. 理想, 实际.</summary>
        public const string SalarySatisfactionWeight = "salary_satisfaction_weight";
        /// <summary>Performance in deciding games. 果断, 犹豫.</summary>
        public const string DeciderPerformance = "decider_performance";
        /// <summary>Magnitude of all mood changes. 老实.</summary>
        public const string MoodChangeMagnitude = "mood_change_magnitude";

        /// <summary>Every legal effect key.</summary>
        public static readonly IReadOnlyList<string> All = Array.AsReadOnly(new[]
        {
            TrainingGain, AskPrice, RenewalAskPrice, OffLaneDiscount, NegativeCompatImpact, FamiliarityGain,
            BenchMoodDrop, StreamFans, StressGain, BusinessRate, BusinessAmount, Command, NegativeMoodChange,
            StressVarianceImpact, LossMoodDrop, WinMoodGain, SalarySatisfactionWeight, DeciderPerformance,
            MoodChangeMagnitude,
        });

        /// <summary>True when <paramref name="key"/> is in the whitelist.</summary>
        public static bool IsKnown(string key)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i] == key) return true;
            return false;
        }
    }
}
