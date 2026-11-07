namespace ObentoTensho.Core.Customers.Domain;

public static class BirthdayRules
{
    // Có nằm trong khoảng `days` ngày quanh sinh nhật năm nay, năm trước hoặc năm sau không (tính cả ngày giáp năm)
    public static bool IsNear(DateOnly dateOfBirth, DateOnly today, int days)
    {
        foreach (var year in new[] { today.Year - 1, today.Year, today.Year + 1 })
        {
            var day = Math.Min(dateOfBirth.Day, DateTime.DaysInMonth(year, dateOfBirth.Month));
            if (Math.Abs(new DateOnly(year, dateOfBirth.Month, day).DayNumber - today.DayNumber) <= days)
            {
                return true;
            }
        }

        return false;
    }
}
