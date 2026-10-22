using System;

namespace Boki.Combat
{
    // Pure damage rules shared by player and enemy components.
    public sealed class HealthState
    {
        public int Maximum { get; }
        public int Current { get; private set; }
        public bool IsAlive => Current > 0;
        public event Action Changed;
        public event Action Died;

        public HealthState(int maximum)
        {
            if (maximum <= 0) throw new ArgumentOutOfRangeException(nameof(maximum));
            Maximum = Current = maximum;
        }

        public void TakeDamage(int damage)
        {
            if (damage <= 0 || !IsAlive) return;
            bool lethal = damage >= Current;
            Current = lethal ? 0 : Current - damage;
            Changed?.Invoke();
            if (lethal) Died?.Invoke();
        }
    }
}
