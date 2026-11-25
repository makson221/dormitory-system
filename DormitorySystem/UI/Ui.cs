using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Net;
using System.Text;
using Dormitory.Models;

namespace Dormitory.UI
{
    /// <summary>Допоміжні методи інтерфейсу: стиль таблиць, повідомлення, пошук, експорт.</summary>
    public static class Ui
    {
        public static readonly Color Accent = Color.FromArgb(37, 84, 140);
        public static readonly Color AccentLight = Color.FromArgb(236, 242, 250);
        public static readonly Font BaseFont = new Font("Segoe UI", 9.75F);
        public static readonly Font HeaderFont = new Font("Segoe UI", 9.75F, FontStyle.Bold);
        public static readonly Font TitleFont = new Font("Segoe UI Semibold", 14F);

        // ------------------------------------------------------------------
        //  Повідомлення
        // ------------------------------------------------------------------

        public static void Info(string text) =>
            MessageBox.Show(text, "Інформація", MessageBoxButtons.OK, MessageBoxIcon.Information);

        public static void Warn(string text) =>
            MessageBox.Show(text, "Увага", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        public static void Error(string text) =>
            MessageBox.Show(text, "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);

        public static bool Confirm(string text) =>
            MessageBox.Show(text, "Підтвердження", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

        /// <summary>Виконує дію, показуючи зрозуміле повідомлення у разі помилки. Повертає true при успіху.</summary>
        public static bool Try(Action action)
        {
            try
            {
                action();
                return true;
            }
            catch (BusinessException ex)
            {
                Warn(ex.Message);
            }
            catch (OperationCanceledException)
            {
                // форма сама показала повідомлення — нічого не робимо
            }
            catch (Exception ex) when (Dormitory.Data.Db.IsConstraintError(ex))
            {
                Warn("Операцію не виконано: порушено обмеження цілісності даних.\n" + ex.Message);
            }
            catch (Exception ex)
            {
                Error(ex.Message);
            }
            return false;
        }

        // ------------------------------------------------------------------
        //  Таблиці
        // ------------------------------------------------------------------

        public static DataGridView CreateGrid()
        {
            var g = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                EnableHeadersVisualStyles = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                Font = BaseFont
            };
            g.ColumnHeadersDefaultCellStyle.BackColor = Accent;
            g.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            g.ColumnHeadersDefaultCellStyle.Font = HeaderFont;
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(4);
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Accent;
            g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(70, 120, 185);
            g.DefaultCellStyle.SelectionForeColor = Color.White;
            g.AlternatingRowsDefaultCellStyle.BackColor = AccentLight;
            g.RowTemplate.Height = 26;
            g.DataBindingComplete += (s, e) => FormatColumns(g);
            return g;
        }

        private static void FormatColumns(DataGridView g)
        {
            foreach (DataGridViewColumn c in g.Columns)
            {
                if (IsTechnicalColumn(c.Name))
                {
                    c.Visible = false;
                }
                else if (c.ValueType == typeof(DateTime))
                {
                    c.DefaultCellStyle.Format = "dd.MM.yyyy";
                    c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                }
                else if (c.ValueType == typeof(double))
                {
                    c.DefaultCellStyle.Format = "N2";
                    c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                }
                else if (c.ValueType == typeof(long) || c.ValueType == typeof(int))
                {
                    c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                }
            }
        }

        public static bool IsTechnicalColumn(string name) => name == "Id" || name.EndsWith("Id", StringComparison.Ordinal);

        /// <summary>Значення стовпця вибраного рядка (або null).</summary>
        public static object SelectedValue(DataGridView g, string column)
        {
            if (g.CurrentRow?.DataBoundItem is DataRowView row && row.Row.Table.Columns.Contains(column))
            {
                var v = row[column];
                return v == DBNull.Value ? null : v;
            }
            return null;
        }

        public static long? SelectedId(DataGridView g, string column = "Id")
        {
            var v = SelectedValue(g, column);
            return v == null ? (long?)null : Convert.ToInt64(v);
        }

        /// <summary>Повторно прив'язує таблицю, зберігаючи виділений рядок за Id.</summary>
        public static void Rebind(DataGridView g, DataTable table, string filter = null)
        {
            var selected = SelectedId(g);
            int firstRow = g.FirstDisplayedScrollingRowIndex;
            if (filter != null) table.DefaultView.RowFilter = filter;
            g.DataSource = table;

            if (selected != null && table.Columns.Contains("Id"))
            {
                foreach (DataGridViewRow r in g.Rows)
                {
                    if (r.DataBoundItem is DataRowView v && Convert.ToInt64(v["Id"]) == selected.Value)
                    {
                        var cell = r.Cells.Cast<DataGridViewCell>().FirstOrDefault(c => c.Visible);
                        if (cell != null) g.CurrentCell = cell;
                        break;
                    }
                }
            }
            if (firstRow >= 0 && firstRow < g.RowCount)
            {
                try { g.FirstDisplayedScrollingRowIndex = firstRow; } catch (InvalidOperationException) { }
            }
        }

        /// <summary>Будує вираз RowFilter для пошуку тексту в усіх видимих стовпцях.</summary>
        public static string BuildSearchFilter(DataTable table, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            var words = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var parts = new List<string>();
            foreach (var word in words)
            {
                string w = EscapeLike(word);
                var cols = table.Columns.Cast<DataColumn>()
                    .Where(c => !IsTechnicalColumn(c.ColumnName))
                    .Select(c => c.DataType == typeof(string)
                        ? $"[{c.ColumnName}] LIKE '%{w}%'"
                        : $"CONVERT([{c.ColumnName}], 'System.String') LIKE '%{w}%'");
                parts.Add("(" + string.Join(" OR ", cols) + ")");
            }
            return string.Join(" AND ", parts);
        }

        private static string EscapeLike(string s)
        {
            var sb = new StringBuilder();
            foreach (char ch in s)
            {
                if (ch == '*' || ch == '%' || ch == '[' || ch == ']') sb.Append('[').Append(ch).Append(']');
                else if (ch == '\'') sb.Append("''");
                else sb.Append(ch);
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------
        //  Панель інструментів
        // ------------------------------------------------------------------

        public static ToolStrip CreateToolStrip()
        {
            return new ToolStrip
            {
                GripStyle = ToolStripGripStyle.Hidden,
                Padding = new Padding(6, 4, 6, 4),
                Font = BaseFont,
                BackColor = Color.FromArgb(248, 250, 253),
                RenderMode = ToolStripRenderMode.System
            };
        }

        public static ToolStripButton Button(string text, EventHandler onClick, string tooltip = null)
        {
            var b = new ToolStripButton(text) { DisplayStyle = ToolStripItemDisplayStyle.Text, ToolTipText = tooltip ?? text };
            b.Click += onClick;
            return b;
        }

        /// <summary>Поле пошуку з міткою.</summary>
        public static ToolStripTextBox AddSearchBox(ToolStrip ts, Action onChanged)
        {
            var box = new ToolStripTextBox { Width = 220, BorderStyle = BorderStyle.FixedSingle, ToolTipText = "Пошук за будь-яким полем" };
            box.TextChanged += (s, e) => onChanged();
            ts.Items.Add(new ToolStripSeparator());
            ts.Items.Add(new ToolStripLabel("Пошук:"));
            ts.Items.Add(box);
            return box;
        }

        // ------------------------------------------------------------------
        //  Експорт
        // ------------------------------------------------------------------

        /// <summary>Експортує видимі рядки й стовпці таблиці в CSV (для Excel) або HTML (для друку).</summary>
        public static void ExportGrid(DataGridView grid, string title, string summary = null)
        {
            if (grid.Rows.Count == 0)
            {
                Info("Немає даних для експорту.");
                return;
            }

            using var dlg = new SaveFileDialog
            {
                Title = "Експорт даних",
                Filter = "Таблиця CSV для Excel (*.csv)|*.csv|Сторінка HTML для друку (*.html)|*.html",
                FileName = MakeFileName(title),
                AddExtension = true
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            Try(() =>
            {
                var cols = grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).OrderBy(c => c.DisplayIndex).ToList();
                bool html = dlg.FilterIndex == 2 || dlg.FileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase);
                string content = html ? BuildHtml(grid, cols, title, summary) : BuildCsv(grid, cols);
                File.WriteAllText(dlg.FileName, content, new UTF8Encoding(true));

                if (Confirm("Файл збережено:\n" + dlg.FileName + "\n\nВідкрити його зараз?"))
                    Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            });
        }

        private static string MakeFileName(string title)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var clean = new string(title.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
            return clean + "_" + DateTime.Now.ToString("yyyy-MM-dd");
        }

        private static string CellText(DataGridViewRow row, DataGridViewColumn col) =>
            Convert.ToString(row.Cells[col.Index].FormattedValue) ?? "";

        private static string BuildCsv(DataGridView grid, List<DataGridViewColumn> cols)
        {
            static string Q(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(";", cols.Select(c => Q(c.HeaderText))));
            foreach (DataGridViewRow row in grid.Rows)
                sb.AppendLine(string.Join(";", cols.Select(c => Q(CellText(row, c)))));
            return sb.ToString();
        }

        private static string BuildHtml(DataGridView grid, List<DataGridViewColumn> cols, string title, string summary)
        {
            string E(string s) => WebUtility.HtmlEncode(s);
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html lang=\"uk\"><head><meta charset=\"utf-8\">");
            sb.AppendLine($"<title>{E(title)}</title><style>");
            sb.AppendLine("body{font-family:'Segoe UI',Arial,sans-serif;margin:24px;color:#222}");
            sb.AppendLine("h1{font-size:20px;margin:0 0 4px}.meta{color:#666;font-size:12px;margin-bottom:12px}");
            sb.AppendLine("table{border-collapse:collapse;width:100%;font-size:13px}");
            sb.AppendLine("th{background:#25548c;color:#fff;text-align:left;padding:6px}td{padding:5px 6px;border-bottom:1px solid #ddd}");
            sb.AppendLine("tr:nth-child(even) td{background:#f2f6fb}.sum{margin-top:12px;font-weight:600}");
            sb.AppendLine("@media print{body{margin:0}}</style></head><body>");
            sb.AppendLine("<div class=\"meta\">Автоматизована система студентського гуртожитку</div>");
            sb.AppendLine($"<h1>{E(title)}</h1>");
            sb.AppendLine($"<div class=\"meta\">Сформовано: {DateTime.Now:dd.MM.yyyy HH:mm}; " +
                          $"користувач: {E(Services.Session.CurrentUser?.FullName ?? "")}</div>");
            sb.AppendLine("<table><thead><tr><th>№</th>");
            foreach (var c in cols) sb.Append("<th>").Append(E(c.HeaderText)).Append("</th>");
            sb.AppendLine("</tr></thead><tbody>");
            int n = 1;
            foreach (DataGridViewRow row in grid.Rows)
            {
                sb.Append("<tr><td>").Append(n++).Append("</td>");
                foreach (var c in cols) sb.Append("<td>").Append(E(CellText(row, c))).Append("</td>");
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</tbody></table>");
            if (!string.IsNullOrEmpty(summary)) sb.AppendLine($"<div class=\"sum\">{E(summary)}</div>");
            sb.AppendLine("</body></html>");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Базова форма-діалог для редагування: поля розташовуються у дві колонки (підпис — елемент),
    /// унизу кнопки «Зберегти» / «Скасувати». Форми створюються кодом, без дизайнера.
    /// </summary>
    [DesignerCategory("Code")]
    public class EditFormBase : Form
    {
        protected readonly TableLayoutPanel Fields;
        protected readonly Button BtnOk;
        protected readonly Button BtnCancel;
        private readonly TableLayoutPanel _outer;

        public EditFormBase(string title, string okText = "Зберегти")
        {
            Text = title;
            Font = Ui.BaseFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            _outer = new TableLayoutPanel
            {
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Location = new Point(0, 0),
                Padding = new Padding(14, 12, 14, 10)
            };

            Fields = new TableLayoutPanel
            {
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill
            };
            Fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            Fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));

            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 10, 0, 0)
            };
            BtnCancel = new Button { Text = "Скасувати", AutoSize = true, MinimumSize = new Size(100, 30), DialogResult = DialogResult.Cancel };
            BtnOk = new Button
            {
                Text = okText, AutoSize = true, MinimumSize = new Size(100, 30),
                BackColor = Ui.Accent, ForeColor = Color.White, FlatStyle = FlatStyle.Flat
            };
            BtnOk.FlatAppearance.BorderSize = 0;
            BtnOk.Click += (s, e) =>
            {
                if (Ui.Try(() => { if (!OnSave()) throw new OperationCanceledException(); }))
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
            };
            buttons.Controls.Add(BtnCancel);
            buttons.Controls.Add(BtnOk);

            _outer.Controls.Add(Fields, 0, 0);
            _outer.Controls.Add(buttons, 0, 1);
            Controls.Add(_outer);

            AcceptButton = BtnOk;
            CancelButton = BtnCancel;
        }

        /// <summary>Додає верхній заголовок/підказку над полями.</summary>
        protected Label AddHeader(string text)
        {
            var l = new Label
            {
                Text = text, AutoSize = true, MaximumSize = new Size(420, 0),
                ForeColor = Color.DimGray, Margin = new Padding(3, 0, 3, 10)
            };
            int row = Fields.RowCount;
            Fields.Controls.Add(l, 0, row);
            Fields.SetColumnSpan(l, 2);
            Fields.RowCount = row + 1;
            return l;
        }

        /// <summary>Додає рядок «підпис — елемент керування».</summary>
        protected T AddField<T>(string label, T control) where T : Control
        {
            var l = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 7, 12, 3) };
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            control.Margin = new Padding(3, 4, 3, 4);
            int row = Fields.RowCount;
            Fields.Controls.Add(l, 0, row);
            Fields.Controls.Add(control, 1, row);
            Fields.RowCount = row + 1;
            return control;
        }

        /// <summary>
        /// Перевірка та збереження даних. Повертає false, щоб залишити форму відкритою.
        /// BusinessException показується як попередження.
        /// </summary>
        protected virtual bool OnSave() => true;

        protected static string Required(TextBox box, string fieldName)
        {
            var v = box.Text.Trim();
            if (v.Length == 0)
            {
                box.Focus();
                throw new BusinessException($"Заповніть поле «{fieldName}».");
            }
            return v;
        }

        protected static string Optional(TextBox box)
        {
            var v = box.Text.Trim();
            return v.Length == 0 ? null : v;
        }

        protected static ComboBox CreateCombo(bool editable = false) => new ComboBox
        {
            DropDownStyle = editable ? ComboBoxStyle.DropDown : ComboBoxStyle.DropDownList
        };

        protected static DateTimePicker CreateDatePicker(DateTime value) => new DateTimePicker
        {
            Format = DateTimePickerFormat.Short,
            Value = value
        };
    }
}
