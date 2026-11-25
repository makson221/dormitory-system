using System.ComponentModel;
using System.Data;
using Dormitory.Data;
using Dormitory.Forms;
using Dormitory.UI;

namespace Dormitory.Pages
{
    /// <summary>Вкладка «Поселення»: поселення, виселення, переселення, архів.</summary>
    [DesignerCategory("Code")]
    public class AccommodationsPage : PageBase
    {
        private readonly ToolStripButton _archive;

        public override string Title => "Поселення";

        public AccommodationsPage()
        {
            Tools.Items.Add(Ui.Button("Поселити", (s, e) => CheckIn()));
            Tools.Items.Add(Ui.Button("Виселити", (s, e) => CheckOut()));
            Tools.Items.Add(Ui.Button("Переселити", (s, e) => Relocate()));
            Tools.Items.Add(new ToolStripSeparator());
            _archive = new ToolStripButton("Показати архів (виселених)") { CheckOnClick = true };
            _archive.CheckedChanged += (s, e) => RefreshData();
            Tools.Items.Add(_archive);
            AddSearch();
            AddExportButton(() => _archive.Checked ? "Історія поселень" : "Поточні мешканці");

            Grid.CellFormatting += (s, e) =>
            {
                if (e.RowIndex >= 0 && Grid.Rows[e.RowIndex].DataBoundItem is DataRowView v &&
                    (string)v["Статус"] == "Виселений")
                    e.CellStyle.ForeColor = Color.Gray;
            };
            BuildLayout();
        }

        protected override DataTable LoadTable() => AccommodationRepository.GetTable(_archive.Checked);

        /// <summary>Вибраний активний запис поселення (або null з підказкою).</summary>
        private Models.Accommodation SelectedActive()
        {
            var id = RequireSelectedId();
            if (id == null) return null;
            var acc = AccommodationRepository.Get(id.Value);
            if (acc == null || !acc.IsActive)
            {
                Ui.Info("Вибраний студент уже виселений.");
                return null;
            }
            return acc;
        }

        private void CheckIn()
        {
            using var f = new CheckInForm();
            if (f.ShowDialog(this) == DialogResult.OK) RefreshData();
        }

        private void CheckOut()
        {
            var acc = SelectedActive();
            if (acc == null) return;
            using var f = new CheckOutForm(acc);
            if (f.ShowDialog(this) == DialogResult.OK) RefreshData();
        }

        private void Relocate()
        {
            var acc = SelectedActive();
            if (acc == null) return;
            using var f = new CheckInForm(relocateFrom: acc);
            if (f.ShowDialog(this) == DialogResult.OK) RefreshData();
        }
    }
}
