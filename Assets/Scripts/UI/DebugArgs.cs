using System;

namespace EsportsManager.UI
{
    /// <summary>The state the player build should present at launch (debug / screenshot use).</summary>
    public enum DemoState { Main, Card, Assign, Offer, Summary, Lineup, Report, Standings, Name, EventPrep, Regular, Bracket, FinalsQual }

    /// <summary>
    /// Debug launch arguments of the built player, all parsed here:
    /// <list type="bullet">
    /// <item><c>--demo main|card|assign|offer|summary|lineup|standings|name|event-prep|regular|bracket|report|finals-qual</c>
    /// state to present (default main; hyphens are optional, case is ignored).
    /// <c>summary</c>: scripted plan resolved, settlement summary. <c>lineup</c>: main screen, offers declined, tactic set to
    /// Teamfight and the first starter benched (tactic selector, toggles and the incomplete-lineup message visible).
    /// <c>standings</c>: the standings screen (combine with <c>--segments N</c>). <c>name</c>: the new-club naming screen.
    /// EVENT DEMOS (the last of the <c>--segments</c> segments is resolved through the UI, so the event is NOT auto-played;
    /// default segments 3, finals-qual 12; the segment must host an event): <c>event-prep</c> = prep screen of that event;
    /// <c>regular</c> = regular-season result (splits); <c>bracket</c> = event result (playoffs played, through the playoff prep
    /// when the club is seeded); <c>report</c> = the same result screen showing only the detailed series reports;
    /// <c>finals-qual</c> = finals prep with the qualification table (default 12 segments).
    /// Without <c>--demo</c> the game starts at the naming screen; every other demo skips it and keeps the default club name.</item>
    /// <item><c>--segments N</c> auto-play N scripted segments before presenting (events of earlier segments are auto-played
    /// with the scripted lineup - debug only, never in normal play).</item>
    /// <item><c>--seed N</c> override the fixed demo seed.</item>
    /// <item><c>--safe-area-top N</c> simulate a notch inset of N screen pixels.</item>
    /// <item><c>--force-offer</c> pick the first seed from --seed upwards whose first segment has a business offer.</item>
    /// </list>
    /// Screenshot arguments (<c>--scene --screenshot --after</c>) belong to ScreenshotOnArg.
    /// </summary>
    public sealed class DebugArgs
    {
        /// <summary>Demo state.</summary>
        public DemoState Demo = DemoState.Main;
        /// <summary>Scripted segments to auto-play first.</summary>
        public int Segments;
        /// <summary>Seed override, or null.</summary>
        public int? Seed;
        /// <summary>Search for a seed that has a first-segment offer.</summary>
        public bool ForceOffer;
        /// <summary>True when --demo was on the command line (any value).</summary>
        public bool DemoGiven;

        /// <summary>True when the arguments ask for a debug / screenshot run (--demo or --screenshot).</summary>
        public static bool IsDebugRun(string[] args)
        {
            foreach (var a in args) if (a == "--demo" || a == "--screenshot") return true;
            return false;
        }

        /// <summary>--safe-area-top N: simulates a top inset (notch) of N screen pixels.</summary>
        public float SafeAreaTop;

        /// <summary>--clicks N: performs N scripted train clicks through GameUi (layout-stability check).</summary>
        public int Clicks;

        /// <summary>True for the demos that resolve the last segment through the UI and walk into an event.</summary>
        public bool IsEventDemo => Demo == DemoState.EventPrep || Demo == DemoState.Regular || Demo == DemoState.Bracket
            || Demo == DemoState.Report || Demo == DemoState.FinalsQual;

        /// <summary>--segments, or the event demo default (3 = spring, finals-qual 12) when none was given.</summary>
        public int EffectiveSegments => Segments > 0 || !IsEventDemo ? Segments : Demo == DemoState.FinalsQual ? 12 : 3;

        /// <summary>Parses the process command line.</summary>
        public static DebugArgs Parse() => Parse(Environment.GetCommandLineArgs());

        /// <summary>Parses an argument vector (testable).</summary>
        public static DebugArgs Parse(string[] args)
        {
            var d = new DebugArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                string next = i + 1 < args.Length ? args[i + 1] : null;
                if (a == "--demo") { d.DemoGiven = true; if (next != null && Enum.TryParse(next.Replace("-", ""), true, out DemoState s)) d.Demo = s; }
                else if (a == "--segments" && int.TryParse(next, out int n)) d.Segments = Math.Max(0, n);
                else if (a == "--seed" && int.TryParse(next, out int seed)) d.Seed = seed;
                else if (a == "--clicks" && int.TryParse(next, out int c)) d.Clicks = Math.Max(0, c);
                else if (a == "--safe-area-top" && float.TryParse(next, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float sat)) d.SafeAreaTop = Math.Max(0, sat);
                else if (a == "--force-offer") d.ForceOffer = true;
            }
            return d;
        }
    }
}
