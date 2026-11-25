using System.ComponentModel;
using System.Data;
using Dormitory.Services;
using Dormitory.UI;

namespace Dormitory.Pages
{
    /// <summary>Вкладка «Звіти».</summary>
    [DesignerCategory("Code")]
    public class ReportsPage : PageBase
    {
        private readonly ToolStripComboBox _report;
        private readonly DateTimePicker _from, _to;
        private readonly ToolStripItem[] _periodItems;
        private readonly Label _title;
        private ReportResult _result;

        public override string Title => "Звіти";

        public ReportsPage()
        {
            Tools.Items.Add(new ToolStripLabel("Звіт:"));
            _report = new ToolStripComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 340 };
            _report.Items.AddRange(ReportService.ReportNames);
            Tools.Items.Add(_report);

            var today = DateTime.Today;
            _from = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 110,
                                         Value = new DateTime(today.Month >= 9 ? today.Year : today.Year - 1, 9, 1) };
            _to = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 110, Value = today };
            _periodItems = new ToolStripItem[]
            {
                new ToolStripLabel("з"), new ToolStripControlHost(_from),
                new ToolStripLabel("по"), new ToolStripControlHost(_to)
            };
            Tools.Items.AddRange(_periodItems);
            Tools.Items.Add(Ui.Button("Сформувати", (s, e) => RefreshData()));
            AddSearch();
            AddExportButton(() => _result?.Title ?? "Звіт", () => _result?.Summary);

            _title = new Label
            {
                Dock = DockStyle.Top, Height = 36, Font = Ui.TitleFont, ForeColor = Ui.Accent,
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0)
            };

            var panel = new Panel { Dock = DockStyle.Fill };
            panel.Controls.Add(Grid);
            panel.Controls.Add(_title);
            BuildLayout(panel);

            Footer.Font = Ui.HeaderFont;
            _report.SelectedIndexChanged += (s, e) =>
            {
                foreach (var item in _periodItems) item.Visible = ReportService.NeedsPeriod(_report.SelectedIndex);
                RefreshData();
            };
            _report.SelectedIndex = 0;
        }

        protected override DataTable LoadTable()
        {
            if (_from.Value.Date > _to.Value.Date)
                throw new Models.BusinessException("Початок періоду не може бути пізніше за кінець.");
            _result = ReportService.Build(Math.Max(0, _report.SelectedIndex), _from.Value.Date, _to.Value.Date);
            _title.Text = _result.Title;
            return _result.Table;
        }

        protected override void UpdateFooter()
        {
            Footer.Text = _result?.Summary ?? "";
        }
    }
}
