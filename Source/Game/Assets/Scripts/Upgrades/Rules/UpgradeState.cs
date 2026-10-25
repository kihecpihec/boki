using System;
using System.Collections.Generic;
using Boki.Battery;
using Boki.Combat;

namespace Boki.Upgrades
{
    public enum UpgradeId { RaveOverdrive, TurboSkiro, BiggerBattery, DoubleTrouble, PanicMode, EmergencySnack, BatteryCharge }

    // Story §5 owns progression, additive bonuses, caps and recovery eligibility.
    public sealed class UpgradeState
    {
        private static readonly int[] thresholds = { 8, 18, 30, 45, 65, 90 };
        private readonly int[] stacks = new int[7];
        private readonly UpgradeId[] offers = new UpgradeId[3];
        private readonly List<UpgradeId> accepted = new List<UpgradeId>();
        private readonly IReadOnlyList<UpgradeId> acceptedView;
        private readonly HealthState health;
        private readonly LightBatteryState battery;
        private bool hasOffer;
        public int XP { get; private set; }
        public int Selections => accepted.Count;
        public bool IsMaxLevel => Selections == thresholds.Length;
        public int PreviousThreshold => Selections == 0 ? 0 : thresholds[Selections - 1];
        public int NextThreshold => IsMaxLevel ? 0 : thresholds[Selections];
        public bool HasPendingSelection => !IsMaxLevel && XP >= NextThreshold;
        public bool HasOffer => hasOffer;
        public int OfferCount { get; private set; }
        public int OfferVersion { get; private set; }
        public IReadOnlyList<UpgradeId> Accepted => acceptedView;
        public float DamageMultiplier => 1f + 0.3f * Count(UpgradeId.RaveOverdrive);
        public float MovementBonus => 0.2f * Count(UpgradeId.TurboSkiro);
        public bool HasDoubleTrouble => Count(UpgradeId.DoubleTrouble) != 0;
        public bool HasPanicMode => Count(UpgradeId.PanicMode) != 0;
        public event Action Changed;

        public UpgradeState(HealthState health, LightBatteryState battery)
        {
            this.health = health ?? throw new ArgumentNullException(nameof(health));
            this.battery = battery ?? throw new ArgumentNullException(nameof(battery));
            acceptedView = accepted.AsReadOnly();
        }

        public int Count(UpgradeId id) => Valid(id) ? stacks[(int)id] : 0;
        private static bool Valid(UpgradeId id) => id >= UpgradeId.RaveOverdrive && id <= UpgradeId.BatteryCharge;
        public static int Cap(UpgradeId id)
        {
            switch (id)
            {
                case UpgradeId.RaveOverdrive: return 3;
                case UpgradeId.TurboSkiro:
                case UpgradeId.BiggerBattery: return 2;
                case UpgradeId.DoubleTrouble:
                case UpgradeId.PanicMode: return 1;
                case UpgradeId.EmergencySnack:
                case UpgradeId.BatteryCharge: return int.MaxValue;
                default: return 0;
            }
        }

        public bool Eligible(UpgradeId id) => health.IsAlive && Valid(id) && Count(id) < Cap(id)
            && (id != UpgradeId.EmergencySnack || health.Current < health.Maximum)
            && (id != UpgradeId.BatteryCharge || battery.Current < battery.Capacity);

        public bool AddXp(int amount = 1)
        {
            if (amount <= 0 || !health.IsAlive || IsMaxLevel || XP == thresholds[5]) return false;
            XP = (int)Math.Min(thresholds[5], (long)XP + amount);
            Changed?.Invoke();
            return true;
        }

        public UpgradeId Offer(int index)
        {
            if (!hasOffer || index < 0 || index >= OfferCount) throw new ArgumentOutOfRangeException(nameof(index));
            return offers[index];
        }

        public bool CreateOffer(Random random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (!health.IsAlive || !HasPendingSelection) return false;
            // Keep a valid offer stable through UI refreshes and rejected duplicate input.
            if (hasOffer)
            {
                bool valid = true;
                for (int i = 0; i < OfferCount; i++) valid &= Eligible(offers[i]);
                if (valid) return true;
            }
            var pool = new List<UpgradeId>(7);
            for (UpgradeId id = UpgradeId.RaveOverdrive; id <= UpgradeId.EmergencySnack; id++)
                if (Eligible(id)) pool.Add(id);
            if (pool.Count < 3 && Eligible(UpgradeId.BatteryCharge)) pool.Add(UpgradeId.BatteryCharge);
            OfferCount = Math.Min(offers.Length, pool.Count);
            for (int i = 0; i < OfferCount; i++)
            {
                int choice = random.Next(pool.Count);
                offers[i] = pool[choice];
                pool.RemoveAt(choice);
            }
            hasOffer = true;
            OfferVersion++;
            return true;
        }

        public bool TrySelect(int index, int version)
        {
            if (!health.IsAlive || !hasOffer || !HasPendingSelection || version != OfferVersion
                || index < 0 || index >= OfferCount || !Eligible(offers[index])) return false;
            UpgradeId id = offers[index];
            hasOffer = false;
            accepted.Add(id);
            stacks[(int)id]++;
            if (id == UpgradeId.BiggerBattery) battery.SetCapacity(100f * (1f + 0.4f * Count(id)));
            if (id == UpgradeId.EmergencySnack) health.TryHeal(30);
            if (id == UpgradeId.BatteryCharge) battery.TryRecharge(25f);
            Changed?.Invoke();
            return true;
        }

        public bool PanicEligible(bool chasing, float distance) => HasPanicMode && chasing
            && distance >= 0f && distance <= 10f;
        public float RidingMultiplier(bool guiltAdvantage) => 1f + MovementBonus + (guiltAdvantage ? 0.25f : 0f);
    }
}
