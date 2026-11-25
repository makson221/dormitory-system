using System.Security.Cryptography;
using Dormitory.Data;
using Dormitory.Models;

namespace Dormitory.Services
{
    /// <summary>Хешування паролів за алгоритмом PBKDF2 (SHA-256, 100 000 ітерацій, сіль 16 байт).</summary>
    public static class PasswordHasher
    {
        private const int Iterations = 100_000;
        private const int SaltSize = 16;
        private const int HashSize = 32;

        public static (string Hash, string Salt) Hash(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
            return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
        }

        public static bool Verify(string password, string storedHash, string storedSalt)
        {
            try
            {
                byte[] salt = Convert.FromBase64String(storedSalt);
                byte[] expected = Convert.FromBase64String(storedHash);
                byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, expected.Length);
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }

    /// <summary>Поточний сеанс роботи (користувач, що увійшов у систему).</summary>
    public static class Session
    {
        public static User CurrentUser { get; private set; }

        public static bool IsAdmin => CurrentUser?.IsAdmin == true;

        public static void Start(User user) => CurrentUser = user;

        public static void Clear() => CurrentUser = null;
    }

    /// <summary>Автентифікація користувачів.</summary>
    public static class AuthService
    {
        /// <summary>Перевіряє логін і пароль. Повертає користувача або кидає BusinessException.</summary>
        public static User Login(string login, string password)
        {
            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrEmpty(password))
                throw new BusinessException("Введіть логін і пароль.");

            var user = UserRepository.GetByLogin(login.Trim());
            if (user == null || !PasswordHasher.Verify(password, user.PasswordHash, user.Salt))
                throw new BusinessException("Невірний логін або пароль.");

            if (!user.IsActive)
                throw new BusinessException("Обліковий запис заблоковано. Зверніться до адміністратора.");

            Session.Start(user);
            return user;
        }

        public static void ChangePassword(User user, string oldPassword, string newPassword)
        {
            var fresh = UserRepository.Get(user.Id);
            if (fresh == null || !PasswordHasher.Verify(oldPassword, fresh.PasswordHash, fresh.Salt))
                throw new BusinessException("Поточний пароль введено невірно.");
            ValidatePassword(newPassword);
            UserRepository.SetPassword(user.Id, newPassword);
        }

        public static void ValidatePassword(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 6)
                throw new BusinessException("Пароль повинен містити щонайменше 6 символів.");
        }
    }
}
