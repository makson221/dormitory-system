using System.ComponentModel;
using Dormitory.Data;
using Dormitory.Models;
using Dormitory.UI;

namespace Dormitory.Forms
{
    /// <summary>Додавання / редагування кімнати.</summary>
    [DesignerCategory("Code")]
    public class RoomEditForm : EditFormBase
    {
        private static readonly string[] GenderCodes = { "", "Ч", "Ж" };

        private readonly Room _room;
        private readonly TextBox _number, _notes;
        private readonly NumericUpDown _floor, _capacity, _price;
        private readonly ComboBox _gender;

        public RoomEditForm(Room room = null) : base(room == null ? "Нова кімната" : "Редагування кімнати")
        {
            _room = room ?? new Room { Floor = 1, Capacity = 3, PricePerMonth = 750 };

            _number = AddField("Номер кімнати *", new TextBox { MaxLength = 10 });
            _floor = AddField("Поверх *", new NumericUpDown { Minimum = 1, Maximum = 30, Width = 80 });
            _capacity = AddField("Кількість місць *", new NumericUpDown { Minimum = 1, Maximum = 10, Width = 80 });
            _gender = AddField("Тип кімнати", CreateCombo());
            _gender.Items.AddRange(new object[] { "Будь-яка", "Чоловіча", "Жіноча" });
            _price = AddField("Ціна за місяць, грн *", new NumericUpDown
            {
                Minimum = 0, Maximum = 100000, DecimalPlaces = 2, Increment = 50, ThousandsSeparator = true, Width = 120
            });
            _notes = AddField("Примітка", new TextBox { MaxLength = 200, Multiline = true, Height = 50 });

            if (_room.Id != 0 && _room.Occupied > 0)
                AddHeader($"Зараз у кімнаті проживає: {_room.Occupied}.");

            _number.Text = _room.Number;
            _floor.Value = _room.Floor;
            _capacity.Value = _room.Capacity;
            _gender.SelectedIndex = Math.Max(0, Array.IndexOf(GenderCodes, _room.Gender ?? ""));
            _price.Value = (decimal)_room.PricePerMonth;
            _notes.Text = _room.Notes;
        }

        protected override bool OnSave()
        {
            var r = _room;
            string number = Required(_number, "Номер кімнати");
            int capacity = (int)_capacity.Value;
            string gender = GenderCodes[Math.Max(0, _gender.SelectedIndex)];

            if (RoomRepository.NumberExists(number, r.Id))
                throw new BusinessException($"Кімната з номером {number} вже існує.");

            if (r.Id != 0)
            {
                var current = RoomRepository.Get(r.Id);
                if (current != null && capacity < current.Occupied)
                    throw new BusinessException($"У кімнаті проживає {current.Occupied} студ. — кількість місць не може бути меншою.");

                if (!string.IsNullOrEmpty(gender) && gender != (current?.Gender ?? "") &&
                    Db.ScalarLong(@"SELECT COUNT(*) FROM Accommodations a JOIN Students s ON s.Id = a.StudentId
                                    WHERE a.RoomId = $r AND a.CheckOutDate IS NULL AND s.Gender <> $g;",
                                  ("$r", r.Id), ("$g", gender)) > 0)
                    throw new BusinessException("Неможливо змінити тип кімнати: у ній проживають студенти іншої статі.");
            }

            r.Number = number;
            r.Floor = (int)_floor.Value;
            r.Capacity = capacity;
            r.Gender = gender;
            r.PricePerMonth = (double)_price.Value;
            r.Notes = Optional(_notes);

            if (r.Id == 0)
                r.Id = RoomRepository.Insert(r);
            else
                RoomRepository.Update(r);
            return true;
        }
    }
}
