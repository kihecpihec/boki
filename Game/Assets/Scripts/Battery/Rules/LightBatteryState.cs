using System;
using System.Collections.Generic;

namespace Boki.Battery
{
    // Gameplay §5, Battery Charges and Action Feedback; Story §4.
    public sealed class LightBatteryState
    {
        private readonly Dictionary<string, ChargeSpaceState> spaces = new Dictionary<string, ChargeSpaceState>();
        private float current = 100f;
        public float Capacity { get; private set; } = 100f;
        public float Current
        {
            get => current;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
                current = Math.Max(0f, Math.Min(Capacity, value));
            }
        }
        public float EnemySpeedMultiplier => Current > 0f ? 1f : 1.3f;

        public void Tick(float seconds, bool active)
        {
            if (!active || !ValidSeconds(seconds)) return;
            Current = Math.Max(0f, Current - 1.5f * seconds);
            foreach (ChargeSpaceState space in spaces.Values) space.Tick(seconds);
        }

        public void SetCapacity(float capacity)
        {
            if (capacity <= 0f || float.IsNaN(capacity) || float.IsInfinity(capacity))
                throw new ArgumentOutOfRangeException(nameof(capacity));
            float fraction = Current / Capacity;
            Capacity = capacity;
            Current = fraction * capacity;
        }

        public bool TryRecharge(float amount = 25f)
        {
            if (amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount) || Current >= Capacity) return false;
            Current = Math.Min(Capacity, Current + amount);
            return true;
        }

        public ChargeSpaceState GetSpace(string id, int chargeCount)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A stable combat-space ID is required.", nameof(id));
            if (!spaces.TryGetValue(id, out ChargeSpaceState space))
            {
                space = new ChargeSpaceState(chargeCount);
                spaces.Add(id, space);
            }
            else if (space.Count != chargeCount) throw new InvalidOperationException("Combat-space charge count changed within a run.");
            return space;
        }

        public void Reset()
        {
            Capacity = current = 100f;
            spaces.Clear();
        }

        internal static bool ValidSeconds(float seconds) => seconds > 0f && !float.IsNaN(seconds) && !float.IsInfinity(seconds);
    }
}
