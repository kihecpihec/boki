using System;
using System.Collections.Generic;

namespace Boki.Progression
{
    public enum UpgradeKind { RaveOverdrive, TurboSkiro, BiggerBattery, DoubleTrouble, PanicMode, EmergencySnack, BatteryCharge }

    // Story §5: eligibility, additive caps, and queued upgrade selections.
    public sealed class RunProgression
    {
        private static readonly int[] thresholds = { 3, 7, 12, 18, 25, 33 };
        private readonly int[] counts = new int[7];
        public int XP { get; set; }
        public int Selections { get; private set; }
        public bool MaximumLevel => Selections == thresholds.Length;
        public bool NeedsSelection => !MaximumLevel && XP >= thresholds[Selections];
        public float DamageMultiplier => 1f + counts[0] * 0.3f;
        public float RidingBonus => counts[1] * 0.2f;
        public float BatteryCapacity => 100f + counts[2] * 40f;
        public bool DoubleTrouble => counts[3] > 0;
        public bool PanicMode => counts[4] > 0;
        public int Count(UpgradeKind kind) => counts[(int)kind];

        public bool CollectXP()
        {
            if (MaximumLevel) return false;
            XP++;
            return true;
        }

        public bool Eligible(UpgradeKind kind, bool injured, bool batteryNotFull)
        {
            switch (kind)
            {
                case UpgradeKind.RaveOverdrive: return counts[0] < 3;
                case UpgradeKind.TurboSkiro: return counts[1] < 2;
                case UpgradeKind.BiggerBattery: return counts[2] < 2;
                case UpgradeKind.DoubleTrouble: return counts[3] < 1;
                case UpgradeKind.PanicMode: return counts[4] < 1;
                case UpgradeKind.EmergencySnack: return injured;
                case UpgradeKind.BatteryCharge: return batteryNotFull;
                default: return false;
            }
        }

        public UpgradeKind[] Offer(Random random, bool injured, bool batteryNotFull)
        {
            if (!NeedsSelection) return Array.Empty<UpgradeKind>();
            var pool = new List<UpgradeKind>();
            for (int i = 0; i < counts.Length; i++)
                if (Eligible((UpgradeKind)i, injured, batteryNotFull)) pool.Add((UpgradeKind)i);
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                var value = pool[i]; pool[i] = pool[j]; pool[j] = value;
            }
            return pool.GetRange(0, Math.Min(3, pool.Count)).ToArray();
        }

        public bool Choose(UpgradeKind kind, bool injured, bool batteryNotFull)
        {
            if (!NeedsSelection || !Eligible(kind, injured, batteryNotFull)) return false;
            counts[(int)kind]++;
            Selections++;
            return true;
        }

        public void Reset()
        {
            XP = Selections = 0;
            Array.Clear(counts, 0, counts.Length);
        }
    }
}
