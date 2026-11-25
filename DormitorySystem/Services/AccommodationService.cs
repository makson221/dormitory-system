using Dormitory.Data;
using Dormitory.Models;

namespace Dormitory.Services
{
    /// <summary>Бізнес-логіка поселення, виселення та переселення студентів.</summary>
    public static class AccommodationService
    {
        /// <summary>Поселення студента в кімнату.</summary>
        public static void CheckIn(long studentId, long roomId, DateTime date, string contractNumber, string notes)
        {
            Db.InTransaction((c, tx) =>
            {
                var student = StudentRepository.Get(c, tx, studentId)
                              ?? throw new BusinessException("Студента не знайдено.");

                if (AccommodationRepository.GetActiveForStudent(c, tx, studentId) != null)
                    throw new BusinessException($"Студент {student.FullName} уже проживає в гуртожитку. Скористайтеся переселенням.");

                var room = RoomRepository.Get(c, tx, roomId) ?? throw new BusinessException("Кімнату не знайдено.");
                CheckRoom(room, student);

                if (date.Date > DateTime.Today)
                    throw new BusinessException("Дата поселення не може бути пізнішою за сьогоднішню.");

                Db.Execute(c, tx,
                    "INSERT INTO Accommodations (StudentId, RoomId, CheckInDate, ContractNumber, Notes) VALUES ($s, $r, $d, $n, $note);",
                    ("$s", studentId), ("$r", roomId), ("$d", date.Date), ("$n", contractNumber), ("$note", notes));
            });
        }

        /// <summary>Виселення студента.</summary>
        public static void CheckOut(long accommodationId, DateTime date, string notes)
        {
            Db.InTransaction((c, tx) =>
            {
                var acc = AccommodationRepository.Get(c, tx, accommodationId)
                          ?? throw new BusinessException("Запис не знайдено.");
                if (!acc.IsActive)
                    throw new BusinessException("Студента вже виселено.");
                if (date.Date < acc.CheckInDate)
                    throw new BusinessException("Дата виселення не може бути раніше дати поселення (" +
                                                acc.CheckInDate.ToString("dd.MM.yyyy") + ").");

                Db.Execute(c, tx,
                    "UPDATE Accommodations SET CheckOutDate = $d, Notes = COALESCE($note, Notes) WHERE Id = $id;",
                    ("$d", date.Date), ("$note", string.IsNullOrWhiteSpace(notes) ? null : notes), ("$id", accommodationId));
            });
        }

        /// <summary>Переселення: закриття поточного запису і створення нового в іншій кімнаті.</summary>
        public static void Relocate(long accommodationId, long newRoomId, DateTime date, string notes)
        {
            Db.InTransaction((c, tx) =>
            {
                var acc = AccommodationRepository.Get(c, tx, accommodationId)
                          ?? throw new BusinessException("Запис не знайдено.");
                if (!acc.IsActive)
                    throw new BusinessException("Студент не проживає в гуртожитку.");
                if (acc.RoomId == newRoomId)
                    throw new BusinessException("Оберіть іншу кімнату.");
                if (date.Date < acc.CheckInDate)
                    throw new BusinessException("Дата переселення не може бути раніше дати поселення.");
                if (date.Date > DateTime.Today)
                    throw new BusinessException("Дата переселення не може бути пізнішою за сьогоднішню.");

                var student = StudentRepository.Get(c, tx, acc.StudentId);
                var room = RoomRepository.Get(c, tx, newRoomId) ?? throw new BusinessException("Кімнату не знайдено.");
                CheckRoom(room, student);

                Db.Execute(c, tx, "UPDATE Accommodations SET CheckOutDate = $d WHERE Id = $id;",
                    ("$d", date.Date), ("$id", accommodationId));
                Db.Execute(c, tx,
                    "INSERT INTO Accommodations (StudentId, RoomId, CheckInDate, ContractNumber, Notes) VALUES ($s, $r, $d, $n, $note);",
                    ("$s", acc.StudentId), ("$r", newRoomId), ("$d", date.Date), ("$n", acc.ContractNumber),
                    ("$note", string.IsNullOrWhiteSpace(notes) ? "Переселення" : notes));
            });
        }

        /// <summary>Перевірка, чи можна поселити студента в кімнату.</summary>
        public static void CheckRoom(Room room, Student student)
        {
            if (room.Free <= 0)
                throw new BusinessException($"У кімнаті {room.Number} немає вільних місць.");
            if (!IsGenderCompatible(room, student))
                throw new BusinessException($"Кімната {room.Number} — {Room.GenderDisplay(room.Gender).ToLower()}; " +
                                            "стать студента не відповідає типу кімнати.");
        }

        public static bool IsGenderCompatible(Room room, Student student) =>
            string.IsNullOrEmpty(room.Gender) || room.Gender == student.Gender;
    }
}
