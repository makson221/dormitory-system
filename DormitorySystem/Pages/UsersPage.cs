using System.ComponentModel;
using System.Data;
using Dormitory.Data;
using Dormitory.Forms;
using Dormitory.Models;
using Dormitory.Services;
using Dormitory.UI;

namespace Dormitory.Pages
{
    /// <summary>Вкладка «Користувачі» (доступна лише адміністратору).</summary>
    [DesignerCategory("Code")]
    public class UsersPage : PageBase
    {
        public override string Title => "Користувачі";

        public UsersPage()
        {
            Tools.Items.Add(Ui.Button("Додати", (s, e) => Add()));
            Tools.Items.Add(Ui.Button("Редагувати / змінити пароль", (s, e) => Edit()));
            Tools.Items.Add(Ui.Button("Заблокувати / розблокувати", (s, e) => ToggleActive()));
            AddSearch();

            Grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) Edit(); };
            Grid.CellFormatting += (s, e) =>
            {
                if (e.RowIndex >= 0 && Grid.Rows[e.RowIndex].DataBoundItem is DataRowView v &&
                    (string)v["Статус"] != "Активний")
                    e.CellStyle.ForeColor = Color.Gray;
            };
            BuildLayout();
        }

        protected override DataTable LoadTable() => UserRepository.GetTable();

        private void Add()
        {
            using var f = new UserEditForm();
            if (f.ShowDialog(this) == DialogResult.OK) RefreshData();
        }

        private void Edit()
        {
            var id = RequireSelectedId();
            if (id == null) return;
            var user = UserRepository.Get(id.Value);
            if (user == null) return;
            using var f = new UserEditForm(user);
            if (f.ShowDialog(this) == DialogResult.OK) RefreshData();
        }

        private void ToggleActive()
        {
            var id = RequireSelectedId();
            if (id == null) return;
            var user = UserRepository.Get(id.Value);
            if (user == null) return;

            Ui.Try(() =>
            {
                if (user.Id == Session.CurrentUser.Id)
                    throw new BusinessException("Неможливо заблокувати власний обліковий запис.");
                if (user.IsActive && user.Role == Roles.Admin && UserRepository.ActiveAdminCount(user.Id) == 0)
                    throw new BusinessException("У системі має залишитися хоча б один активний адміністратор.");
                if (!Ui.Confirm($"{(user.IsActive ? "Заблокувати" : "Розблокувати")} користувача «{user.Login}»?"))
                    return;
                UserRepository.SetActive(user.Id, !user.IsActive);
                RefreshData();
            });
        }
    }
}
