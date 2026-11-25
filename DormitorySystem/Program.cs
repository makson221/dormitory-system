using Dormitory.Data;
using Dormitory.Forms;
using Dormitory.Services;

namespace Dormitory
{
    internal static class Program
    {
        /// <summary>
        /// Точка входу в програму: ініціалізація БД → вхід користувача → головне вікно.
        /// Після "Вийти з облікового запису" знову показується вікно входу.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            ApplicationConfiguration.Initialize();

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) =>
                MessageBox.Show("Сталася непередбачена помилка:\n" + e.Exception.Message,
                    "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);

            try
            {
                Db.Initialize();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не вдалося відкрити базу даних:\n" + ex.Message,
                    "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            while (true)
            {
                using (var login = new LoginForm())
                {
                    if (login.ShowDialog() != DialogResult.OK)
                        return;
                }

                bool logout;
                using (var main = new MainForm())
                {
                    Application.Run(main);
                    logout = main.LogoutRequested;
                }

                Session.Clear();
                if (!logout)
                    return;
            }
        }
    }
}
