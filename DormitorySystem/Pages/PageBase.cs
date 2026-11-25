using System.ComponentModel;
using System.Data;
using Dormitory.UI;

namespace Dormitory.Pages
{
    /// <summary>
    /// Базова сторінка (вкладка головного вікна): панель інструментів, таблиця, рядок підсумків, пошук.
    /// </summary>
    [DesignerCategory("Code")]
    public abstract class PageBase : UserControl
    {
        protected readonly ToolStrip Tools = Ui.CreateToolStrip();
        protected readonly DataGridView Grid = Ui.CreateGrid();
        protected readonly Label Footer = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 28,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0),
            BackColor = Color.FromArgb(248, 250, 253),
            ForeColor = Color.FromArgb(60, 60, 60)
        };
        protected ToolStripTextBox SearchBox;
        protected DataTable Table;

        /// <summary>Назва вкладки.</summary>
        public abstract string Title { get; }

        protected PageBase()
        {
            Dock = DockStyle.Fill;
            Font = Ui.BaseFont;
        }

        /// <summary>Розміщує елементи сторінки. center — основний вміст (за замовчуванням таблиця).</summary>
        protected void BuildLayout(Control center = null)
        {
            Controls.Add(center ?? Grid);  // Fill — додається першим
            Controls.Add(Footer);
            Controls.Add(Tools);
        }

        protected void AddSearch() => SearchBox = Ui.AddSearchBox(Tools, ApplySearch);

        protected void AddExportButton(Func<string> title, Func<string> summary = null)
        {
            var b = Ui.Button("Експорт…", (s, e) => Ui.ExportGrid(Grid, title(), summary?.Invoke()),
                "Зберегти таблицю в CSV (Excel) або HTML (для друку)");
            b.Alignment = ToolStripItemAlignment.Right;
            Tools.Items.Add(b);
        }

        /// <summary>Перезавантажує дані з бази.</summary>
        public void RefreshData()
        {
            Ui.Try(() =>
            {
                Table = LoadTable();
                Ui.Rebind(Grid, Table, Ui.BuildSearchFilter(Table, SearchBox?.Text));
                UpdateFooter();
                OnDataLoaded();
            });
        }

        protected abstract DataTable LoadTable();

        protected virtual void OnDataLoaded() { }

        protected virtual void UpdateFooter()
        {
            if (Table != null)
                Footer.Text = $"Записів: {Table.DefaultView.Count}" +
                              (Table.DefaultView.Count != Table.Rows.Count ? $" (з {Table.Rows.Count})" : "");
        }

        private void ApplySearch()
        {
            if (Table == null) return;
            try
            {
                Table.DefaultView.RowFilter = Ui.BuildSearchFilter(Table, SearchBox.Text);
            }
            catch (Exception)
            {
                Table.DefaultView.RowFilter = "";
            }
            UpdateFooter();
        }

        /// <summary>Id вибраного рядка; показує підказку, якщо нічого не вибрано.</summary>
        protected long? RequireSelectedId(string column = "Id")
        {
            var id = Ui.SelectedId(Grid, column);
            if (id == null) Ui.Info("Спочатку виберіть запис у таблиці.");
            return id;
        }
    }
}
