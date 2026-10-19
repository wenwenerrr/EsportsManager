using System;

namespace EsportsManager.UI
{
    /// <summary>The state the player build should present at launch (debug / screenshot use).</summary>
    public enum DemoState { Main, Card, Assign, Offer, Summary }

    /// <summary>
    /// Debug launch arguments of the built player, all parsed here:
    /// <list type="bullet">
    /// <item><c>--demo main|card|assign|offer|summary</c> state to present (default main).</item>
    /// <item><c>--segments N</c> auto-play N scripted segments before presenting.</item>
    /// <item><c>--seed N</c> override the fixed demo seed.</item>
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
                if (a == "--demo" && next != null && Enum.TryParse(next, true, out DemoState s)) d.Demo = s;
                else if (a == "--segments" && int.TryParse(next, out int n)) d.Segments = Math.Max(0, n);
                else if (a == "--seed" && int.TryParse(next, out int seed)) d.Seed = seed;
                else if (a == "--force-offer") d.ForceOffer = true;
            }
            return d;
        }
    }
}
