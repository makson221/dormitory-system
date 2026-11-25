using Dormitory.Data;
using Dormitory.Models;

namespace Dormitory.Services
{
    /// <summary>
    /// Нарахування плати за проживання та розрахунок заборгованості.
    /// Правило: оплачується кожен розпочатий календарний місяць проживання за ціною кімнати,
    /// у якій студент проживав у цьому місяці (при переселенні — за новою кімнатою),
    /// з урахуванням пільги студента.
    /// </summary>
    public static class BillingService
    {
        private class AccRow
        {
            public long StudentId;
            public DateTime CheckIn;
            public DateTime? CheckOut;
            public double Price;
        }

        public static Dictionary<long, BalanceInfo> CalculateAll(DateTime? asOf = null)
        {
            var today = (asOf ?? DateTime.Today).Date;
            var result = new Dictionary<long, BalanceInfo>();

            // 1. Студенти та їхня поточна кімната
            foreach (var b in Db.QueryList($@"
                SELECT s.Id, {StudentRepository.FullNameSql} AS FullName, s.BenefitPercent,
                       r.Number AS RoomNumber, r.PricePerMonth AS Price
                FROM Students s
                LEFT JOIN Accommodations a ON a.StudentId = s.Id AND a.CheckOutDate IS NULL
                LEFT JOIN Rooms r ON r.Id = a.RoomId;",
                r => new BalanceInfo
                {
                    StudentId = r.Long("Id"),
                    FullName = r.Str("FullName"),
                    BenefitPercent = r.Int("BenefitPercent"),
                    CurrentRoom = r.Str("RoomNumber"),
                    MonthlyFee = ApplyBenefit(r.Dbl("Price"), r.Int("BenefitPercent"))
                }))
            {
                result[b.StudentId] = b;
            }

            // 2. Нарахування за історією поселень
            var accs = Db.QueryList(@"
                SELECT a.StudentId, a.CheckInDate, a.CheckOutDate, r.PricePerMonth
                FROM Accommodations a JOIN Rooms r ON r.Id = a.RoomId
                ORDER BY a.StudentId, a.CheckInDate, a.Id;",
                r => new AccRow
                {
                    StudentId = r.Long("StudentId"),
                    CheckIn = r.Date("CheckInDate"),
                    CheckOut = r.DateN("CheckOutDate"),
                    Price = r.Dbl("PricePerMonth")
                });

            foreach (var group in accs.GroupBy(a => a.StudentId))
            {
                if (!result.TryGetValue(group.Key, out var info)) continue;

                // ключ місяця (рік*12 + місяць) → ціна кімнати; пізніше поселення перекриває раніше
                var months = new Dictionary<int, double>();
                foreach (var a in group)
                {
                    var end = a.CheckOut ?? today;
                    if (end > today) end = today;
                    if (a.CheckIn > end) continue;

                    for (int k = MonthKey(a.CheckIn); k <= MonthKey(end); k++)
                        months[k] = a.Price;
                }

                info.Charged = ApplyBenefit(months.Values.Sum(), info.BenefitPercent);
            }

            // 3. Сплачені суми
            foreach (var (studentId, paid) in Db.QueryList(
                "SELECT StudentId, SUM(Amount) AS Paid FROM Payments GROUP BY StudentId;",
                r => (r.Long("StudentId"), r.Dbl("Paid"))))
            {
                if (result.TryGetValue(studentId, out var info))
                    info.Paid = Math.Round(paid, 2);
            }

            return result;
        }

        public static BalanceInfo Calculate(long studentId) =>
            CalculateAll().TryGetValue(studentId, out var b) ? b : new BalanceInfo { StudentId = studentId };

        public static double ApplyBenefit(double amount, int benefitPercent) =>
            Math.Round(amount * (100 - benefitPercent) / 100.0, 2);

        private static int MonthKey(DateTime d) => d.Year * 12 + d.Month - 1;
    }
}
