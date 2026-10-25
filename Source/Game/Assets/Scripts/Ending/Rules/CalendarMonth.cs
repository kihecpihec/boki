using System;

namespace Boki.Ending
{
    // Month grid of the wall calendar in the FERI ending (Story §3 victory). Weeks start on Monday, as in Slovenia.
    public sealed class CalendarMonth
    {
        public const int Columns = 7;
        public const int SaturdayColumn = 5;

        public int Year { get; }
        public int Month { get; }
        public int DayCount { get; }
        // Column of the 1st: 0 = Monday .. 6 = Sunday.
        public int FirstColumn { get; }
        public int Rows { get; }

        public CalendarMonth(int year, int month)
        {
            if (year < 1 || year > 9999) throw new ArgumentOutOfRangeException(nameof(year));
            if (month < 1 || month > 12) throw new ArgumentOutOfRangeException(nameof(month));
            Year = year;
            Month = month;
            DayCount = DateTime.DaysInMonth(year, month);
            FirstColumn = ((int)new DateTime(year, month, 1).DayOfWeek + 6) % Columns;
            Rows = (FirstColumn + DayCount + Columns - 1) / Columns;
        }

        public int ColumnOf(int day) => (FirstColumn + Checked(day) - 1) % Columns;
        public int RowOf(int day) => (FirstColumn + Checked(day) - 1) / Columns;
        public bool IsWeekend(int day) => ColumnOf(day) >= SaturdayColumn;
        public DayOfWeek WeekdayOf(int day) => new DateTime(Year, Month, Checked(day)).DayOfWeek;

        private int Checked(int day)
        {
            if (day < 1 || day > DayCount) throw new ArgumentOutOfRangeException(nameof(day));
            return day;
        }
    }
}
