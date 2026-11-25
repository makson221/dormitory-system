using System.ComponentModel;
using System.Text.RegularExpressions;
using Dormitory.Data;
using Dormitory.Models;
using Dormitory.UI;

namespace Dormitory.Forms
{
    /// <summary>Додавання / редагування студента.</summary>
    [DesignerCategory("Code")]
    public class StudentEditForm : EditFormBase
    {
        private readonly Student _student;
        private readonly TextBox _lastName, _firstName, _middleName, _group, _ticket, _phone, _email;
        private readonly DateTimePicker _birth;
        private readonly ComboBox _gender, _faculty;
        private readonly NumericUpDown _course, _benefit;

        public StudentEditForm(Student student = null)
            : base(student == null ? "Новий студент" : "Редагування студента")
        {
            _student = student ?? new Student();

            AddHeader("Поля, позначені *, є обов'язковими.");
            _lastName = AddField("Прізвище *", new TextBox { MaxLength = 50 });
            _firstName = AddField("Ім'я *", new TextBox { MaxLength = 50 });
            _middleName = AddField("По батькові", new TextBox { MaxLength = 50 });
            _birth = AddField("Дата народження", new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                ShowCheckBox = true,
                MinDate = new DateTime(1950, 1, 1)
            });
            _gender = AddField("Стать *", CreateCombo());
            _gender.Items.AddRange(new object[] { "Чоловіча", "Жіноча" });

            _faculty = AddField("Факультет *", CreateCombo(editable: true));
            foreach (var f in StudentRepository.GetFaculties()) _faculty.Items.Add(f);

            _course = AddField("Курс *", new NumericUpDown { Minimum = 1, Maximum = 6, Width = 80 });
            _group = AddField("Група", new TextBox { MaxLength = 20 });
            _ticket = AddField("№ студ. квитка *", new TextBox { MaxLength = 20 });
            _phone = AddField("Телефон", new TextBox { MaxLength = 20 });
            _email = AddField("E-mail", new TextBox { MaxLength = 80 });
            _benefit = AddField("Пільга на оплату, %", new NumericUpDown { Minimum = 0, Maximum = 100, Increment = 5, Width = 80 });

            LoadData();
        }

        private void LoadData()
        {
            var s = _student;
            _lastName.Text = s.LastName;
            _firstName.Text = s.FirstName;
            _middleName.Text = s.MiddleName;
            if (s.BirthDate.HasValue)
            {
                _birth.Value = s.BirthDate.Value;
                _birth.Checked = true;
            }
            else
            {
                _birth.Value = DateTime.Today.AddYears(-18);
                _birth.Checked = false;
            }
            _gender.SelectedIndex = s.Gender == "Ж" ? 1 : 0;
            _faculty.Text = s.Faculty;
            _course.Value = Math.Max(1, Math.Min(6, s.Course));
            _group.Text = s.GroupName;
            _ticket.Text = s.StudentTicket;
            _phone.Text = s.Phone;
            _email.Text = s.Email;
            _benefit.Value = s.BenefitPercent;
        }

        protected override bool OnSave()
        {
            var s = _student;
            s.LastName = Required(_lastName, "Прізвище");
            s.FirstName = Required(_firstName, "Ім'я");
            s.MiddleName = Optional(_middleName);
            s.BirthDate = _birth.Checked ? _birth.Value.Date : (DateTime?)null;
            s.Gender = _gender.SelectedIndex == 1 ? "Ж" : "Ч";

            s.Faculty = _faculty.Text.Trim();
            if (s.Faculty.Length == 0)
            {
                _faculty.Focus();
                throw new BusinessException("Заповніть поле «Факультет».");
            }

            s.Course = (int)_course.Value;
            s.GroupName = Optional(_group);
            s.StudentTicket = Required(_ticket, "№ студентського квитка");
            s.Phone = Optional(_phone);
            s.Email = Optional(_email);
            s.BenefitPercent = (int)_benefit.Value;

            if (s.BirthDate.HasValue && s.BirthDate.Value > DateTime.Today.AddYears(-14))
                throw new BusinessException("Перевірте дату народження: вік студента має бути не менше 14 років.");

            if (s.Phone != null && !Regex.IsMatch(s.Phone, @"^\+?[\d\s\-()]{7,20}$"))
            {
                _phone.Focus();
                throw new BusinessException("Невірний формат телефону. Приклад: +380671234567");
            }

            if (s.Email != null && !Regex.IsMatch(s.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                _email.Focus();
                throw new BusinessException("Невірний формат e-mail.");
            }

            if (StudentRepository.TicketExists(s.StudentTicket, s.Id))
            {
                _ticket.Focus();
                throw new BusinessException("Студент з таким номером студентського квитка вже є в базі.");
            }

            if (s.Id == 0)
                s.Id = StudentRepository.Insert(s);
            else
                StudentRepository.Update(s);
            return true;
        }
    }
}
