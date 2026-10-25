using System;
using Boki.Ending;
using NUnit.Framework;

namespace Boki.Tests
{
    public sealed class CalendarMonthTests
    {
        [Test]
        public void TheEndingDateIsASaturday()
        {
            // »Danes je sobota« must be true of the calendar the professor points to.
            Assert.That(EndingDate.Weekday, Is.EqualTo(DayOfWeek.Saturday));
        }

        [Test]
        public void TheEndingDateLandsInTheSaturdayColumnOfItsMonth()
        {
            var month = new CalendarMonth(EndingDate.Year, EndingDate.Month);
            Assert.That(month.ColumnOf(EndingDate.Day), Is.EqualTo(CalendarMonth.SaturdayColumn));
            Assert.That(month.WeekdayOf(EndingDate.Day), Is.EqualTo(DayOfWeek.Saturday));
            Assert.That(month.DayCount, Is.EqualTo(EndingDate.Day));
        }

        [Test]
        public void OctoberTwentyTwentySixStartsOnThursdayAndFillsFiveRows()
        {
            var month = new CalendarMonth(2026, 10);
            Assert.That(month.FirstColumn, Is.EqualTo(3));
            Assert.That(month.Rows, Is.EqualTo(5));
            Assert.That(month.RowOf(1), Is.Zero);
            Assert.That(month.RowOf(31), Is.EqualTo(4));
        }

        [Test]
        public void WeeksStartOnMonday()
        {
            // 5 Oct 2026 is a Monday, so it opens the second row.
            var month = new CalendarMonth(2026, 10);
            Assert.That(month.ColumnOf(5), Is.Zero);
            Assert.That(month.RowOf(5), Is.EqualTo(1));
            Assert.That(month.ColumnOf(11), Is.EqualTo(6));
        }

        [Test]
        public void SaturdaysAndSundaysAreTheWeekend()
        {
            var month = new CalendarMonth(2026, 10);
            Assert.That(month.IsWeekend(31), Is.True);
            Assert.That(month.IsWeekend(25), Is.True);
            Assert.That(month.IsWeekend(30), Is.False);
        }

        [Test]
        public void LeapFebruaryHasTwentyNineDays()
        {
            Assert.That(new CalendarMonth(2024, 2).DayCount, Is.EqualTo(29));
            Assert.That(new CalendarMonth(2026, 2).DayCount, Is.EqualTo(28));
        }

        [Test]
        public void ASixRowMonthIsCounted()
        {
            // 1 Mar 2026 is a Sunday: 31 days starting in the last column need six rows.
            Assert.That(new CalendarMonth(2026, 3).Rows, Is.EqualTo(6));
        }

        [Test]
        public void InvalidMonthsAndDaysAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CalendarMonth(2026, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CalendarMonth(2026, 13));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CalendarMonth(0, 10));
            var month = new CalendarMonth(2026, 10);
            Assert.Throws<ArgumentOutOfRangeException>(() => month.ColumnOf(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => month.RowOf(32));
        }
    }
}
