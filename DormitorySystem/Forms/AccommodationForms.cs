using System.ComponentModel;
using Dormitory.Data;
using Dormitory.Models;
using Dormitory.Services;
using Dormitory.UI;

namespace Dormitory.Forms
{
    /// <summary>Поселення студента або переселення в іншу кімнату.</summary>
    [DesignerCategory("Code")]
    public class CheckInForm : EditFormBase
    {
        private readonly Accommodation _relocateFrom;   // null — нове поселення
        private readonly ComboBox _student, _room;
        private readonly DateTimePicker _date;
        private readonly TextBox _contract, _notes;
        private readonly Label _roomInfo;
        private readonly List<Room> _rooms;

        /// <param name="relocateFrom">Поточне поселення (для режиму переселення).</param>
        /// <param name="preselectStudentId">Студент, якого вибрати одразу.</param>
        /// <param name="preselectRoomId">Кімната, яку вибрати одразу.</param>
        public CheckInForm(Accommodation relocateFrom = null, long? preselectStudentId = null, long? preselectRoomId = null)
            : base(relocateFrom == null ? "Поселення студента" : "Переселення студента",
                   relocateFrom == null ? "Поселити" : "Переселити")
        {
            _relocateFrom = relocateFrom;
            _rooms = RoomRepository.GetAll();

            Fields.ColumnStyles[1].Width = 400;   // ширші списки для довгих назв

            Student fixedStudent = null;
            if (relocateFrom == null)
            {
                AddHeader("У списку — лише студенти, які зараз не проживають у гуртожитку.\n" +
                          "Кімнати відфільтровано за статтю студента та наявністю вільних місць.");
            }
            else
            {
                fixedStudent = StudentRepository.Get(relocateFrom.StudentId);
                var oldRoom = _rooms.FirstOrDefault(r => r.Id == relocateFrom.RoomId);
                AddHeader($"Поточна кімната: № {oldRoom?.Number}, проживає з {relocateFrom.CheckInDate:dd.MM.yyyy}.");
            }

            _student = AddField("Студент *", CreateCombo(editable: true));
            _student.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            _student.AutoCompleteSource = AutoCompleteSource.ListItems;

            _room = AddField(relocateFrom == null ? "Кімната *" : "Нова кімната *", CreateCombo());
            _roomInfo = AddField("", new Label { AutoSize = true, ForeColor = Color.DimGray });
            _date = AddField(relocateFrom == null ? "Дата поселення *" : "Дата переселення *", CreateDatePicker(DateTime.Today));
            _contract = AddField("№ договору", new TextBox { MaxLength = 30 });
            _notes = AddField("Примітка", new TextBox { MaxLength = 200 });

            if (fixedStudent == null)
            {
                foreach (var s in StudentRepository.GetNotLiving())
                    _student.Items.Add(new ListItem(s.Id, $"{s.FullName} ({s.Faculty}, {s.Course} курс)", s));
                _contract.Text = AccommodationRepository.NextContractNumber();
            }
            else
            {
                _student.Items.Add(new ListItem(fixedStudent.Id, fixedStudent.FullName, fixedStudent));
                _student.Enabled = false;
                _contract.Text = relocateFrom.ContractNumber;
                _contract.ReadOnly = true;
            }

            _student.SelectedIndexChanged += (s, e) => FillRooms(preselectRoomId);
            _room.SelectedIndexChanged += (s, e) => ShowRoomInfo();

            var pre = _student.Items.Cast<ListItem>().FirstOrDefault(i => i.Id == preselectStudentId);
            if (pre != null) _student.SelectedItem = pre;
            else if (relocateFrom != null) _student.SelectedIndex = 0;
            else FillRooms(preselectRoomId);
        }

        private Student SelectedStudent => (_student.SelectedItem as ListItem)?.Tag as Student;

        private void FillRooms(long? preselectRoomId)
        {
            var student = SelectedStudent;
            var keep = (_room.SelectedItem as ListItem)?.Id ?? preselectRoomId;
            _room.Items.Clear();

            foreach (var r in _rooms)
            {
                if (r.Free <= 0) continue;
                if (_relocateFrom != null && r.Id == _relocateFrom.RoomId) continue;
                if (student != null && !AccommodationService.IsGenderCompatible(r, student)) continue;
                _room.Items.Add(new ListItem(r.Id,
                    $"№ {r.Number}  —  вільно {r.Free} з {r.Capacity}, {Room.GenderDisplay(r.Gender).ToLower()}, {r.PricePerMonth:N0} грн/міс", r));
            }

            var item = _room.Items.Cast<ListItem>().FirstOrDefault(i => i.Id == keep);
            if (item != null) _room.SelectedItem = item;
            else if (_room.Items.Count > 0) _room.SelectedIndex = 0;
            ShowRoomInfo();
        }

        private void ShowRoomInfo()
        {
            var room = (_room.SelectedItem as ListItem)?.Tag as Room;
            var student = SelectedStudent;
            if (room == null)
            {
                _roomInfo.Text = _room.Items.Count == 0 ? "Немає кімнат з вільними місцями." : "";
                return;
            }
            int benefit = student?.BenefitPercent ?? 0;
            double fee = BillingService.ApplyBenefit(room.PricePerMonth, benefit);
            _roomInfo.Text = $"Щомісячна плата: {fee:N2} грн" + (benefit > 0 ? $" (пільга {benefit}%)" : "");
        }

        protected override bool OnSave()
        {
            var student = SelectedStudent ?? throw new BusinessException("Оберіть студента зі списку.");
            var room = (_room.SelectedItem as ListItem)?.Tag as Room ?? throw new BusinessException("Оберіть кімнату.");

            if (_relocateFrom == null)
            {
                AccommodationService.CheckIn(student.Id, room.Id, _date.Value.Date,
                    string.IsNullOrWhiteSpace(_contract.Text) ? null : _contract.Text.Trim(), Optional(_notes));
                Ui.Info($"Студента {student.FullName} поселено в кімнату № {room.Number}.");
            }
            else
            {
                AccommodationService.Relocate(_relocateFrom.Id, room.Id, _date.Value.Date, Optional(_notes));
                Ui.Info($"Студента {student.FullName} переселено в кімнату № {room.Number}.");
            }
            return true;
        }
    }

    /// <summary>Виселення студента.</summary>
    [DesignerCategory("Code")]
    public class CheckOutForm : EditFormBase
    {
        private readonly Accommodation _acc;
        private readonly DateTimePicker _date;
        private readonly TextBox _reason;
        private readonly BalanceInfo _balance;

        public CheckOutForm(Accommodation acc) : base("Виселення студента", "Виселити")
        {
            _acc = acc;
            var student = StudentRepository.Get(acc.StudentId);
            var room = RoomRepository.Get(acc.RoomId);
            _balance = BillingService.Calculate(acc.StudentId);

            AddHeader($"Студент: {student.FullName}\nКімната: № {room?.Number}, проживає з {acc.CheckInDate:dd.MM.yyyy}");
            if (_balance.Debt > 0)
            {
                var warn = AddHeader($"Увага! Заборгованість з оплати: {_balance.Debt:N2} грн.");
                warn.ForeColor = Color.Firebrick;
                warn.Font = Ui.HeaderFont;
            }

            _date = AddField("Дата виселення *", CreateDatePicker(DateTime.Today));
            _reason = AddField("Причина / примітка", new TextBox { MaxLength = 200 });
        }

        protected override bool OnSave()
        {
            if (_balance.Debt > 0 &&
                !Ui.Confirm($"У студента є борг {_balance.Debt:N2} грн. Все одно оформити виселення?"))
                return false;

            AccommodationService.CheckOut(_acc.Id, _date.Value.Date, Optional(_reason));
            return true;
        }
    }
}
