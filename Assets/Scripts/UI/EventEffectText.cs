using System;
using System.Collections.Generic;
using System.Globalization;
using EsportsManager.Events;

namespace EsportsManager.UI
{
    /// <summary>One effect as plain Chinese plus whether it is good (+1), bad (-1) or neutral (0) for the club.</summary>
    public readonly struct EffectPhrase
    {
        /// <summary>Sentence, e.g. "心情 +12" / "资金 −4.2 万" / "潜力上升".</summary>
        public readonly string Text;
        /// <summary>+1 good, -1 bad, 0 neutral (drives the colour).</summary>
        public readonly int Sentiment;

        /// <summary>Creates a phrase.</summary>
        public EffectPhrase(string text, int sentiment) { Text = text; Sentiment = sentiment; }
    }

    /// <summary>
    /// Story 006: pure text mapping of event effects (no UI elements, unit-tested). Every <see cref="EffectType"/> has a
    /// sentence. Potential effects NEVER show a number (the true potential stays hidden): only "潜力上升" / "潜力下降".
    /// Pending choice options show the pre-rolled <see cref="EventEffectResult.Value"/>; resolved occurrences show the
    /// actual change per affected player (or per pair for familiarity) for stat effects, the rolled value for modifiers.
    /// </summary>
    public static class EventEffectText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Describes one effect. <paramref name="pairSubject"/>: the event is about a pair of players.</summary>
        public static EffectPhrase Describe(EventEffectResult e, bool resolved, bool pairSubject = false)
        {
            float v = Amount(e, resolved);
            string who = Prefix(e, pairSubject);
            switch (e.Type)
            {
                case EffectType.Mood: return new EffectPhrase(UiStrings.F(UiStrings.EffMood, who, Signed(v)), Sign(v));
                case EffectType.Stress: return new EffectPhrase(UiStrings.F(UiStrings.EffStress, who, Signed(v)), -Sign(v));
                case EffectType.PlayerPopularity: return new EffectPhrase(UiStrings.F(UiStrings.EffPlayerPopularity, who, Signed(v)), Sign(v));
                case EffectType.ClubPopularity: return new EffectPhrase(UiStrings.F(UiStrings.EffClubPopularity, Signed(v)), Sign(v));
                case EffectType.Familiarity: return new EffectPhrase(UiStrings.F(UiStrings.EffFamiliarity, FamiliarityPrefix(e, pairSubject), Signed(v)), Sign(v));
                case EffectType.Potential:
                    // Direction from the rolled value only: never a number, never "unchanged" (that would hint at the cap).
                    return e.Value >= 0f ? new EffectPhrase(UiStrings.EffPotentialUp, 1) : new EffectPhrase(UiStrings.EffPotentialDown, -1);
                case EffectType.Funds: return new EffectPhrase(UiStrings.F(UiStrings.EffFunds, Money((long)Math.Round(v))), Sign(v));
                case EffectType.TrainMultiplier: return Multiplier(UiStrings.EffTrainMultiplier, e.Value);
                case EffectType.TrainAdd: return new EffectPhrase(UiStrings.F(UiStrings.EffTrainAdd, Signed(v)), Sign(v));
                case EffectType.StreamMultiplier: return Multiplier(UiStrings.EffStreamMultiplier, e.Value);
                case EffectType.StreamAdd: return new EffectPhrase(UiStrings.F(UiStrings.EffStreamAdd, Money((long)Math.Round(v))), Sign(v));
                case EffectType.RestMultiplier: return Multiplier(UiStrings.EffRestMultiplier, e.Value);
                case EffectType.BusinessMultiplier: return Multiplier(UiStrings.EffBusinessMultiplier, e.Value);
                case EffectType.SponsorChanceAdd:
                    return new EffectPhrase(UiStrings.F(UiStrings.EffSponsorChance, SignedPercent(v)), Sign(v));
                case EffectType.VoidAction: return new EffectPhrase(UiStrings.EffVoidAction, -1);
                case EffectType.OnLeave: return new EffectPhrase(UiStrings.EffOnLeave, 0);
                default: return new EffectPhrase(UiStrings.EffNone, 0);
            }
        }

        /// <summary>All effects of a list, in order (an empty list gives one neutral "没有其他影响").</summary>
        public static List<EffectPhrase> DescribeAll(IReadOnlyList<EventEffectResult> effects, bool resolved, bool pairSubject = false)
        {
            var list = new List<EffectPhrase>();
            if (effects != null) foreach (var e in effects) list.Add(Describe(e, resolved, pairSubject));
            if (list.Count == 0) list.Add(new EffectPhrase(UiStrings.EffNone, 0));
            return list;
        }

        /// <summary>The phrases joined with "、" (logs and tests).</summary>
        public static string Join(IReadOnlyList<EventEffectResult> effects, bool resolved, bool pairSubject = false)
        {
            var parts = new List<string>();
            foreach (var p in DescribeAll(effects, resolved, pairSubject)) parts.Add(p.Text);
            return string.Join(UiStrings.EffJoin, parts);
        }

        /// <summary>Fills the {player} / {partner} placeholders of an event text (null names become empty).</summary>
        public static string Fill(string template, string player, string partner)
        {
            if (template == null) return "";
            return template.Replace("{player}", player ?? "").Replace("{partner}", partner ?? "");
        }

        /// <summary>Signed money in plain Chinese: "+8,000 元", "−4.2 万" (>= 10,000 yuan in 万, up to two decimals).</summary>
        public static string Money(long yuan)
        {
            string sign = yuan > 0 ? UiStrings.Plus : yuan < 0 ? UiStrings.Minus : "";
            long abs = Math.Abs(yuan);
            string body = abs >= 10000
                ? UiStrings.F(UiStrings.MoneyWanFormat, (abs / 10000.0).ToString("0.##", Inv))
                : UiStrings.F(UiStrings.MoneyYuanFormat, abs.ToString("N0", Inv));
            return sign + body;
        }

        /// <summary>Signed number with at most one decimal and a real minus sign: "+12", "−10", "+2.5", "0".</summary>
        public static string Signed(float v)
        {
            double r = Math.Round(v, 1, MidpointRounding.AwayFromZero);
            if (r == 0) return "0";
            return (r > 0 ? UiStrings.Plus : UiStrings.Minus) + Math.Abs(r).ToString("0.#", Inv);
        }

        private static string SignedPercent(float chance)
        {
            int pct = (int)Math.Round(chance * 100.0, MidpointRounding.AwayFromZero);
            if (pct == 0) return "0%";
            return (pct > 0 ? UiStrings.Plus : UiStrings.Minus) + Math.Abs(pct).ToString(Inv) + "%";
        }

        private static EffectPhrase Multiplier(string format, float m)
            => new EffectPhrase(UiStrings.F(format, Math.Round(m, 2).ToString("0.##", Inv)), m > 1.0001f ? 1 : m < 0.9999f ? -1 : 0);

        private static int Sign(float v) => Math.Abs(v) < 0.05f ? 0 : v > 0 ? 1 : -1;

        // Resolved stat effects carry the SUMMED actual change; show it per player (per pair for familiarity).
        private static float Amount(EventEffectResult e, bool resolved)
        {
            if (!resolved) return e.Value;
            int n = e.PlayerIds != null ? e.PlayerIds.Count : 0;
            switch (e.Type)
            {
                case EffectType.Mood: case EffectType.Stress: case EffectType.PlayerPopularity:
                    return n > 1 ? e.Applied / n : e.Applied;
                case EffectType.Familiarity:
                    int pairs = PairCount(e.Target, n);
                    return pairs > 1 ? e.Applied / pairs : e.Applied;
                case EffectType.ClubPopularity: case EffectType.Funds: case EffectType.SponsorChanceAdd:
                    return e.Applied;
                default: return e.Value;
            }
        }

        private static int PairCount(EffectTarget t, int players)
        {
            if (players < 2) return players == 0 ? 0 : 1;
            switch (t)
            {
                case EffectTarget.SubjectWithStarters: return players - 1;
                case EffectTarget.Subject: return 1;
                default: return players * (players - 1) / 2;
            }
        }

        private static string Prefix(EventEffectResult e, bool pairSubject)
        {
            switch (e.Target)
            {
                case EffectTarget.Roster: return UiStrings.EffPrefixRoster;
                case EffectTarget.Starters: case EffectTarget.SubjectWithStarters: return UiStrings.EffPrefixStarters;
                case EffectTarget.Subject: return pairSubject ? UiStrings.EffPrefixPair : "";
                default: return "";
            }
        }

        private static string FamiliarityPrefix(EventEffectResult e, bool pairSubject)
        {
            switch (e.Target)
            {
                case EffectTarget.SubjectWithStarters: return UiStrings.EffPrefixWithStarters;
                case EffectTarget.Starters: case EffectTarget.Roster: return UiStrings.EffPrefixStarters;
                default: return pairSubject ? UiStrings.EffPrefixPair : "";
            }
        }
    }
}
