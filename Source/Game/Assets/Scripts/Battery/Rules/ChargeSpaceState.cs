using System;

namespace Boki.Battery
{
    public sealed class ChargeSpaceState
    {
        private readonly float[] remaining;
        public int Count => remaining.Length;
        public bool Occupied { get; set; }

        public ChargeSpaceState(int count)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            remaining = new float[count];
        }

        public float Remaining(int slot) => remaining[slot];
        public bool Available(int slot) => remaining[slot] <= 0f;

        public bool TryCollect(int slot, LightBatteryState battery, bool active)
        {
            if (!active || !Occupied || !Available(slot) || !battery.TryRecharge()) return false;
            remaining[slot] = 20f;
            return true;
        }

        internal void Tick(float seconds)
        {
            if (!Occupied) return;
            for (int i = 0; i < remaining.Length; i++) remaining[i] = Math.Max(0f, remaining[i] - seconds);
        }
    }
}
