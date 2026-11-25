using System.Data;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Dormitory.Data
{
    /// <summary>
    /// Шар доступу до бази даних SQLite (ADO.NET).
    /// Файл бази dormitory.db створюється автоматично поруч із програмою.
    /// </summary>
    public static class Db
    {
        public const string DateFormat = "yyyy-MM-dd";

        public static string DbPath { get; } = Path.Combine(AppContext.BaseDirectory, "dormitory.db");

        private static string ConnectionString =>
            new SqliteConnectionStringBuilder { DataSource = DbPath, ForeignKeys = true }.ToString();

        public static SqliteConnection Open()
        {
            var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            return connection;
        }

        /// <summary>Створює таблиці (якщо їх немає) і заповнює демонстраційними даними.</summary>
        public static void Initialize()
        {
            using var c = Open();
            using (var cmd = Prepare(c, null, Schema.CreateSql))
                cmd.ExecuteNonQuery();

            SeedData.EnsureSeeded(c);
        }

        // ------------------------------------------------------------------
        //  Команди
        // ------------------------------------------------------------------

        public static SqliteCommand Prepare(SqliteConnection c, SqliteTransaction tx, string sql,
            params (string Name, object Value)[] parameters)
        {
            var cmd = c.CreateCommand();
            cmd.CommandText = sql;
            cmd.Transaction = tx;
            foreach (var (name, value) in parameters)
                cmd.Parameters.AddWithValue(name, ToDbValue(value));
            return cmd;
        }

        /// <summary>Перетворення значень .NET у формат зберігання SQLite.</summary>
        private static object ToDbValue(object value)
        {
            switch (value)
            {
                case null: return DBNull.Value;
                case DateTime d: return d.ToString(DateFormat, CultureInfo.InvariantCulture);
                case bool b: return b ? 1L : 0L;
                case decimal m: return (double)m;   // decimal у SQLite інакше зберігся б як TEXT
                case int i: return (long)i;
                default: return value;
            }
        }

        public static int Execute(string sql, params (string, object)[] p)
        {
            using var c = Open();
            using var cmd = Prepare(c, null, sql, p);
            return cmd.ExecuteNonQuery();
        }

        public static int Execute(SqliteConnection c, SqliteTransaction tx, string sql, params (string, object)[] p)
        {
            using var cmd = Prepare(c, tx, sql, p);
            return cmd.ExecuteNonQuery();
        }

        /// <summary>Виконує INSERT і повертає Id нового запису.</summary>
        public static long Insert(string sql, params (string, object)[] p)
        {
            using var c = Open();
            return Insert(c, null, sql, p);
        }

        public static long Insert(SqliteConnection c, SqliteTransaction tx, string sql, params (string, object)[] p)
        {
            using (var cmd = Prepare(c, tx, sql, p))
                cmd.ExecuteNonQuery();
            using var idCmd = Prepare(c, tx, "SELECT last_insert_rowid();");
            return (long)idCmd.ExecuteScalar();
        }

        public static object Scalar(string sql, params (string, object)[] p)
        {
            using var c = Open();
            return Scalar(c, null, sql, p);
        }

        public static object Scalar(SqliteConnection c, SqliteTransaction tx, string sql, params (string, object)[] p)
        {
            using var cmd = Prepare(c, tx, sql, p);
            var result = cmd.ExecuteScalar();
            return result == DBNull.Value ? null : result;
        }

        public static long ScalarLong(string sql, params (string, object)[] p) =>
            Convert.ToInt64(Scalar(sql, p) ?? 0L);

        public static long ScalarLong(SqliteConnection c, SqliteTransaction tx, string sql, params (string, object)[] p) =>
            Convert.ToInt64(Scalar(c, tx, sql, p) ?? 0L);

        /// <summary>Виконує дії в одній транзакції (з автоматичним відкатом при помилці).</summary>
        public static void InTransaction(Action<SqliteConnection, SqliteTransaction> action)
        {
            using var c = Open();
            using var tx = c.BeginTransaction();
            action(c, tx);
            tx.Commit();
        }

        public static List<T> QueryList<T>(string sql, Func<SqliteDataReader, T> map, params (string, object)[] p)
        {
            using var c = Open();
            return QueryList(c, null, sql, map, p);
        }

        public static List<T> QueryList<T>(SqliteConnection c, SqliteTransaction tx, string sql,
            Func<SqliteDataReader, T> map, params (string, object)[] p)
        {
            var list = new List<T>();
            using var cmd = Prepare(c, tx, sql, p);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(map(r));
            return list;
        }

        // ------------------------------------------------------------------
        //  Завантаження у DataTable (для DataGridView)
        // ------------------------------------------------------------------

        /// <summary>
        /// Виконує SELECT і повертає DataTable. Типи стовпців визначаються за даними:
        /// цілі → long, дробові → double, рядки формату yyyy-MM-dd → DateTime, інше → string.
        /// Це дає коректне сортування та форматування в таблицях.
        /// </summary>
        public static DataTable Query(string sql, params (string, object)[] p)
        {
            using var c = Open();
            using var cmd = Prepare(c, null, sql, p);
            using var r = cmd.ExecuteReader();

            var names = new List<string>();
            for (int i = 0; i < r.FieldCount; i++)
                names.Add(r.GetName(i));

            var rows = new List<object[]>();
            while (r.Read())
            {
                var values = new object[r.FieldCount];
                r.GetValues(values);
                rows.Add(values);
            }

            return BuildTable(names, rows);
        }

        private static DataTable BuildTable(List<string> names, List<object[]> rows)
        {
            var table = new DataTable();
            for (int i = 0; i < names.Count; i++)
                table.Columns.Add(names[i], InferType(rows, i));

            table.BeginLoadData();
            foreach (var row in rows)
            {
                var values = new object[names.Count];
                for (int i = 0; i < names.Count; i++)
                    values[i] = ConvertValue(row[i], table.Columns[i].DataType);
                table.Rows.Add(values);
            }
            table.EndLoadData();
            table.AcceptChanges();
            return table;
        }

        private static Type InferType(List<object[]> rows, int col)
        {
            bool hasString = false, allDates = true, hasLong = false, hasDouble = false;
            foreach (var row in rows)
            {
                var v = row[col];
                if (v == null || v == DBNull.Value) continue;
                switch (v)
                {
                    case string s:
                        hasString = true;
                        if (!IsIsoDate(s)) allDates = false;
                        break;
                    case long _: hasLong = true; break;
                    case double _: hasDouble = true; break;
                    default: hasString = true; allDates = false; break;
                }
            }

            if (hasString)
                return allDates && !hasLong && !hasDouble ? typeof(DateTime) : typeof(string);
            if (hasDouble) return typeof(double);
            if (hasLong) return typeof(long);
            return typeof(string);
        }

        private static object ConvertValue(object v, Type type)
        {
            if (v == null || v == DBNull.Value) return DBNull.Value;
            if (type == typeof(DateTime)) return ParseDate((string)v);
            if (type == typeof(double)) return Convert.ToDouble(v, CultureInfo.InvariantCulture);
            if (type == typeof(long)) return Convert.ToInt64(v, CultureInfo.InvariantCulture);
            return Convert.ToString(v, CultureInfo.CurrentCulture);
        }

        private static bool IsIsoDate(string s) =>
            s.Length == 10 && s[4] == '-' && s[7] == '-' &&
            DateTime.TryParseExact(s, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

        public static DateTime ParseDate(string s) =>
            DateTime.ParseExact(s, DateFormat, CultureInfo.InvariantCulture);

        // ------------------------------------------------------------------
        //  Розширення для SqliteDataReader
        // ------------------------------------------------------------------

        public static string Str(this SqliteDataReader r, string col)
        {
            int i = r.GetOrdinal(col);
            return r.IsDBNull(i) ? null : r.GetString(i);
        }

        public static long Long(this SqliteDataReader r, string col)
        {
            int i = r.GetOrdinal(col);
            return r.IsDBNull(i) ? 0 : r.GetInt64(i);
        }

        public static long? LongN(this SqliteDataReader r, string col)
        {
            int i = r.GetOrdinal(col);
            return r.IsDBNull(i) ? (long?)null : r.GetInt64(i);
        }

        public static int Int(this SqliteDataReader r, string col) => (int)r.Long(col);

        public static double Dbl(this SqliteDataReader r, string col)
        {
            int i = r.GetOrdinal(col);
            return r.IsDBNull(i) ? 0 : r.GetDouble(i);
        }

        public static DateTime? DateN(this SqliteDataReader r, string col)
        {
            var s = r.Str(col);
            return string.IsNullOrEmpty(s) ? (DateTime?)null : ParseDate(s);
        }

        public static DateTime Date(this SqliteDataReader r, string col) => r.DateN(col) ?? DateTime.MinValue;

        /// <summary>Чи є помилка порушенням зовнішнього ключа / унікальності (код SQLITE_CONSTRAINT = 19).</summary>
        public static bool IsConstraintError(Exception ex) =>
            ex is SqliteException se && se.SqliteErrorCode == 19;
    }
}
