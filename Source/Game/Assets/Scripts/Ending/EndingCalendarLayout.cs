using UnityEngine;

namespace Boki.Ending
{
    // Where things sit on the wall calendar, in meters from its center on the card plane (+X right, +Y up as a viewer
    // standing in the room sees it). The Editor builder lays the card out with this and EndingActing aims the zoom with it.
    public static class EndingCalendarLayout
    {
        public const float Width = 0.66f;
        public const float Height = 0.66f;
        public const float CellWidth = 0.085f;
        public const float RowHeight = 0.075f;
        public const float TitleY = 0.265f;
        public const float WeekdayY = 0.175f;
        public const float FirstRowY = 0.095f;

        public static Vector2 Cell(int column, int row)
            => new Vector2((column - (CalendarMonth.Columns - 1) * 0.5f) * CellWidth, FirstRowY - row * RowHeight);

        public static Vector2 EndingDateCell()
        {
            var month = new CalendarMonth(EndingDate.Year, EndingDate.Month);
            return Cell(month.ColumnOf(EndingDate.Day), month.RowOf(EndingDate.Day));
        }
    }
}
