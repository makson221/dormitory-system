using Dormitory.Models;
using Dormitory.Services;
using Microsoft.Data.Sqlite;

namespace Dormitory.Data
{
    /// <summary>Початкові дані: облікові записи та демонстраційний набір для захисту проєкту.</summary>
    internal static class SeedData
    {
        public static void EnsureSeeded(SqliteConnection c)
        {
            if (Db.ScalarLong(c, null, "SELECT COUNT(*) FROM Users;") == 0)
                SeedUsers(c);

            if (Db.ScalarLong(c, null, "SELECT COUNT(*) FROM Rooms;") == 0)
                SeedDemo(c);
        }

        private static void SeedUsers(SqliteConnection c)
        {
            AddUser(c, "admin", "admin123", "Адміністратор системи", Roles.Admin);
            AddUser(c, "komendant", "komendant1", "Ковальчук Галина Іванівна", Roles.Commandant);
        }

        private static void AddUser(SqliteConnection c, string login, string password, string name, string role)
        {
            var (hash, salt) = PasswordHasher.Hash(password);
            Db.Execute(c, null,
                "INSERT INTO Users (Login, PasswordHash, Salt, FullName, Role, IsActive) VALUES ($l, $h, $s, $n, $r, 1);",
                ("$l", login), ("$h", hash), ("$s", salt), ("$n", name), ("$r", role));
        }

        private static readonly string[] Faculties =
        {
            "Інформаційних технологій", "Економічний", "Юридичний", "Електроніки", "Філологічний"
        };

        // Прізвище, ім'я, по батькові, стать
        private static readonly string[][] People =
        {
            new[] { "Коваленко", "Андрій", "Петрович", "Ч" },
            new[] { "Шевчук", "Олег", "Іванович", "Ч" },
            new[] { "Бондаренко", "Максим", "Сергійович", "Ч" },
            new[] { "Ткаченко", "Дмитро", "Олександрович", "Ч" },
            new[] { "Мельник", "Богдан", "Васильович", "Ч" },
            new[] { "Кравченко", "Артем", "Миколайович", "Ч" },
            new[] { "Олійник", "Владислав", "Юрійович", "Ч" },
            new[] { "Поліщук", "Назар", "Андрійович", "Ч" },
            new[] { "Лисенко", "Тарас", "Ігорович", "Ч" },
            new[] { "Савченко", "Ілля", "Романович", "Ч" },
            new[] { "Петренко", "Олена", "Василівна", "Ж" },
            new[] { "Іваненко", "Марія", "Олегівна", "Ж" },
            new[] { "Бойко", "Анастасія", "Сергіївна", "Ж" },
            new[] { "Мороз", "Софія", "Віталіївна", "Ж" },
            new[] { "Кузьменко", "Юлія", "Андріївна", "Ж" },
            new[] { "Павленко", "Дарина", "Ігорівна", "Ж" },
            new[] { "Руденко", "Вікторія", "Михайлівна", "Ж" },
            new[] { "Марченко", "Катерина", "Олександрівна", "Ж" },
            new[] { "Гаврилюк", "Ірина", "Петрівна", "Ж" },
            new[] { "Захарченко", "Аліна", "Богданівна", "Ж" },
        };

        private static void SeedDemo(SqliteConnection c)
        {
            using var tx = c.BeginTransaction();

            // --- Кімнати: 1 поверх — чоловічий, 2 — жіночий, 3 — будь-який
            int[] capacities = { 2, 3, 3, 4, 2, 3 };
            var rooms = new List<(long Id, int Capacity, string Gender, double Price)>();
            for (int floor = 1; floor <= 3; floor++)
            {
                string gender = floor == 1 ? "Ч" : floor == 2 ? "Ж" : "";
                int count = floor == 3 ? 4 : 6;
                for (int i = 0; i < count; i++)
                {
                    int cap = capacities[i];
                    double price = cap == 2 ? 900 : cap == 3 ? 750 : 650;
                    string number = $"{floor}{i + 1:00}";
                    long id = Db.Insert(c, tx,
                        "INSERT INTO Rooms (Number, Floor, Capacity, Gender, PricePerMonth, Notes) VALUES ($n, $f, $c, $g, $p, $note);",
                        ("$n", number), ("$f", floor), ("$c", cap), ("$g", gender), ("$p", price),
                        ("$note", i == 0 ? "Біля сходів" : null));
                    rooms.Add((id, cap, gender, price));
                }
            }

            // --- Студенти
            var rnd = new Random(2026);
            var students = new List<(long Id, string Gender, int Benefit)>();
            for (int i = 0; i < People.Length; i++)
            {
                var p = People[i];
                int course = 1 + i % 4;
                string faculty = Faculties[i % Faculties.Length];
                int benefit = i == 3 || i == 14 ? 50 : 0;     // пільговики
                var birth = new DateTime(2007 - course, 1 + rnd.Next(12), 1 + rnd.Next(28));
                string group = FacultyCode(faculty) + "-" + (26 - course) + (1 + i % 2);
                long id = Db.Insert(c, tx,
                    @"INSERT INTO Students (LastName, FirstName, MiddleName, BirthDate, Gender, Faculty, Course,
                                            GroupName, StudentTicket, Phone, Email, BenefitPercent)
                      VALUES ($ln, $fn, $mn, $bd, $g, $fac, $course, $grp, $t, $ph, $em, $b);",
                    ("$ln", p[0]), ("$fn", p[1]), ("$mn", p[2]), ("$bd", birth), ("$g", p[3]),
                    ("$fac", faculty), ("$course", course), ("$grp", group),
                    ("$t", $"КВ{12345600 + i:00000000}"),
                    ("$ph", $"+38067{1000000 + rnd.Next(8999999)}"),
                    ("$em", null), ("$b", benefit));
                students.Add((id, p[3], benefit));
            }

            // --- Поселення з 1 вересня поточного навчального року
            var today = DateTime.Today;
            var yearStart = new DateTime(today.Month >= 9 ? today.Year : today.Year - 1, 9, 1);
            var occupied = new Dictionary<long, int>();
            int contract = 1;

            for (int i = 0; i < students.Count; i++)
            {
                var st = students[i];
                if (i == 8 || i == 9 || i == 18 || i == 19) continue;     // ще не поселені

                var room = rooms.First(r => (r.Gender == st.Gender || r.Gender == "") &&
                                            occupied.GetValueOrDefault(r.Id) < r.Capacity);
                occupied[room.Id] = occupied.GetValueOrDefault(room.Id) + 1;

                var checkIn = yearStart.AddDays(i % 5);
                if (checkIn > today) checkIn = today;
                Db.Execute(c, tx,
                    "INSERT INTO Accommodations (StudentId, RoomId, CheckInDate, ContractNumber) VALUES ($s, $r, $d, $n);",
                    ("$s", st.Id), ("$r", room.Id), ("$d", checkIn),
                    ("$n", $"Д-{yearStart.Year}-{contract++:0000}"));

                // --- Платежі: щомісяця 5-го числа; кожен четвертий студент пропустив 2 останні місяці (боржник)
                double fee = Math.Round(room.Price * (100 - st.Benefit) / 100.0, 2);
                var month = new DateTime(yearStart.Year, yearStart.Month, 1);
                int receipt = 1;
                while (month <= today)
                {
                    bool skip = i % 4 == 0 && month >= new DateTime(today.Year, today.Month, 1).AddMonths(-1);
                    var payDate = month.AddDays(4);
                    if (!skip && payDate <= today && fee > 0)
                    {
                        Db.Execute(c, tx,
                            @"INSERT INTO Payments (StudentId, Amount, PaymentDate, Period, ReceiptNumber, UserId, Comment)
                              VALUES ($s, $a, $d, $p, $rc, 2, NULL);",
                            ("$s", st.Id), ("$a", fee), ("$d", payDate), ("$p", month.ToString("yyyy-MM")),
                            ("$rc", $"КВТ-{st.Id:000}-{receipt++:00}"));
                    }
                    month = month.AddMonths(1);
                }
            }

            tx.Commit();
        }

        private static string FacultyCode(string faculty) => faculty switch
        {
            "Інформаційних технологій" => "ІТ",
            "Економічний" => "ЕК",
            "Юридичний" => "ЮР",
            "Електроніки" => "ЕЛ",
            _ => "ФЛ"
        };
    }
}
