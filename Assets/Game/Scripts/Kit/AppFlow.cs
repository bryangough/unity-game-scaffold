using System.Collections.Generic;

namespace GameScaffold
{
    /// <summary>What the Back button (Android Back, Esc, the pad's B) should do in the current situation.</summary>
    public enum BackAction
    {
        None,
        /// <summary>The top overlay was closed (already done by <see cref="AppFlow.Back"/>).</summary>
        CloseOverlay,
        /// <summary>Go to the screen named by the rule's target.</summary>
        GoTo,
        /// <summary>The overlay named by the rule's target was opened (already pushed).</summary>
        OpenOverlay,
        /// <summary>Something the game decides, named by the rule's target ("quit", "continue").</summary>
        Custom,
    }

    /// <summary>
    /// App-level navigation rules, plain C# and tested: which screen is on, which overlays are stacked over it, and
    /// what Back does. A controller applies the results to the scene (show a screen, open a panel). Switching screens
    /// closes every overlay; pushing the overlay already on top does nothing; Back pops an overlay first. Rules per
    /// screen come from <see cref="OnBack"/> ("Levels" → GoTo "Title"; "Play" → OpenOverlay "Pause"; "Title" →
    /// OpenOverlay "QuitConfirm"). From the duel game, where Android Back had to behave on every screen.
    /// </summary>
    public sealed class AppFlow
    {
        private readonly List<string> overlays = new List<string>();
        private readonly Dictionary<string, (BackAction action, string target)> rules = new Dictionary<string, (BackAction, string)>();

        public string State { get; private set; } = "";
        public IReadOnlyList<string> Overlays => overlays;
        public bool HasOverlay => overlays.Count > 0;
        public string Top => overlays.Count > 0 ? overlays[overlays.Count - 1] : null;

        /// <summary>What Back does on <paramref name="state"/> when no overlay is open.</summary>
        public AppFlow OnBack(string state, BackAction action, string target = null)
        {
            rules[state] = (action, target);
            return this;
        }

        /// <summary>Switching screens closes any overlays.</summary>
        public void Enter(string state)
        {
            State = state;
            overlays.Clear();
        }

        public void Push(string overlay)
        {
            if (Top == overlay) return;
            overlays.Add(overlay);
        }

        public bool Pop()
        {
            if (overlays.Count == 0) return false;
            overlays.RemoveAt(overlays.Count - 1);
            return true;
        }

        public bool IsOpen(string overlay) => overlays.Contains(overlay);

        /// <summary>Decides (and applies, for overlay changes) what Back does now; <paramref name="target"/> is the rule's.</summary>
        public BackAction Back(out string target)
        {
            target = null;
            if (overlays.Count > 0)
            {
                Pop();
                return BackAction.CloseOverlay;
            }
            if (!rules.TryGetValue(State, out (BackAction action, string target) rule)) return BackAction.None;
            target = rule.target;
            if (rule.action == BackAction.OpenOverlay && !string.IsNullOrEmpty(rule.target)) Push(rule.target);
            return rule.action;
        }

        public BackAction Back() => Back(out _);
    }
}
