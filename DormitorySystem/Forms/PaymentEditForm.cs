using System.ComponentModel;
using Dormitory.Data;
using Dormitory.Models;
using Dormitory.Services;
using Dormitory.UI;

namespace Dormitory.Forms
{
    /// <summary>Внесення платежу за проживання.</summary>
    [DesignerCategory("Code")]
    public class PaymentEditForm : EditFormBase
    {
        private readonly ComboBox _student;
        private readonly Label _balanceInfo;
        private readonly NumericUpDown _amount;
        private readonly DateTimePicker _date, _period;
        private readonly TextBox _receipt, _comment;
        private Dictionary<long, BalanceInfo> _balances;

        public PaymentEditForm(long? studentId = null) : base("Внесення оплати за проживання", "Провести платіж")
        {
            Fields.ColumnStyles[1].Width = 360;
            _balances = BillingService.CalculateAll();

            _student = AddField("Студент *", CreateCombo(editable: true));
            _student.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            _student.AutoCompleteSource = AutoCompleteSource.ListItems;

            _balanceInfo = AddField("", new Label { AutoSize = true, ForeColor = Color.DimGray, MaximumSize = new Size(360, 0) });
            _amount = AddField("Сума, грн *", new NumericUpDown
            {
                Minimum = 0, Maximum = 1_000_000, DecimalPlaces = 2, Increment = 100, ThousandsSeparator = true, Width = 140
            });
            _date = AddField("Дата оплати *", CreateDatePicker(DateTime.Today));
            _period = AddField("За місяць *", new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "MMMM yyyy",
                ShowUpDown = true,
                Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
                Width = 160
            });
            _receipt = AddField("№ квитанції", new TextBox { MaxLength = 30, Text = PaymentRepository.NextReceiptNumber() });
            _comment = AddField("Коментар", new TextBox { MaxLength = 200 });

            // Спочатку ті, хто проживає, потім інші (наприклад, виселені боржники)
            var ordered = StudentRepository.GetAll()
                .OrderBy(s => _balances.TryGetValue(s.Id, out var b) && b.CurrentRoom != null ? 0 : 1)
                .ThenBy(s => s.FullName);
            foreach (var s in ordered)
            {
                _balances.TryGetValue(s.Id, out var b);
                string room = b?.CurrentRoom != null ? $"кімн. {b.CurrentRoom}" : "не проживає";
                _student.Items.Add(new ListItem(s.Id, $"{s.FullName} ({room})", s));
            }

            _student.SelectedIndexChanged += (s, e) => OnStudentChanged();
            var pre = _student.Items.Cast<ListItem>().FirstOrDefault(i => i.Id == studentId);
            if (pre != null) _student.SelectedItem = pre;
        }

        private Student SelectedStudent => (_student.SelectedItem as ListItem)?.Tag as Student;

        private void OnStudentChanged()
        {
            var s = SelectedStudent;
            if (s == null || !_balances.TryGetValue(s.Id, out var b))
            {
                _balanceInfo.Text = "";
                return;
            }

            _balanceInfo.Text =
                $"Щомісячна плата: {b.MonthlyFee:N2} грн" + (b.BenefitPercent > 0 ? $" (пільга {b.BenefitPercent}%)" : "") +
                $"\nНараховано: {b.Charged:N2} грн;  сплачено: {b.Paid:N2} грн" +
                (b.Debt > 0 ? $"\nЗаборгованість: {b.Debt:N2} грн"
                            : b.Debt < 0 ? $"\nПереплата: {-b.Debt:N2} грн" : "\nЗаборгованості немає");
            _balanceInfo.ForeColor = b.Debt > 0 ? Color.Firebrick : Color.DarkGreen;

            // Пропонуємо суму: борг (якщо є) або щомісячну плату
            decimal suggested = (decimal)(b.Debt > 0 ? b.Debt : b.MonthlyFee);
            _amount.Value = Math.Min(_amount.Maximum, Math.Max(0, suggested));
        }

        protected override bool OnSave()
        {
            var s = SelectedStudent ?? throw new BusinessException("Оберіть студента зі списку.");
            if (_amount.Value <= 0)
                throw new BusinessException("Сума платежу повинна бути більшою за нуль.");
            if (_date.Value.Date > DateTime.Today)
                throw new BusinessException("Дата оплати не може бути в майбутньому.");

            var payment = new Payment
            {
                StudentId = s.Id,
                Amount = (double)_amount.Value,
                PaymentDate = _date.Value.Date,
                Period = _period.Value.ToString("yyyy-MM"),
                ReceiptNumber = Optional(_receipt),
                UserId = Session.CurrentUser?.Id,
                Comment = Optional(_comment)
            };
            PaymentRepository.Insert(payment);
            return true;
        }
    }
}
