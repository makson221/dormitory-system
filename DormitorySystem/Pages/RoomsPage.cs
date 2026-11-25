using System.ComponentModel;
using System.Data;
using Dormitory.Data;
using Dormitory.Forms;
using Dormitory.Services;
using Dormitory.UI;

namespace Dormitory.Pages
{
    /// <summary>Вкладка «Кімнати»: перелік кімнат і мешканці вибраної кімнати.</summary>
    [DesignerCategory("Code")]
    public class RoomsPage : PageBase
    {
        private readonly ToolStripButton _onlyFree;
        private readonly DataGridView _residents = Ui.CreateGrid();
        private readonly Label _residentsTitle;

        public override string Title => "Кімнати";

        public RoomsPage()
        {
            if (Session.IsAdmin)
                Tools.Items.Add(Ui.Button("Додати", (s, e) => Add()));
            Tools.Items.Add(Ui.Button("Редагувати", (s, e) => Edit()));
            if (Session.IsAdmin)
                Tools.Items.Add(Ui.Button("Видалити", (s, e) => Delete()));
            Tools.Items.Add(new ToolStripSeparator());
            Tools.Items.Add(Ui.Button("Поселити в кімнату", (s, e) => CheckIn()));
            Tools.Items.Add(new ToolStripSeparator());
            _onlyFree = new ToolStripButton("Лише з вільними місцями") { CheckOnClick = true };
            _onlyFree.CheckedChanged += (s, e) => RefreshData();
            Tools.Items.Add(_onlyFree);
            AddSearch();
            AddExportButton(() => "Кімнати гуртожитку");

            _residentsTitle = new Label
            {
                Dock = DockStyle.Top, Height = 28, Font = Ui.HeaderFont, ForeColor = Ui.Accent,
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(6, 0, 0, 0)
            };

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 6,
                BackColor = Color.FromArgb(220, 228, 238)
            };
            split.Panel1.Controls.Add(Grid);
            split.Panel2.Controls.Add(_residents);
            split.Panel2.Controls.Add(_residentsTitle);
            split.Panel1.BackColor = split.Panel2.BackColor = SystemColors.Window;
            Load += (s, e) =>
            {
                try { split.SplitterDistance = Math.Max(100, (int)(split.Height * 0.6)); }
                catch (Exception) { /* занадто мале вікно — залишаємо розмір за замовчуванням */ }
            };

            Grid.SelectionChanged += (s, e) => LoadResidents();
            Grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) Edit(); };
            Grid.CellFormatting += HighlightFree;
            BuildLayout(split);
        }

        protected override DataTable LoadTable() => RoomRepository.GetTable(_onlyFree.Checked);

        protected override void UpdateFooter()
        {
            base.UpdateFooter();
            var (total, occupied) = RoomRepository.GetCapacityStats();
            double pct = total == 0 ? 0 : 100.0 * occupied / total;
            Footer.Text += $"     Місць усього: {total}, зайнято: {occupied}, вільно: {total - occupied} (заповненість {pct:0.0}%)";
        }

        protected override void OnDataLoaded() => LoadResidents();

        private void LoadResidents()
        {
            var id = Ui.SelectedId(Grid);
            if (id == null)
            {
                _residents.DataSource = null;
                _residentsTitle.Text = "Мешканці кімнати";
                return;
            }
            Ui.Try(() =>
            {
                var t = RoomRepository.GetResidents(id.Value);
                _residents.DataSource = t;
                _residentsTitle.Text = $"Мешканці кімнати № {Ui.SelectedValue(Grid, "Кімната")}  ({t.Rows.Count})";
            });
        }

        private void HighlightFree(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || Grid.Columns[e.ColumnIndex].Name != "Вільно") return;
            if (e.Value is long free)
            {
                e.CellStyle.ForeColor = free > 0 ? Color.DarkGreen : Color.Firebrick;
                e.CellStyle.Font = Ui.HeaderFont;
            }
        }

        private void Add()
        {
            using var f = new RoomEditForm();
            if (f.ShowDialog(this) == DialogResult.OK) RefreshData();
        }

        private void Edit()
        {
            var id = RequireSelectedId();
            if (id == null) return;
            var room = RoomRepository.Get(id.Value);
            if (room == null) return;
            using var f = new RoomEditForm(room);
            if (f.ShowDialog(this) == DialogResult.OK) RefreshData();
        }

        private void Delete()
        {
            var id = RequireSelectedId();
            if (id == null) return;
            if (!Ui.Confirm($"Видалити кімнату № {Ui.SelectedValue(Grid, "Кімната")}?")) return;
            if (Ui.Try(() => RoomRepository.Delete(id.Value))) RefreshData();
        }

        private void CheckIn()
        {
            var id = RequireSelectedId();
            if (id == null) return;
            if (Ui.SelectedValue(Grid, "Вільно") is long free && free <= 0)
            {
                Ui.Info("У вибраній кімнаті немає вільних місць.");
                return;
            }
            using var f = new CheckInForm(preselectRoomId: id);
            if (f.ShowDialog(this) == DialogResult.OK) RefreshData();
        }
    }
}
