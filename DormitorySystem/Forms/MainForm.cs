using System.ComponentModel;
using Dormitory.Data;
using Dormitory.Models;
using Dormitory.Pages;
using Dormitory.Services;
using Dormitory.UI;

namespace Dormitory.Forms
{
    /// <summary>Головне вікно програми: меню, вкладки модулів, рядок стану.</summary>
    [DesignerCategory("Code")]
    public class MainForm : Form
    {
        private readonly TabControl _tabs;
        private readonly ToolStripStatusLabel _statusUser, _statusStats, _statusTime;
        private readonly List<PageBase> _pages = new List<PageBase>();

        /// <summary>true — користувач вийшов з облікового запису (показати вікно входу знову).</summary>
        public bool LogoutRequested { get; private set; }

        public MainForm()
        {
            var user = Session.CurrentUser;
            Text = "Автоматизована система студентського гуртожитку";
            Font = Ui.BaseFont;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1000, 600);
            Size = new Size(1280, 760);
            WindowState = FormWindowState.Maximized;

            // ---------- Меню
            var menu = new MenuStrip { Font = Ui.BaseFont };
            var file = new ToolStripMenuItem("&Файл");
            file.DropDownItems.Add(new ToolStripMenuItem("Оновити дані", null, (s, e) => RefreshCurrent(), Keys.F5));
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add("Змінити пароль…", null, (s, e) => ChangePassword());
            file.DropDownItems.Add("Вийти з облікового запису", null, (s, e) => Logout());
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add(new ToolStripMenuItem("Вихід", null, (s, e) => Close(), Keys.Alt | Keys.F4));

            var help = new ToolStripMenuItem("&Довідка");
            help.DropDownItems.Add("Про програму", null, (s, e) => About());
            menu.Items.Add(file);
            menu.Items.Add(help);
            MainMenuStrip = menu;

            // ---------- Заголовок
            var header = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Ui.Accent };
            header.Controls.Add(new Label
            {
                Text = "Студентський гуртожиток",
                Font = new Font("Segoe UI Semibold", 15F),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(14, 12)
            });
            var who = new Label
            {
                Text = $"{user.FullName}  •  {Roles.Display(user.Role)}",
                ForeColor = Color.FromArgb(220, 230, 245),
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            header.Controls.Add(who);
            header.Resize += (s, e) => who.Location = new Point(header.Width - who.Width - 16, 18);

            // ---------- Вкладки
            _tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(14, 5) };
            AddPage(new StudentsPage());
            AddPage(new RoomsPage());
            AddPage(new AccommodationsPage());
            AddPage(new PaymentsPage());
            AddPage(new ReportsPage());
            if (Session.IsAdmin)
                AddPage(new UsersPage());
            _tabs.SelectedIndexChanged += (s, e) => RefreshCurrent();

            // ---------- Рядок стану
            var status = new StatusStrip();
            _statusUser = new ToolStripStatusLabel($"Користувач: {user.Login} ({Roles.Display(user.Role)})");
            _statusStats = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            _statusTime = new ToolStripStatusLabel(DateTime.Now.ToString("dd.MM.yyyy"));
            status.Items.AddRange(new ToolStripItem[]
            {
                _statusUser, new ToolStripStatusLabel("|"), _statusStats,
                new ToolStripStatusLabel("БД: " + Path.GetFileName(Db.DbPath)) { ToolTipText = Db.DbPath },
                _statusTime
            });

            Controls.Add(_tabs);
            Controls.Add(header);
            Controls.Add(menu);
            Controls.Add(status);

            Shown += (s, e) => RefreshCurrent();
        }

        private void AddPage(PageBase page)
        {
            var tab = new TabPage(page.Title) { BackColor = SystemColors.Window };
            tab.Controls.Add(page);
            _tabs.TabPages.Add(tab);
            _pages.Add(page);
        }

        private void RefreshCurrent()
        {
            if (_tabs.SelectedIndex >= 0 && _tabs.SelectedIndex < _pages.Count)
                _pages[_tabs.SelectedIndex].RefreshData();
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            Ui.Try(() =>
            {
                var (total, occupied) = RoomRepository.GetCapacityStats();
                long students = Db.ScalarLong("SELECT COUNT(*) FROM Students;");
                _statusStats.Text = $"Студентів у базі: {students}   Мешканців: {occupied}   Вільних місць: {total - occupied} з {total}";
            });
        }

        private void ChangePassword()
        {
            using var f = new ChangePasswordForm();
            f.ShowDialog(this);
        }

        private void Logout()
        {
            if (!Ui.Confirm("Вийти з облікового запису?")) return;
            LogoutRequested = true;
            Close();
        }

        private void About()
        {
            MessageBox.Show(
                "Автоматизована система студентського гуртожитку\nВерсія 1.0\n\n" +
                "Курсовий проєкт.\nC#, .NET 8, Windows Forms, SQLite.\n\n" +
                "Модулі: облік студентів і кімнат, поселення / виселення / переселення,\n" +
                "оплата проживання та заборгованість, звіти, керування користувачами.",
                "Про програму", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
