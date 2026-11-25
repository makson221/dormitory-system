using System.Data;
using Dormitory.Models;
using Dormitory.Services;
using Microsoft.Data.Sqlite;

namespace Dormitory.Data
{
    // =====================================================================
    //  Користувачі
    // =====================================================================
    public static class UserRepository
    {
        private static User Map(SqliteDataReader r) => new User
        {
            Id = r.Long("Id"),
            Login = r.Str("Login"),
            FullName = r.Str("FullName"),
            Role = r.Str("Role"),
            IsActive = r.Long("IsActive") == 1,
            PasswordHash = r.Str("PasswordHash"),
            Salt = r.Str("Salt")
        };

        public static User GetByLogin(string login) =>
            Db.QueryList("SELECT * FROM Users WHERE Login = $l;", Map, ("$l", login)).FirstOrDefault();

        public static User Get(long id) =>
            Db.QueryList("SELECT * FROM Users WHERE Id = $id;", Map, ("$id", id)).FirstOrDefault();

        public static DataTable GetTable() => Db.Query(@"
            SELECT Id,
                   Login    AS ""Логін"",
                   FullName AS ""ПІБ"",
                   CASE Role WHEN 'Admin' THEN 'Адміністратор' ELSE 'Комендант' END AS ""Роль"",
                   CASE IsActive WHEN 1 THEN 'Активний' ELSE 'Заблокований' END   AS ""Статус""
            FROM Users ORDER BY Role, Login;");

        public static bool LoginExists(string login, long exceptId) =>
            Db.ScalarLong("SELECT COUNT(*) FROM Users WHERE Login = $l AND Id <> $id;", ("$l", login), ("$id", exceptId)) > 0;

        public static long Insert(User u, string password)
        {
            var (hash, salt) = PasswordHasher.Hash(password);
            return Db.Insert(
                "INSERT INTO Users (Login, PasswordHash, Salt, FullName, Role, IsActive) VALUES ($l, $h, $s, $n, $r, $a);",
                ("$l", u.Login), ("$h", hash), ("$s", salt), ("$n", u.FullName), ("$r", u.Role), ("$a", u.IsActive));
        }

        public static void Update(User u) =>
            Db.Execute("UPDATE Users SET Login = $l, FullName = $n, Role = $r, IsActive = $a WHERE Id = $id;",
                ("$l", u.Login), ("$n", u.FullName), ("$r", u.Role), ("$a", u.IsActive), ("$id", u.Id));

        public static void SetPassword(long userId, string password)
        {
            var (hash, salt) = PasswordHasher.Hash(password);
            Db.Execute("UPDATE Users SET PasswordHash = $h, Salt = $s WHERE Id = $id;",
                ("$h", hash), ("$s", salt), ("$id", userId));
        }

        public static void SetActive(long userId, bool active) =>
            Db.Execute("UPDATE Users SET IsActive = $a WHERE Id = $id;", ("$a", active), ("$id", userId));

        public static long ActiveAdminCount(long exceptId) =>
            Db.ScalarLong("SELECT COUNT(*) FROM Users WHERE Role = 'Admin' AND IsActive = 1 AND Id <> $id;", ("$id", exceptId));
    }

    // =====================================================================
    //  Кімнати
    // =====================================================================
    public static class RoomRepository
    {
        private const string OccupiedJoin =
            "LEFT JOIN (SELECT RoomId, COUNT(*) AS Cnt FROM Accommodations WHERE CheckOutDate IS NULL GROUP BY RoomId) o ON o.RoomId = r.Id";

        private static Room Map(SqliteDataReader r) => new Room
        {
            Id = r.Long("Id"),
            Number = r.Str("Number"),
            Floor = r.Int("Floor"),
            Capacity = r.Int("Capacity"),
            Gender = r.Str("Gender") ?? "",
            PricePerMonth = r.Dbl("PricePerMonth"),
            Notes = r.Str("Notes"),
            Occupied = r.Int("Occupied")
        };

        public static DataTable GetTable(bool onlyFree) => Db.Query($@"
            SELECT r.Id,
                   r.Number   AS ""Кімната"",
                   r.Floor    AS ""Поверх"",
                   CASE r.Gender WHEN 'Ч' THEN 'Чоловіча' WHEN 'Ж' THEN 'Жіноча' ELSE 'Будь-яка' END AS ""Тип"",
                   r.Capacity AS ""Місць"",
                   COALESCE(o.Cnt, 0)              AS ""Зайнято"",
                   r.Capacity - COALESCE(o.Cnt, 0) AS ""Вільно"",
                   r.PricePerMonth AS ""Ціна за місяць, грн"",
                   r.Notes    AS ""Примітка""
            FROM Rooms r {OccupiedJoin}
            WHERE $free = 0 OR r.Capacity - COALESCE(o.Cnt, 0) > 0
            ORDER BY r.Floor, r.Number;", ("$free", onlyFree));

        public static List<Room> GetAll() => Db.QueryList($@"
            SELECT r.*, COALESCE(o.Cnt, 0) AS Occupied
            FROM Rooms r {OccupiedJoin}
            ORDER BY r.Floor, r.Number;", Map);

        public static Room Get(long id) => Db.QueryList($@"
            SELECT r.*, COALESCE(o.Cnt, 0) AS Occupied
            FROM Rooms r {OccupiedJoin}
            WHERE r.Id = $id;", Map, ("$id", id)).FirstOrDefault();

        public static Room Get(SqliteConnection c, SqliteTransaction tx, long id) => Db.QueryList(c, tx, $@"
            SELECT r.*, COALESCE(o.Cnt, 0) AS Occupied
            FROM Rooms r {OccupiedJoin}
            WHERE r.Id = $id;", Map, ("$id", id)).FirstOrDefault();

        public static DataTable GetResidents(long roomId) => Db.Query(@"
            SELECT s.Id,
                   s.LastName || ' ' || s.FirstName || COALESCE(' ' || NULLIF(s.MiddleName, ''), '') AS ""ПІБ"",
                   s.Faculty AS ""Факультет"",
                   s.Course  AS ""Курс"",
                   s.Phone   AS ""Телефон"",
                   a.CheckInDate AS ""Дата поселення""
            FROM Accommodations a JOIN Students s ON s.Id = a.StudentId
            WHERE a.RoomId = $id AND a.CheckOutDate IS NULL
            ORDER BY s.LastName;", ("$id", roomId));

        public static bool NumberExists(string number, long exceptId) =>
            Db.ScalarLong("SELECT COUNT(*) FROM Rooms WHERE Number = $n AND Id <> $id;", ("$n", number), ("$id", exceptId)) > 0;

        public static long Insert(Room r) => Db.Insert(
            "INSERT INTO Rooms (Number, Floor, Capacity, Gender, PricePerMonth, Notes) VALUES ($n, $f, $c, $g, $p, $note);",
            ("$n", r.Number), ("$f", r.Floor), ("$c", r.Capacity), ("$g", r.Gender ?? ""), ("$p", r.PricePerMonth), ("$note", r.Notes));

        public static void Update(Room r) => Db.Execute(
            "UPDATE Rooms SET Number = $n, Floor = $f, Capacity = $c, Gender = $g, PricePerMonth = $p, Notes = $note WHERE Id = $id;",
            ("$n", r.Number), ("$f", r.Floor), ("$c", r.Capacity), ("$g", r.Gender ?? ""), ("$p", r.PricePerMonth),
            ("$note", r.Notes), ("$id", r.Id));

        public static void Delete(long id)
        {
            if (Db.ScalarLong("SELECT COUNT(*) FROM Accommodations WHERE RoomId = $id;", ("$id", id)) > 0)
                throw new BusinessException("Кімнату неможливо видалити: з нею пов'язані записи про поселення (у т.ч. архівні).");
            Db.Execute("DELETE FROM Rooms WHERE Id = $id;", ("$id", id));
        }

        /// <summary>Загальна статистика: усього місць / зайнято.</summary>
        public static (long Total, long Occupied) GetCapacityStats()
        {
            long total = Db.ScalarLong("SELECT COALESCE(SUM(Capacity), 0) FROM Rooms;");
            long occ = Db.ScalarLong("SELECT COUNT(*) FROM Accommodations WHERE CheckOutDate IS NULL;");
            return (total, occ);
        }
    }

    // =====================================================================
    //  Студенти
    // =====================================================================
    public static class StudentRepository
    {
        public const string FullNameSql =
            "s.LastName || ' ' || s.FirstName || COALESCE(' ' || NULLIF(s.MiddleName, ''), '')";

        private static Student Map(SqliteDataReader r) => new Student
        {
            Id = r.Long("Id"),
            LastName = r.Str("LastName"),
            FirstName = r.Str("FirstName"),
            MiddleName = r.Str("MiddleName"),
            BirthDate = r.DateN("BirthDate"),
            Gender = r.Str("Gender"),
            Faculty = r.Str("Faculty"),
            Course = r.Int("Course"),
            GroupName = r.Str("GroupName"),
            StudentTicket = r.Str("StudentTicket"),
            Phone = r.Str("Phone"),
            Email = r.Str("Email"),
            BenefitPercent = r.Int("BenefitPercent")
        };

        /// <param name="living">null — усі; true — лише ті, хто проживає; false — ті, хто не проживає.</param>
        public static DataTable GetTable(bool? living)
        {
            var table = Db.Query($@"
                SELECT s.Id,
                       {FullNameSql}   AS ""ПІБ"",
                       s.Gender        AS ""Стать"",
                       s.Faculty       AS ""Факультет"",
                       s.Course        AS ""Курс"",
                       s.GroupName     AS ""Група"",
                       s.StudentTicket AS ""Студ. квиток"",
                       s.Phone         AS ""Телефон"",
                       (SELECT r.Number FROM Accommodations a JOIN Rooms r ON r.Id = a.RoomId
                         WHERE a.StudentId = s.Id AND a.CheckOutDate IS NULL) AS ""Кімната"",
                       s.BenefitPercent AS ""Пільга (%)""
                FROM Students s
                WHERE $mode = 0
                   OR ($mode = 1 AND EXISTS (SELECT 1 FROM Accommodations a WHERE a.StudentId = s.Id AND a.CheckOutDate IS NULL))
                   OR ($mode = 2 AND NOT EXISTS (SELECT 1 FROM Accommodations a WHERE a.StudentId = s.Id AND a.CheckOutDate IS NULL))
                ORDER BY s.LastName, s.FirstName;",
                ("$mode", living == null ? 0 : living.Value ? 1 : 2));

            // Додаємо обчислюваний стовпець заборгованості
            var balances = BillingService.CalculateAll();
            var debtCol = table.Columns.Add("Борг, грн", typeof(double));
            foreach (DataRow row in table.Rows)
            {
                long id = (long)row["Id"];
                row[debtCol] = balances.TryGetValue(id, out var b) ? Math.Max(0, b.Debt) : 0.0;
            }
            table.AcceptChanges();
            return table;
        }

        public static Student Get(long id) =>
            Db.QueryList("SELECT * FROM Students WHERE Id = $id;", Map, ("$id", id)).FirstOrDefault();

        public static Student Get(SqliteConnection c, SqliteTransaction tx, long id) =>
            Db.QueryList(c, tx, "SELECT * FROM Students WHERE Id = $id;", Map, ("$id", id)).FirstOrDefault();

        public static List<Student> GetAll() =>
            Db.QueryList("SELECT * FROM Students ORDER BY LastName, FirstName;", Map);

        /// <summary>Студенти без активного поселення.</summary>
        public static List<Student> GetNotLiving() => Db.QueryList(@"
            SELECT * FROM Students s
            WHERE NOT EXISTS (SELECT 1 FROM Accommodations a WHERE a.StudentId = s.Id AND a.CheckOutDate IS NULL)
            ORDER BY LastName, FirstName;", Map);

        public static List<string> GetFaculties() =>
            Db.QueryList("SELECT DISTINCT Faculty FROM Students ORDER BY Faculty;", r => r.GetString(0));

        public static bool TicketExists(string ticket, long exceptId) =>
            Db.ScalarLong("SELECT COUNT(*) FROM Students WHERE StudentTicket = $t AND Id <> $id;",
                ("$t", ticket), ("$id", exceptId)) > 0;

        private static (string, object)[] Params(Student s) => new (string, object)[]
        {
            ("$ln", s.LastName), ("$fn", s.FirstName), ("$mn", s.MiddleName), ("$bd", s.BirthDate),
            ("$g", s.Gender), ("$fac", s.Faculty), ("$course", s.Course), ("$grp", s.GroupName),
            ("$t", s.StudentTicket), ("$ph", s.Phone), ("$em", s.Email), ("$b", s.BenefitPercent), ("$id", s.Id)
        };

        public static long Insert(Student s) => Db.Insert(@"
            INSERT INTO Students (LastName, FirstName, MiddleName, BirthDate, Gender, Faculty, Course,
                                  GroupName, StudentTicket, Phone, Email, BenefitPercent)
            VALUES ($ln, $fn, $mn, $bd, $g, $fac, $course, $grp, $t, $ph, $em, $b);", Params(s));

        public static void Update(Student s)
        {
            // Не можна змінити стать студента, якщо він живе в кімнаті іншого типу
            var room = Db.Scalar(@"SELECT r.Gender FROM Accommodations a JOIN Rooms r ON r.Id = a.RoomId
                                   WHERE a.StudentId = $id AND a.CheckOutDate IS NULL;", ("$id", s.Id)) as string;
            if (!string.IsNullOrEmpty(room) && room != s.Gender)
                throw new BusinessException("Студент проживає в кімнаті, тип якої не відповідає вказаній статі.");

            Db.Execute(@"
                UPDATE Students SET LastName = $ln, FirstName = $fn, MiddleName = $mn, BirthDate = $bd, Gender = $g,
                       Faculty = $fac, Course = $course, GroupName = $grp, StudentTicket = $t, Phone = $ph,
                       Email = $em, BenefitPercent = $b
                WHERE Id = $id;", Params(s));
        }

        public static void Delete(long id)
        {
            long links = Db.ScalarLong(@"SELECT (SELECT COUNT(*) FROM Accommodations WHERE StudentId = $id)
                                              + (SELECT COUNT(*) FROM Payments WHERE StudentId = $id);", ("$id", id));
            if (links > 0)
                throw new BusinessException("Студента неможливо видалити: для нього є записи про поселення або платежі.\n" +
                                            "Якщо студент вибув — оформіть виселення.");
            Db.Execute("DELETE FROM Students WHERE Id = $id;", ("$id", id));
        }
    }

    // =====================================================================
    //  Поселення
    // =====================================================================
    public static class AccommodationRepository
    {
        private static Accommodation Map(SqliteDataReader r) => new Accommodation
        {
            Id = r.Long("Id"),
            StudentId = r.Long("StudentId"),
            RoomId = r.Long("RoomId"),
            CheckInDate = r.Date("CheckInDate"),
            CheckOutDate = r.DateN("CheckOutDate"),
            ContractNumber = r.Str("ContractNumber"),
            Notes = r.Str("Notes")
        };

        public static DataTable GetTable(bool includeArchive) => Db.Query($@"
            SELECT a.Id, a.StudentId, a.RoomId,
                   {StudentRepository.FullNameSql} AS ""ПІБ"",
                   s.Faculty AS ""Факультет"",
                   s.Course  AS ""Курс"",
                   r.Number  AS ""Кімната"",
                   a.ContractNumber AS ""№ договору"",
                   a.CheckInDate    AS ""Дата поселення"",
                   a.CheckOutDate   AS ""Дата виселення"",
                   CASE WHEN a.CheckOutDate IS NULL THEN 'Проживає' ELSE 'Виселений' END AS ""Статус"",
                   a.Notes AS ""Примітка""
            FROM Accommodations a
            JOIN Students s ON s.Id = a.StudentId
            JOIN Rooms r    ON r.Id = a.RoomId
            WHERE $all = 1 OR a.CheckOutDate IS NULL
            ORDER BY a.CheckOutDate IS NOT NULL, r.Number, s.LastName;", ("$all", includeArchive));

        public static Accommodation Get(long id) =>
            Db.QueryList("SELECT * FROM Accommodations WHERE Id = $id;", Map, ("$id", id)).FirstOrDefault();

        public static Accommodation Get(SqliteConnection c, SqliteTransaction tx, long id) =>
            Db.QueryList(c, tx, "SELECT * FROM Accommodations WHERE Id = $id;", Map, ("$id", id)).FirstOrDefault();

        public static Accommodation GetActiveForStudent(SqliteConnection c, SqliteTransaction tx, long studentId) =>
            Db.QueryList(c, tx, "SELECT * FROM Accommodations WHERE StudentId = $s AND CheckOutDate IS NULL;",
                Map, ("$s", studentId)).FirstOrDefault();

        public static string NextContractNumber()
        {
            long n = Db.ScalarLong("SELECT COUNT(*) FROM Accommodations;") + 1;
            string number;
            do
            {
                number = $"Д-{DateTime.Today.Year}-{n++:0000}";
            } while (Db.ScalarLong("SELECT COUNT(*) FROM Accommodations WHERE ContractNumber = $n;", ("$n", number)) > 0);
            return number;
        }
    }

    // =====================================================================
    //  Платежі
    // =====================================================================
    public static class PaymentRepository
    {
        public static DataTable GetTable(DateTime from, DateTime to) => Db.Query($@"
            SELECT p.Id, p.StudentId,
                   p.PaymentDate AS ""Дата"",
                   {StudentRepository.FullNameSql} AS ""ПІБ"",
                   (SELECT r.Number FROM Accommodations a JOIN Rooms r ON r.Id = a.RoomId
                     WHERE a.StudentId = s.Id AND a.CheckOutDate IS NULL) AS ""Кімната"",
                   p.Period        AS ""За місяць"",
                   p.Amount        AS ""Сума, грн"",
                   p.ReceiptNumber AS ""№ квитанції"",
                   u.FullName      AS ""Прийняв"",
                   p.Comment       AS ""Коментар""
            FROM Payments p
            JOIN Students s ON s.Id = p.StudentId
            LEFT JOIN Users u ON u.Id = p.UserId
            WHERE p.PaymentDate BETWEEN $from AND $to
            ORDER BY p.PaymentDate DESC, p.Id DESC;", ("$from", from.Date), ("$to", to.Date));

        public static long Insert(Payment p) => Db.Insert(@"
            INSERT INTO Payments (StudentId, Amount, PaymentDate, Period, ReceiptNumber, UserId, Comment)
            VALUES ($s, $a, $d, $p, $r, $u, $c);",
            ("$s", p.StudentId), ("$a", p.Amount), ("$d", p.PaymentDate.Date), ("$p", p.Period),
            ("$r", p.ReceiptNumber), ("$u", p.UserId), ("$c", p.Comment));

        public static void Delete(long id) => Db.Execute("DELETE FROM Payments WHERE Id = $id;", ("$id", id));

        public static string NextReceiptNumber()
        {
            long n = Db.ScalarLong("SELECT COALESCE(MAX(Id), 0) FROM Payments;") + 1;
            return $"КВТ-{DateTime.Today:yyMMdd}-{n:0000}";
        }
    }
}
