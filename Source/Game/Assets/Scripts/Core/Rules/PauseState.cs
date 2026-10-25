using System;

namespace Boki.Core
{
    [Flags]
    public enum PauseReason { Menu = 1, Upgrade = 2, Loading = 4, ScriptedSequence = 8 }

    public sealed class PauseState
    {
        private PauseReason reasons;
        public bool IsPaused => reasons != 0;
        public event Action Changed;
        public bool Contains(PauseReason reason) => (reasons & reason) != 0;
        public void Set(PauseReason reason, bool active)
        {
            PauseReason next = active ? reasons | reason : reasons & ~reason;
            if (next == reasons) return;
            reasons = next;
            Changed?.Invoke();
        }
        public void Reset()
        {
            reasons = 0;
            Changed?.Invoke();
        }
    }
}
