using System.Collections.Generic;
using EsportsManager.Matches;
using UnityEngine;
using UnityEngine.UIElements;

namespace EsportsManager.UI
{
    /// <summary>
    /// Detailed series report component (Story 004): title with stage and opponent, score, both tactics, game-by-game results and
    /// the 2-3 reasons as plain sentences (<see cref="ReasonText"/>), then the cycle / early-end and even notes and the tactic
    /// advice line (design/gdd/match.md "战报界面", Story 011). Used by the event result screen for the player's playoff /
    /// finals series.
    /// </summary>
    public static class ReportScreen
    {
        /// <summary>Lane id -> lane display name of the current lineup config (for reason subjects).</summary>
        public static Dictionary<string, string> LaneNames(GameUi ui)
        {
            var lanes = new Dictionary<string, string>();
            foreach (var slot in ui.Session.GetLineup().Slots) lanes[slot.LaneId] = slot.LaneName;
            return lanes;
        }

        /// <summary>The plain sentence of one reason of a player series.</summary>
        public static string Reason(GameUi ui, MatchReason reason, MatchResult res, Dictionary<string, string> lanes)
            => ReasonText.For(reason, Subject(ui, reason, res, lanes), res.SideA.Tactic, res.SideB.Tactic);

        /// <summary>One detailed series panel (player club = side A).</summary>
        public static VisualElement SeriesPanel(GameUi ui, EventSeries series, string opponentName, int index, Dictionary<string, string> lanes)
        {
            var res = series.Result;
            bool won = res.WinnerIsA;
            var panel = UiKit.Block(UiKit.Panel, 6);
            panel.name = "series-" + index;
            panel.style.width = 820; panel.style.alignSelf = Align.Center; UiKit.Pad(panel, 16); panel.style.marginTop = 8;
            panel.style.borderLeftWidth = 5; panel.style.borderLeftColor = won ? UiKit.Good : UiKit.Bad;

            var head = UiKit.Row(40);
            var on = UiKit.Lbl(string.Format(UiStrings.SeriesTitleFormat, EventTexts.StageName(series.Stage), opponentName), 22, UiKit.Text, true);
            UiKit.Ellipsis(on);
            head.Add(on);
            head.Add(UiKit.Spacer());
            head.Add(UiKit.Lbl(string.Format(UiStrings.SeriesScoreFormat, res.ScoreA, res.ScoreB), 30, won ? UiKit.Good : UiKit.Bad, true));
            var wl = UiKit.Lbl(won ? UiStrings.SeriesWon : UiStrings.SeriesLost, 22, won ? UiKit.Good : UiKit.Bad, true);
            wl.style.marginLeft = 10; head.Add(wl);
            panel.Add(head);

            var tac = UiKit.Lbl(string.Format(UiStrings.SeriesTacticsFormat, UiStrings.TacticName(res.SideA.Tactic), UiStrings.TacticName(res.SideB.Tactic)), 15, UiKit.TextDim);
            tac.style.marginTop = 2; panel.Add(tac);

            var games = UiKit.Row(30);
            games.style.marginTop = 6;
            for (int i = 0; i < res.Games.Count; i++)
            {
                var g = res.Games[i];
                string t = string.Format(UiStrings.GameLineFormat, i + 1, g.WinnerIsA ? UiStrings.GameWon : UiStrings.GameLost)
                    + (g.WasDecider ? UiStrings.DeciderSuffix : "");
                var gl = UiKit.Lbl(t, 15, g.WinnerIsA ? UiKit.Good : UiKit.Bad, true);
                gl.style.marginRight = 24; games.Add(gl);
            }
            panel.Add(games);

            var rt = UiKit.Lbl(UiStrings.ReasonsTitle, 14, UiKit.Accent, true);
            rt.style.marginTop = 10; panel.Add(rt);
            foreach (var reason in series.Report.Reasons)
            {
                var text = reason.IsLuckGame ? Reason(ui, reason, res, lanes) : ReasonText.WithValue(Reason(ui, reason, res, lanes), reason.Value);
                var l = UiKit.Lbl(UiStrings.ReasonBullet + text, 16, reason.Value >= 0f ? UiKit.Text : UiKit.Warn);
                l.name = "reason";
                l.style.whiteSpace = WhiteSpace.Normal; l.style.marginTop = 4;
                panel.Add(l);
            }
            var notes = ReasonText.Notes(series.Report);
            if (notes.Count > 0)
            {
                var nt = UiKit.Lbl(UiStrings.NotesTitle, 14, UiKit.Accent, true);
                nt.style.marginTop = 10; panel.Add(nt);
                foreach (var n in notes)
                {
                    var l = UiKit.Lbl(UiStrings.ReasonBullet + n, 15, UiKit.TextDim);
                    l.name = "report-note";
                    l.style.whiteSpace = WhiteSpace.Normal; l.style.marginTop = 3;
                    panel.Add(l);
                }
            }
            return panel;
        }

        private static ReasonSubject Subject(GameUi ui, MatchReason reason, MatchResult res, Dictionary<string, string> lanes)
        {
            if (!reason.HasSubject) return default;
            // Display lookup only: the report is built with the player club as side A.
            foreach (var p in res.SideA.Participants)
            {
                if (p.PlayerId != reason.SubjectPlayerId) continue;
                lanes.TryGetValue(p.LaneId, out var lane);
                return new ReasonSubject(ui.Session.PlayerName(p.PlayerId), lane ?? p.LaneId);
            }
            return default;
        }
    }
}
