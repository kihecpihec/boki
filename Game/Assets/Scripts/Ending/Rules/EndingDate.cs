using System;

namespace Boki.Ending
{
    // The date the professor points to. It must be a Saturday: it is what makes »danes je sobota« true (Story §3 victory).
    public static class EndingDate
    {
        public const int Year = 2026;
        public const int Month = 10;
        public const int Day = 31;

        public static DayOfWeek Weekday => new DateTime(Year, Month, Day).DayOfWeek;
    }
}
