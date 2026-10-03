using Npgsql;
using System;
using System.Diagnostics;
using System.Text;
using System.Text.Unicode;
using System.Threading;
using System.Threading.Tasks;

namespace ExifApp.Services;

public class DatabaseService
{
    /// <summary>
    /// Результат проверки подключения.
    /// </summary>
    public record TestResult(bool Success, string Message, TimeSpan Elapsed);

    /// <summary>
    /// Проверяет подключение к Postgres. Соединение открывается на время теста
    /// и сразу закрывается — как и при будущих запросах к процедурам БД.
    /// </summary>
    public async Task<TestResult> TestConnectionAsync(
        string connectionString,
        int timeoutSeconds,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new TestResult(false, "Строка подключения не задана.", TimeSpan.Zero);
        }

        // Разбираем строку, чтобы применить таймаут и получить корректный ConnectionString
        NpgsqlConnectionStringBuilder parsed;
        try
        {
            parsed = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (Exception ex)
        {
            return new TestResult(false,
                $"Неверный формат строки подключения:\n{ex.Message}",
                TimeSpan.Zero);
        }

        // Применяем таймаут из настроек, если он не задан в самой строке
        var effectiveTimeout = Math.Max(1, timeoutSeconds);
        if (parsed.Timeout == 0)
            parsed.Timeout = effectiveTimeout;
        // Принудительно переводим сообщения сервера на английский —
        // защита от кракозябр при русской локали PostgreSQL на Windows.
        // Если пользователь уже указал свои Options, не перезаписываем.
        /*if (string.IsNullOrWhiteSpace(parsed.Options) ||
            !parsed.Options.Contains("lc_messages", StringComparison.OrdinalIgnoreCase))
        {
            parsed.Options = string.IsNullOrWhiteSpace(parsed.Options)
                ? "-c lc_messages=C"
                : $"{parsed.Options} -c lc_messages=C";
        }*/

        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = new NpgsqlConnection(parsed.ConnectionString);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(effectiveTimeout));

            await conn.OpenAsync(timeoutCts.Token);

            // Простейшая проверка «живости» — читаем свойства открытого соединения
            var host = conn.Host;
            var port = conn.Port;
            var database = conn.Database;
            var version = conn.PostgreSqlVersion;

            await conn.CloseAsync();
            sw.Stop();

            return new TestResult(
                true,
                $"Подключение установлено и закрыто.\n\n" +
                $"Сервер:  {host}:{port}\n" +
                $"База:    {database}\n" +
                $"Версия:  {version}\n" +
                $"Время:   {sw.ElapsedMilliseconds} мс",
                sw.Elapsed);
        }
        catch (NpgsqlException ex)
        {
            sw.Stop();
            var display = MaskPassword(parsed.ConnectionString);
            return new TestResult(false,
                $"База данных недоступна.\n\n" +
                $"Причина: {ex.Message}\n\n" +
                $"Параметры подключения:\n{display}",
                sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            var display = MaskPassword(parsed.ConnectionString);
            return new TestResult(false,
                $"Таймаут подключения ({effectiveTimeout} c).\n\n" +
                $"Параметры подключения:\n{display}",
                sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            var display = MaskPassword(parsed.ConnectionString);
            return new TestResult(false,
                $"Ошибка подключения: {ex.Message}\n\n" +
                $"Параметры подключения:\n{display}",
                sw.Elapsed);
        }
    }

    /// <summary>
    /// Заменяет пароль в строке подключения на *** для отображения в UI.
    /// </summary>
    public static string MaskPassword(string connectionString)
    {
        try
        {
            var b = new NpgsqlConnectionStringBuilder(connectionString);
            if (!string.IsNullOrEmpty(b.Password))
                b.Password = "***";
            return b.ConnectionString;
        }
        catch
        {
            return connectionString;
        }
    }
}