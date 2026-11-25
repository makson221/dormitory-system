using System.ComponentModel;
using System.Data;
using Dormitory.Data;
using Dormitory.Forms;
using Dormitory.Models;
using Dormitory.Services;
using Dormitory.UI;

namespace Dormitory.Pages
{
    /// <summary>Вкладка «Оплата»: журнал платежів за період.</summary>
    [DesignerCategory("Code")]
    public class PaymentsPage : PageBase
    {
        private readonly DateTimePicker _from, _to;

        public override string Title => "Оплата";

        public PaymentsPage()
        {
            Tools.Items.Add(Ui.Button("Внести платіж", (s, e) => Add()));
            if (Session.IsAdmin)
                Tools.Items.Add(Ui.Button("Видалити (сторно)", (s, e) => Delete(), "Видалити помилково внесений платіж"));
            Tools.Items.Add(new ToolStripSeparator());

            var today = DateTime.Today;
            var yearStart = new DateTime(today.Month >= 9 ? today.Year : today.Year - 1, 9, 1);
            _from = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 110, Value = yearStart };
            _to = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 110, Value = today };
            _from.ValueChanged += (s, e) => RefreshData();
            _to.ValueChanged += (s, e) => RefreshData();

            Tools.Items.Add(new ToolStripLabel("Період з"));
            Tools.Items.Add(new ToolStripControlHost(_from));
            Tools.Items.Add(new ToolStripLabel("по"));
            Tools.Items.Add(new ToolStripControlHost(_to));
            Tools.Items.Add(Ui.Button("Поточний місяць", (s, e) =>
            {
                _from.Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                _to.Value = DateTime.Today;
            }));
            AddSearch();
            AddExportButton(() => $"Платежі {_from.Value:dd.MM.yyyy}-{_to.Value:dd.MM.yyyy}", () => Footer.Text);

            BuildLayout();
        }

        protected override DataTable LoadTable()
        {
            if (_from.Value.Date > _to.Value.Date)
                throw new BusinessException("Початок періоду не може бути пізніше за кінець.");
            return PaymentRepository.GetTable(_from.Value, _to.Value);
        }

        protected override void UpdateFooter()
        {
            base.UpdateFooter();
            if (Table == null) return;
            double sum = Table.DefaultView.Cast<DataRowView>().Sum(r => Convert.ToDouble(r["Сума, грн"]));
            Footer.Text += $"     Сума: {sum:N2} грн";
        }

        private void Add()
        {
            using var f = new PaymentEditForm();
            if (f.ShowDialog(this) == DialogResult.OK) RefreshData();
        }

        private void Delete()
        {
            var id = RequireSelectedId();
            if (id == null) return;
            if (!Ui.Confirm($"Видалити платіж на суму {Ui.SelectedValue(Grid, "Сума, грн"):N2} грн " +
                            $"(«{Ui.SelectedValue(Grid, "ПІБ")}»)?\nОперацію неможливо скасувати."))
                return;
            if (Ui.Try(() => PaymentRepository.Delete(id.Value))) RefreshData();
        }
    }
}
