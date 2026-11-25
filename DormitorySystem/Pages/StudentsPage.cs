using System.ComponentModel;
using System.Data;
using Dormitory.Data;
using Dormitory.Forms;
using Dormitory.Services;
using Dormitory.UI;

namespace Dormitory.Pages
{
    /// <summary>Вкладка «Студенти».</summary>
    [DesignerCategory("Code")]
    public class StudentsPage : PageBase
    {
        private readonly ToolStripComboBox _filter;

        public override string Title => "Студенти";

        public StudentsPage()
        {
            Tools.Items.Add(Ui.Button("Додати", (s, e) => Add()));
            Tools.Items.Add(Ui.Button("Редагувати", (s, e) => Edit()));
            if (Session.IsAdmin)
                Tools.Items.Add(Ui.Button("Видалити", (s, e) => Delete()));
            Tools.Items.Add(new ToolStripSeparator());
            Tools.Items.Add(Ui.Button("Поселити", (s, e) => CheckIn(), "Поселити вибраного студента"));
            Tools.Items.Add(Ui.Button("Внести оплату", (s, e) => Pay(), "Внести платіж для вибраного студента"));
            Tools.Items.Add(new ToolStripSeparator());
            Tools.Items.Add(new ToolStripLabel("Показати:"));
            _filter = new ToolStripComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
            _filter.Items.AddRange(new object[] { "усіх", "хто проживає", "хто не проживає" });
            _filter.SelectedIndex = 0;
            _filter.SelectedIndexChanged += (s, e) => RefreshData();
            Tools.Items.Add(_filter);
            AddSearch();
            AddExportButton(() => "Студенти");

            Grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) Edit(); };
            Grid.CellFormatting += HighlightDebt;
            BuildLayout();
        }

        protected override DataTable LoadTable()
        {
            bool? living = _filter.SelectedIndex == 1 ? true : _filter.SelectedIndex == 2 ? false : (bool?)null;
            return StudentRepository.GetTable(living);
        }

        protected override void UpdateFooter()
        {
            base.UpdateFooter();
            if (Table == null) return;
            double debt = Table.DefaultView.Cast<DataRowView>().Sum(r => (double)r["Борг, грн"]);
            int debtors = Table.DefaultView.Cast<DataRowView>().Count(r => (double)r["Борг, грн"] > 0);
            Footer.Text += $"     Боржників: {debtors}, загальний борг: {debt:N2} грн";
        }

        private void HighlightDebt(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || Grid.Columns[e.ColumnIndex].Name != "Борг, грн") return;
            if (e.Value is double d && d > 0)
            {
                e.CellStyle.ForeColor = Color.Firebrick;
                e.CellStyle.Font = Ui.HeaderFont;
            }
        }

        private void Add()
        {
            using var f = new StudentEditForm();
            if (f.ShowDialog(this) == DialogResult.OK) RefreshData();
        }

        private void Edit()
        {
            var id = RequireSelectedId();
            if (id == null) return;
            var student = StudentRepository.Get(id.Value);
            if (student == null) return;
            using var f = new StudentEditForm(student);
            if (f.ShowDialog(this) == DialogResult.OK) RefreshData();
        }

        private void Delete()
        {
            var id = RequireSelectedId();
            if (id == null) return;
            var name = Ui.SelectedValue(Grid, "ПІБ");
            if (!Ui.Confirm($"Видалити студента «{name}»?")) return;
            if (Ui.Try(() => StudentRepository.Delete(id.Value))) RefreshData();
        }

        private void CheckIn()
        {
            var id = RequireSelectedId();
            if (id == null) return;
            if (Ui.SelectedValue(Grid, "Кімната") != null)
            {
                Ui.Info("Цей студент уже проживає в гуртожитку. Для зміни кімнати використайте «Переселити» на вкладці «Поселення».");
                return;
            }
            using var f = new CheckInForm(preselectStudentId: id);
            if (f.ShowDialog(this) == DialogResult.OK) RefreshData();
        }

        private void Pay()
        {
            var id = RequireSelectedId();
            if (id == null) return;
            using var f = new PaymentEditForm(id);
            if (f.ShowDialog(this) == DialogResult.OK) RefreshData();
        }
    }
}
