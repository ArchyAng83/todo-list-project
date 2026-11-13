using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TodoList.Infrastructure.Persistence;

namespace TodoList.Infrastructure.Reminders;

/// <summary>
/// Serializes reminder dispatch across replicas with a PostgreSQL session-level
/// advisory lock. The lock belongs to the open connection, so it is released
/// automatically when the connection closes or the process crashes; no schema
/// change or extra table is required.
/// </summary>
public sealed class ReminderDispatchLock(
    AppDbContext db,
    ILogger<ReminderDispatchLock> logger) : IAsyncDisposable
{
    /// <summary>
    /// Stable application-wide key: the ASCII bytes of <c>TodoList</c> read as
    /// a signed 64-bit integer.
    /// </summary>
    private const long LockKey = 0x546F646F4C697374;

    private bool _locked;

    /// <summary>
    /// Tries to take the dispatch lock without waiting. A second replica gets
    /// <see langword="false"/> and skips this poll instead of sending duplicates.
    /// </summary>
    public async Task<bool> TryAcquireAsync(CancellationToken cancellationToken = default)
    {
        if (_locked)
        {
            return true;
        }

        var connection = db.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
        {
            await db.Database.OpenConnectionAsync(cancellationToken);
        }

        await using var command = CreateCommand(connection, "SELECT pg_try_advisory_lock(@key)");

        _locked = (bool)(await command.ExecuteScalarAsync(cancellationToken))!;

        if (!_locked)
        {
            // Do not keep an idle connection when another replica owns the lock.
            await db.Database.CloseConnectionAsync();
        }

        return _locked;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_locked)
        {
            return;
        }

        _locked = false;

        try
        {
            var connection = db.Database.GetDbConnection();

            if (connection.State == ConnectionState.Open)
            {
                await using var command = CreateCommand(connection, "SELECT pg_advisory_unlock(@key)");
                await command.ExecuteScalarAsync();
            }
        }
        catch (Exception exception)
        {
            // Closing the connection releases the lock anyway, so a failed
            // unlock is noisy but not fatal.
            logger.LogWarning(exception, "Could not release the reminder dispatch advisory lock.");
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static DbCommand CreateCommand(DbConnection connection, string sql)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;

        var parameter = command.CreateParameter();
        parameter.ParameterName = "key";
        parameter.Value = LockKey;
        command.Parameters.Add(parameter);

        return command;
    }
}
