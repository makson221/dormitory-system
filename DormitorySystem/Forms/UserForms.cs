using System.ComponentModel;
using Dormitory.Data;
using Dormitory.Models;
using Dormitory.Services;
using Dormitory.UI;

namespace Dormitory.Forms
{
    /// <summary>Додавання / редагування облікового запису (лише для адміністратора).</summary>
    [DesignerCategory("Code")]
    public class UserEditForm : EditFormBase
    {
        private readonly User _user;
        private readonly TextBox _login, _fullName, _password, _password2;
        private readonly ComboBox _role;
        private readonly CheckBox _active;

        public UserEditForm(User user = null) : base(user == null ? "Новий користувач" : "Редагування користувача")
        {
            _user = user ?? new User { Role = Roles.Commandant, IsActive = true };
            bool isNew = _user.Id == 0;

            _login = AddField("Логін *", new TextBox { MaxLength = 30, Text = _user.Login });
            _fullName = AddField("ПІБ *", new TextBox { MaxLength = 100, Text = _user.FullName });
            _role = AddField("Роль *", CreateCombo());
            _role.Items.AddRange(new object[] { Roles.Display(Roles.Admin), Roles.Display(Roles.Commandant) });
            _role.SelectedIndex = _user.Role == Roles.Admin ? 0 : 1;
            _active = AddField("", new CheckBox { Text = "Обліковий запис активний", Checked = _user.IsActive, AutoSize = true });

            _password = AddField(isNew ? "Пароль *" : "Новий пароль", new TextBox { UseSystemPasswordChar = true });
            _password2 = AddField("Повторіть пароль", new TextBox { UseSystemPasswordChar = true });
            if (!isNew)
                AddHeader("Залиште поля пароля порожніми, щоб не змінювати пароль.");

            if (_user.Id == Session.CurrentUser?.Id)
            {
                _role.Enabled = false;      // не можна понизити або заблокувати самого себе
                _active.Enabled = false;
            }
        }

        protected override bool OnSave()
        {
            var u = _user;
            string login = Required(_login, "Логін");
            if (login.Contains(' '))
                throw new BusinessException("Логін не повинен містити пробілів.");
            if (UserRepository.LoginExists(login, u.Id))
                throw new BusinessException("Користувач з таким логіном уже існує.");

            string role = _role.SelectedIndex == 0 ? Roles.Admin : Roles.Commandant;
            bool active = _active.Checked;

            if (u.Id != 0 && u.Role == Roles.Admin && (role != Roles.Admin || !active) &&
                UserRepository.ActiveAdminCount(u.Id) == 0)
                throw new BusinessException("У системі має залишитися хоча б один активний адміністратор.");

            bool changePassword = u.Id == 0 || _password.Text.Length > 0 || _password2.Text.Length > 0;
            if (changePassword)
            {
                AuthService.ValidatePassword(_password.Text);
                if (_password.Text != _password2.Text)
                    throw new BusinessException("Паролі не збігаються.");
            }

            u.Login = login;
            u.FullName = Required(_fullName, "ПІБ");
            u.Role = role;
            u.IsActive = active;

            if (u.Id == 0)
            {
                u.Id = UserRepository.Insert(u, _password.Text);
            }
            else
            {
                UserRepository.Update(u);
                if (changePassword) UserRepository.SetPassword(u.Id, _password.Text);
            }
            return true;
        }
    }

    /// <summary>Зміна власного пароля.</summary>
    [DesignerCategory("Code")]
    public class ChangePasswordForm : EditFormBase
    {
        private readonly TextBox _old, _new, _new2;

        public ChangePasswordForm() : base("Зміна пароля")
        {
            AddHeader($"Користувач: {Session.CurrentUser.FullName} ({Session.CurrentUser.Login})");
            _old = AddField("Поточний пароль", new TextBox { UseSystemPasswordChar = true });
            _new = AddField("Новий пароль", new TextBox { UseSystemPasswordChar = true });
            _new2 = AddField("Повторіть новий", new TextBox { UseSystemPasswordChar = true });
        }

        protected override bool OnSave()
        {
            if (_new.Text != _new2.Text)
                throw new BusinessException("Нові паролі не збігаються.");
            AuthService.ChangePassword(Session.CurrentUser, _old.Text, _new.Text);
            Ui.Info("Пароль успішно змінено.");
            return true;
        }
    }
}
