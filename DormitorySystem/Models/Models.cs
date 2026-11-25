namespace Dormitory.Models
{
    /// <summary>Ролі користувачів системи.</summary>
    public static class Roles
    {
        public const string Admin = "Admin";
        public const string Commandant = "Commandant";

        public static string Display(string role) =>
            role == Admin ? "Адміністратор" : role == Commandant ? "Комендант" : role;
    }

    /// <summary>Користувач системи (адміністратор або комендант).</summary>
    public class User
    {
        public long Id { get; set; }
        public string Login { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; }
        public bool IsActive { get; set; } = true;
        public string PasswordHash { get; set; }
        public string Salt { get; set; }

        public bool IsAdmin => Role == Roles.Admin;
    }

    /// <summary>Кімната гуртожитку.</summary>
    public class Room
    {
        public long Id { get; set; }
        public string Number { get; set; }
        public int Floor { get; set; }
        public int Capacity { get; set; }
        /// <summary>'Ч' — чоловіча, 'Ж' — жіноча, '' — будь-яка.</summary>
        public string Gender { get; set; } = "";
        public double PricePerMonth { get; set; }
        public string Notes { get; set; }
        /// <summary>Кількість мешканців зараз (обчислюване поле).</summary>
        public int Occupied { get; set; }

        public int Free => Capacity - Occupied;

        public static string GenderDisplay(string g) =>
            g == "Ч" ? "Чоловіча" : g == "Ж" ? "Жіноча" : "Будь-яка";
    }

    /// <summary>Студент.</summary>
    public class Student
    {
        public long Id { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public DateTime? BirthDate { get; set; }
        /// <summary>'Ч' або 'Ж'.</summary>
        public string Gender { get; set; } = "Ч";
        public string Faculty { get; set; }
        public int Course { get; set; } = 1;
        public string GroupName { get; set; }
        public string StudentTicket { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        /// <summary>Пільга на оплату проживання, %.</summary>
        public int BenefitPercent { get; set; }

        public string FullName =>
            string.Join(" ", new[] { LastName, FirstName, MiddleName }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    /// <summary>Запис про поселення студента в кімнату.</summary>
    public class Accommodation
    {
        public long Id { get; set; }
        public long StudentId { get; set; }
        public long RoomId { get; set; }
        public DateTime CheckInDate { get; set; }
        public DateTime? CheckOutDate { get; set; }
        public string ContractNumber { get; set; }
        public string Notes { get; set; }

        public bool IsActive => CheckOutDate == null;
    }

    /// <summary>Платіж за проживання.</summary>
    public class Payment
    {
        public long Id { get; set; }
        public long StudentId { get; set; }
        public double Amount { get; set; }
        public DateTime PaymentDate { get; set; }
        /// <summary>Місяць, за який внесено оплату, у форматі yyyy-MM.</summary>
        public string Period { get; set; }
        public string ReceiptNumber { get; set; }
        public long? UserId { get; set; }
        public string Comment { get; set; }
    }

    /// <summary>Фінансовий стан студента.</summary>
    public class BalanceInfo
    {
        public long StudentId { get; set; }
        public string FullName { get; set; }
        public string CurrentRoom { get; set; }
        public int BenefitPercent { get; set; }
        /// <summary>Щомісячна плата з урахуванням пільги (0, якщо не проживає).</summary>
        public double MonthlyFee { get; set; }
        public double Charged { get; set; }
        public double Paid { get; set; }
        public double Debt => Math.Round(Charged - Paid, 2);
    }

    /// <summary>Елемент для випадаючих списків.</summary>
    public class ListItem
    {
        public long Id { get; set; }
        public string Text { get; set; }
        public object Tag { get; set; }

        public ListItem(long id, string text, object tag = null)
        {
            Id = id;
            Text = text;
            Tag = tag;
        }

        public override string ToString() => Text;
    }

    /// <summary>Порушення бізнес-правил (показується користувачу як попередження).</summary>
    public class BusinessException : Exception
    {
        public BusinessException(string message) : base(message) { }
    }
}
