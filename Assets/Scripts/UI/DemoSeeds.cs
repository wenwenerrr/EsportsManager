using System;
using EsportsManager.Events;
using EsportsManager.Session;

namespace EsportsManager.UI
{
    /// <summary>
    /// Story 006 debug demos: deterministic seed search. The constants are the first seeds (searched upwards from
    /// <see cref="GameSession.DefaultSeed"/>) that satisfy the predicates with the shipped configs; the EditMode tests pin them,
    /// and if a config change breaks one the bootstrap keeps searching upwards (logging the seed it used).
    /// </summary>
    public static class DemoSeeds
    {
        /// <summary>First segment has a choice event with a leave option (--demo choice).</summary>
        public const int ChoiceDemoSeed = 20260153;
        /// <summary>The scripted first segment yields >= 3 events incl. a bad money event and a choice result (--demo events).</summary>
        public const int EventsDemoSeed = 20260120;
        /// <summary>Seeds tried before giving up (the start seed is then used as is).</summary>
        public const int MaxTries = 3000;

        /// <summary>First seed from <paramref name="start"/> upwards whose fresh session satisfies <paramref name="ok"/>, or -1.</summary>
        public static int Find(Func<int, GameSession> make, int start, Func<GameSession, bool> ok, int maxTries = MaxTries)
        {
            for (int i = 0; i < maxTries; i++)
            {
                int seed = start + i;
                if (ok(make(seed))) return seed;
            }
            return -1;
        }

        /// <summary>Choice demo seed: prefers a choice with a leave option, else any choice, else <paramref name="start"/>.</summary>
        public static int FindChoiceSeed(Func<int, GameSession> make, int start)
        {
            int s = Find(make, start, HasLeaveChoice);
            if (s < 0) s = Find(make, start, HasChoice);
            UnityEngine.Debug.Log("DemoSeeds: choice demo seed " + (s < 0 ? start : s));
            return s < 0 ? start : s;
        }

        /// <summary>Events demo seed (see <see cref="HasEventCards"/>), else <paramref name="start"/>.</summary>
        public static int FindEventsSeed(Func<int, GameSession> make, int start)
        {
            int s = Find(make, start, HasEventCards);
            UnityEngine.Debug.Log("DemoSeeds: events demo seed " + (s < 0 ? start : s));
            return s < 0 ? start : s;
        }

        /// <summary>The open segment has a choice event.</summary>
        public static bool HasChoice(GameSession s) => s.PendingChoices.Count > 0;

        /// <summary>The open segment has a choice event one of whose options puts a player on leave.</summary>
        public static bool HasLeaveChoice(GameSession s)
        {
            foreach (var c in s.PendingChoices) foreach (var o in c.Options) if (o.PutsOnLeave) return true;
            return false;
        }

        /// <summary>
        /// MUTATES the session: applies the scripted plan and resolves the segment, then checks the summary has at least 3
        /// events, one bad event that cost money and one choice result.
        /// </summary>
        public static bool HasEventCards(GameSession s)
        {
            s.ApplyScriptedPlan();
            if (!s.TryValidate(out _)) return false;
            var sum = s.Resolve().Summary;
            if (sum.Events.Count < 3) return false;
            bool badMoney = false, choice = false;
            foreach (var e in sum.Events)
            {
                if (e.Tone == EventTone.Bad && e.FundsDelta < 0) badMoney = true;
                if (e.Kind == EventKind.Choice) choice = true;
            }
            return badMoney && choice;
        }
    }
}
