namespace Boki.Enemies
{
    // Gameplay §5 "Initial Playtest Rules: Pressure and Timing" table; tuning stays in that document.
    public readonly struct GuiltTier
    {
        public readonly float RestSeconds;
        public readonly float ChaseSpeed;
        public readonly int ContactDamage;

        public GuiltTier(float restSeconds, float chaseSpeed, int contactDamage)
        {
            RestSeconds = restSeconds;
            ChaseSpeed = chaseSpeed;
            ContactDamage = contactDamage;
        }
    }

    public static class GuiltPressure
    {
        public static GuiltTier Evaluate(float accumulatedCombatSeconds)
        {
            if (accumulatedCombatSeconds < 30f) return new GuiltTier(18f, 4.5f, 10);
            if (accumulatedCombatSeconds < 60f) return new GuiltTier(15f, 5.0f, 12);
            if (accumulatedCombatSeconds < 90f) return new GuiltTier(12f, 5.5f, 14);
            if (accumulatedCombatSeconds < 120f) return new GuiltTier(9f, 6.0f, 16);
            return new GuiltTier(6f, 6.5f, 18);
        }
    }
}
