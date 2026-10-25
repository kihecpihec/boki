using System;

namespace Boki.Combat
{
    // Counts laser hits in a row; the adapter resets it whenever the player is hurt.
    public sealed class ComboCounter
    {
        public int Count { get; private set; }
        public event Action Changed;

        public void RegisterHit()
        {
            if (Count < int.MaxValue) Count++;
            Changed?.Invoke();
        }

        public void Reset()
        {
            if (Count == 0) return;
            Count = 0;
            Changed?.Invoke();
        }
    }
}
