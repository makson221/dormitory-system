using System.ComponentModel;
using Dormitory.Models;
using Dormitory.Services;
using Dormitory.UI;

namespace Dormitory.Forms
{
    /// <summary>Вікно входу в систему.</summary>
    [DesignerCategory("Code")]
    public class LoginForm : EditFormBase
    {
        private const int MaxAttempts = 5;
        private readonly TextBox _login;
        private readonly TextBox _password;
        private int _attempts;

        public LoginForm() : base("Вхід до системи", "Увійти")
        {
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = true;

            var title = new Label
            {
                Text = "Студентський гуртожиток",
                Font = Ui.TitleFont,
                ForeColor = Ui.Accent,
                AutoSize = true,
                Margin = new Padding(3, 0, 3, 2)
            };
            Fields.Controls.Add(title, 0, 0);
            Fields.SetColumnSpan(title, 2);
            Fields.RowCount = 1;
            AddHeader("Автоматизована система обліку мешканців.\nВведіть свої облікові дані.");

            _login = AddField("Логін:", new TextBox());
            _password = AddField("Пароль:", new TextBox { UseSystemPasswordChar = true });

            var hint = AddHeader("Демо-доступ: admin / admin123 (адміністратор), komendant / komendant1 (комендант)");
            hint.Margin = new Padding(3, 10, 3, 0);
            hint.Font = new Font(Ui.BaseFont.FontFamily, 8.25F);

            Shown += (s, e) => _login.Focus();
        }

        protected override bool OnSave()
        {
            try
            {
                AuthService.Login(_login.Text, _password.Text);
                return true;
            }
            catch (BusinessException)
            {
                _attempts++;
                _password.Clear();
                _password.Focus();
                if (_attempts >= MaxAttempts)
                {
                    Ui.Error("Перевищено кількість спроб входу. Програму буде закрито.");
                    DialogResult = DialogResult.Cancel;
                    Close();
                    throw new OperationCanceledException();
                }
                throw;
            }
        }
    }
}
