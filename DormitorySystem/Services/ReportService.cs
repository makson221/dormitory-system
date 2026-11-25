using System.Data;
using Dormitory.Data;

namespace Dormitory.Services
{
    public class ReportResult
    {
        public string Title { get; set; }
        public DataTable Table { get; set; }
        public string Summary { get; set; }
    }

    /// <summary>Формування звітів.</summary>
    public static class ReportService
    {
        public static readonly string[] ReportNames =
        {
            "Заселеність кімнат",
            "Список мешканців гуртожитку",
            "Боржники з оплати проживання",
            "Статистика за факультетами",
            "Надходження оплат за період (по місяцях)",
            "Рух мешканців за період (поселення / виселення)"
        };

        /// <summary>Чи потрібен звіту період дат.</summary>
        public static bool NeedsPeriod(int index) => index == 4 || index == 5;

        public static ReportResult Build(int index, DateTime from, DateTime to)
        {
            switch (index)
            {
                case 0: return Occupancy();
                case 1: return Residents();
                case 2: return Debtors();
                case 3: return Faculties();
                case 4: return PaymentsByMonth(from, to);
                case 5: return Movement(from, to);
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        private static ReportResult Occupancy()
        {
            var t = Db.Query(@"
                SELECT r.Number AS ""Кімната"", r.Floor AS ""Поверх"",
                       CASE r.Gender WHEN 'Ч' THEN 'Чоловіча' WHEN 'Ж' THEN 'Жіноча' ELSE 'Будь-яка' END AS ""Тип"",
                       r.Capacity AS ""Місць"",
                       COALESCE(o.Cnt, 0) AS ""Зайнято"",
                       r.Capacity - COALESCE(o.Cnt, 0) AS ""Вільно"",
                       ROUND(100.0 * COALESCE(o.Cnt, 0) / r.Capacity, 1) AS ""Заповненість, %""
                FROM Rooms r
                LEFT JOIN (SELECT RoomId, COUNT(*) AS Cnt FROM Accommodations WHERE CheckOutDate IS NULL GROUP BY RoomId) o
                       ON o.RoomId = r.Id
                ORDER BY r.Floor, r.Number;");

            var (total, occupied) = RoomRepository.GetCapacityStats();
            double pct = total == 0 ? 0 : 100.0 * occupied / total;
            return new ReportResult
            {
                Title = ReportNames[0],
                Table = t,
                Summary = $"Кімнат: {t.Rows.Count}.  Усього місць: {total}.  Зайнято: {occupied}.  " +
                          $"Вільно: {total - occupied}.  Заповненість: {pct:0.0}%."
            };
        }

        private static ReportResult Residents()
        {
            var t = Db.Query($@"
                SELECT r.Number AS ""Кімната"",
                       {StudentRepository.FullNameSql} AS ""ПІБ"",
                       s.Faculty AS ""Факультет"", s.Course AS ""Курс"", s.GroupName AS ""Група"",
                       s.Phone AS ""Телефон"", a.CheckInDate AS ""Дата поселення"", a.ContractNumber AS ""№ договору""
                FROM Accommodations a
                JOIN Students s ON s.Id = a.StudentId
                JOIN Rooms r ON r.Id = a.RoomId
                WHERE a.CheckOutDate IS NULL
                ORDER BY r.Floor, r.Number, s.LastName;");
            return new ReportResult
            {
                Title = ReportNames[1],
                Table = t,
                Summary = $"Мешканців: {t.Rows.Count}."
            };
        }

        private static ReportResult Debtors()
        {
            var t = new DataTable();
            t.Columns.Add("ПІБ", typeof(string));
            t.Columns.Add("Кімната", typeof(string));
            t.Columns.Add("Щомісячна плата, грн", typeof(double));
            t.Columns.Add("Нараховано, грн", typeof(double));
            t.Columns.Add("Сплачено, грн", typeof(double));
            t.Columns.Add("Борг, грн", typeof(double));

            var debtors = BillingService.CalculateAll().Values
                .Where(b => b.Debt > 0.009)
                .OrderByDescending(b => b.Debt)
                .ToList();

            foreach (var b in debtors)
                t.Rows.Add(b.FullName, b.CurrentRoom ?? "(виселений)", b.MonthlyFee, b.Charged, b.Paid, b.Debt);

            return new ReportResult
            {
                Title = ReportNames[2],
                Table = t,
                Summary = $"Боржників: {debtors.Count}.  Загальна сума заборгованості: {debtors.Sum(d => d.Debt):N2} грн."
            };
        }

        private static ReportResult Faculties()
        {
            var t = Db.Query(@"
                SELECT s.Faculty AS ""Факультет"",
                       COUNT(*) AS ""Студентів у базі"",
                       SUM(CASE WHEN a.Id IS NOT NULL THEN 1 ELSE 0 END) AS ""Проживають"",
                       SUM(CASE WHEN a.Id IS NOT NULL AND s.Gender = 'Ч' THEN 1 ELSE 0 END) AS ""з них хлопців"",
                       SUM(CASE WHEN a.Id IS NOT NULL AND s.Gender = 'Ж' THEN 1 ELSE 0 END) AS ""з них дівчат"",
                       SUM(CASE WHEN a.Id IS NOT NULL AND s.BenefitPercent > 0 THEN 1 ELSE 0 END) AS ""Пільговиків""
                FROM Students s
                LEFT JOIN Accommodations a ON a.StudentId = s.Id AND a.CheckOutDate IS NULL
                GROUP BY s.Faculty
                ORDER BY ""Проживають"" DESC, s.Faculty;");

            long living = t.Rows.Cast<DataRow>().Sum(r => (long)r["Проживають"]);
            return new ReportResult
            {
                Title = ReportNames[3],
                Table = t,
                Summary = $"Факультетів: {t.Rows.Count}.  Усього мешканців: {living}."
            };
        }

        private static ReportResult PaymentsByMonth(DateTime from, DateTime to)
        {
            var t = Db.Query(@"
                SELECT substr(PaymentDate, 1, 7) AS ""Місяць"",
                       COUNT(*)                  AS ""Кількість платежів"",
                       COUNT(DISTINCT StudentId) AS ""Студентів"",
                       ROUND(SUM(Amount), 2)     AS ""Сума, грн""
                FROM Payments
                WHERE PaymentDate BETWEEN $from AND $to
                GROUP BY substr(PaymentDate, 1, 7)
                ORDER BY 1;", ("$from", from.Date), ("$to", to.Date));

            double sum = t.Rows.Cast<DataRow>().Sum(r => Convert.ToDouble(r["Сума, грн"]));
            return new ReportResult
            {
                Title = $"{ReportNames[4]}: {from:dd.MM.yyyy} – {to:dd.MM.yyyy}",
                Table = t,
                Summary = $"Усього надійшло: {sum:N2} грн."
            };
        }

        private static ReportResult Movement(DateTime from, DateTime to)
        {
            var t = Db.Query($@"
                SELECT ev.EventDate AS ""Дата"", ev.EventType AS ""Подія"", ev.FullName AS ""ПІБ"", ev.Room AS ""Кімната""
                FROM (
                    SELECT a.CheckInDate AS EventDate, 'Поселення' AS EventType,
                           {StudentRepository.FullNameSql} AS FullName, r.Number AS Room
                    FROM Accommodations a JOIN Students s ON s.Id = a.StudentId JOIN Rooms r ON r.Id = a.RoomId
                    WHERE a.CheckInDate BETWEEN $from AND $to
                    UNION ALL
                    SELECT a.CheckOutDate, 'Виселення',
                           {StudentRepository.FullNameSql}, r.Number
                    FROM Accommodations a JOIN Students s ON s.Id = a.StudentId JOIN Rooms r ON r.Id = a.RoomId
                    WHERE a.CheckOutDate BETWEEN $from AND $to
                ) ev
                ORDER BY ev.EventDate, ev.EventType DESC;", ("$from", from.Date), ("$to", to.Date));

            int ins = t.Rows.Cast<DataRow>().Count(r => (string)r["Подія"] == "Поселення");
            return new ReportResult
            {
                Title = $"{ReportNames[5]}: {from:dd.MM.yyyy} – {to:dd.MM.yyyy}",
                Table = t,
                Summary = $"Поселено: {ins}.  Виселено: {t.Rows.Count - ins}."
            };
        }
    }
}
