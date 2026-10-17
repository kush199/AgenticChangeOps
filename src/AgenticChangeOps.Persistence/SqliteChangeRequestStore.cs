using System.Text.Json;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Stores;
using Microsoft.Data.Sqlite;

namespace AgenticChangeOps.Persistence;

/// <summary>
/// Durable <see cref="IChangeRequestStore"/> over one local SQLite file. Records are stored whole
/// as JSON beside the few columns needed for keys and ordering, so the store never looks at what
/// kind of record an agent produced. Every access takes <see cref="SyncRoot"/>; the monitor is
/// re-entrant, so a service can hold it across a check-then-write sequence.
///
/// The file is opened lazily. If it cannot be opened, or holds a layout this version does not
/// understand, the operation fails with <see cref="StorageUnavailableException"/> and the next
/// operation tries again. Failures never carry a path, driver message or inner exception.
/// </summary>
public sealed class SqliteChangeRequestStore : IChangeRequestStore, IDisposable
{
    private const int NotADatabase = 26;
    private const int PrimaryKeyViolation = 1555;

    private readonly object _syncRoot = new();
    private readonly SqliteStorageOptions _options;
    private SqliteConnection? _connection;

    public SqliteChangeRequestStore(SqliteStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public object SyncRoot => _syncRoot;

    public void Save(ChangeRequest changeRequest)
    {
        ArgumentNullException.ThrowIfNull(changeRequest);
        Run(connection =>
        {
            using var transaction = connection.BeginTransaction();
            RequireNew(connection, transaction, changeRequest.ChangeRequestId);
            InsertChangeRequest(connection, transaction, changeRequest);
            transaction.Commit();
        });
    }

    public void Update(ChangeRequest changeRequest)
    {
        ArgumentNullException.ThrowIfNull(changeRequest);
        Run(connection =>
        {
            using var transaction = connection.BeginTransaction();
            ReplaceChangeRequest(connection, transaction, changeRequest);
            transaction.Commit();
        });
    }

    public ChangeRequest? FindById(Guid id) =>
        Run(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT payload FROM {StorageSchema.ChangeRequests} WHERE id = $id";
            command.Parameters.AddWithValue("$id", Key(id));
            return command.ExecuteScalar() is string payload
                ? JsonSerializer.Deserialize<ChangeRequest>(payload, RecordJson.Options)
                : null;
        });

    public void SaveAuditEntry(ChangeRequestAuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Run(connection =>
        {
            using var transaction = connection.BeginTransaction();
            InsertAuditEntry(connection, transaction, entry);
            transaction.Commit();
        });
    }

    public IReadOnlyList<ChangeRequestAuditEntry> GetAuditEntries(Guid changeRequestId) =>
        Run(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                $"SELECT payload FROM {StorageSchema.AuditEntries} " +
                "WHERE change_request_id = $id ORDER BY occurred_at_ticks, seq";
            command.Parameters.AddWithValue("$id", Key(changeRequestId));

            var entries = new List<ChangeRequestAuditEntry>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
                entries.Add(JsonSerializer.Deserialize<ChangeRequestAuditEntry>(reader.GetString(0), RecordJson.Options)!);
            return (IReadOnlyList<ChangeRequestAuditEntry>)entries.AsReadOnly();
        });

    public void SaveWithAudit(ChangeRequest changeRequest, ChangeRequestAuditEntry auditEntry)
    {
        ArgumentNullException.ThrowIfNull(changeRequest);
        ArgumentNullException.ThrowIfNull(auditEntry);
        RequireSameChangeRequest(changeRequest.ChangeRequestId, auditEntry);

        Run(connection =>
        {
            using var transaction = connection.BeginTransaction();
            RequireNew(connection, transaction, changeRequest.ChangeRequestId);
            InsertChangeRequest(connection, transaction, changeRequest);
            InsertAuditEntry(connection, transaction, auditEntry);
            transaction.Commit();
        });
    }

    public void UpdateWithAudit(ChangeRequest changeRequest, ChangeRequestAuditEntry auditEntry)
    {
        ArgumentNullException.ThrowIfNull(changeRequest);
        ArgumentNullException.ThrowIfNull(auditEntry);
        RequireSameChangeRequest(changeRequest.ChangeRequestId, auditEntry);

        Run(connection =>
        {
            using var transaction = connection.BeginTransaction();
            ReplaceChangeRequest(connection, transaction, changeRequest);
            InsertAuditEntry(connection, transaction, auditEntry);
            transaction.Commit();
        });
    }

    public void SaveAgentOutput<T>(T output, ChangeRequestAuditEntry auditEntry) where T : IAgentOutput
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(auditEntry);

        // All checks happen before any write so the call stores both items or neither.
        RequireSameChangeRequest(output.ChangeRequestId, auditEntry);

        var outputType = OutputType<T>();
        Run(connection =>
        {
            using var transaction = connection.BeginTransaction();

            using (var exists = connection.CreateCommand())
            {
                exists.Transaction = transaction;
                exists.CommandText =
                    $"SELECT 1 FROM {StorageSchema.AgentOutputs} " +
                    "WHERE change_request_id = $id AND output_type = $type AND version = $version";
                exists.Parameters.AddWithValue("$id", Key(output.ChangeRequestId));
                exists.Parameters.AddWithValue("$type", outputType);
                exists.Parameters.AddWithValue("$version", output.Version);
                if (exists.ExecuteScalar() is not null)
                    throw new StorageVersionConflictException();
            }

            using (var insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText =
                    $"INSERT INTO {StorageSchema.AgentOutputs} (change_request_id, output_type, version, payload) " +
                    "VALUES ($id, $type, $version, $payload)";
                insert.Parameters.AddWithValue("$id", Key(output.ChangeRequestId));
                insert.Parameters.AddWithValue("$type", outputType);
                insert.Parameters.AddWithValue("$version", output.Version);
                insert.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(output, RecordJson.Options));
                insert.ExecuteNonQuery();
            }

            InsertAuditEntry(connection, transaction, auditEntry);
            transaction.Commit();
        });
    }

    public IReadOnlyList<T> GetAgentOutputs<T>(Guid changeRequestId) where T : IAgentOutput
    {
        var outputType = OutputType<T>();
        return Run(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                $"SELECT payload FROM {StorageSchema.AgentOutputs} " +
                "WHERE change_request_id = $id AND output_type = $type ORDER BY version";
            command.Parameters.AddWithValue("$id", Key(changeRequestId));
            command.Parameters.AddWithValue("$type", outputType);

            var outputs = new List<T>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
                outputs.Add(JsonSerializer.Deserialize<T>(reader.GetString(0), RecordJson.Options)!);
            return (IReadOnlyList<T>)outputs.AsReadOnly();
        });
    }

    public void Dispose()
    {
        lock (_syncRoot)
            Discard();
    }

    // ---- shared steps -------------------------------------------------------------------

    private static string OutputType<T>() => typeof(T).FullName ?? typeof(T).Name;

    private static string Key(Guid id) => id.ToString("D");

    private static void RequireSameChangeRequest(Guid changeRequestId, ChangeRequestAuditEntry auditEntry)
    {
        if (changeRequestId != auditEntry.ChangeRequestId)
            throw new ArgumentException(
                "The item and its audit entry must belong to the same change request.",
                nameof(auditEntry));
    }

    private static void RequireNew(SqliteConnection connection, SqliteTransaction transaction, Guid id)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT 1 FROM {StorageSchema.ChangeRequests} WHERE id = $id";
        command.Parameters.AddWithValue("$id", Key(id));
        if (command.ExecuteScalar() is not null)
            throw new InvalidOperationException($"A change request with id {id} already exists.");
    }

    private static void InsertChangeRequest(
        SqliteConnection connection, SqliteTransaction transaction, ChangeRequest changeRequest)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"INSERT INTO {StorageSchema.ChangeRequests} (id, payload) VALUES ($id, $payload)";
        command.Parameters.AddWithValue("$id", Key(changeRequest.ChangeRequestId));
        command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(changeRequest, RecordJson.Options));
        command.ExecuteNonQuery();
    }

    private static void ReplaceChangeRequest(
        SqliteConnection connection, SqliteTransaction transaction, ChangeRequest changeRequest)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"UPDATE {StorageSchema.ChangeRequests} SET payload = $payload WHERE id = $id";
        command.Parameters.AddWithValue("$id", Key(changeRequest.ChangeRequestId));
        command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(changeRequest, RecordJson.Options));
        if (command.ExecuteNonQuery() == 0)
            throw new InvalidOperationException(
                $"No change request with id {changeRequest.ChangeRequestId} exists to update.");
    }

    private static void InsertAuditEntry(
        SqliteConnection connection, SqliteTransaction transaction, ChangeRequestAuditEntry entry)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"INSERT INTO {StorageSchema.AuditEntries} (change_request_id, occurred_at_ticks, payload) " +
            "VALUES ($id, $ticks, $payload)";
        command.Parameters.AddWithValue("$id", Key(entry.ChangeRequestId));
        command.Parameters.AddWithValue("$ticks", entry.OccurredAt.Ticks);
        command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(entry, RecordJson.Options));
        command.ExecuteNonQuery();
    }

    // ---- opening, failure mapping -------------------------------------------------------

    private T Run<T>(Func<SqliteConnection, T> work)
    {
        lock (_syncRoot)
        {
            var connection = Open();
            try
            {
                return work(connection);
            }
            catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == PrimaryKeyViolation)
            {
                throw new StorageVersionConflictException();
            }
            catch (SqliteException)
            {
                Discard();
                throw new StorageUnavailableException(StorageFailureCategory.Unavailable);
            }
            catch (JsonException)
            {
                throw new StorageUnavailableException(StorageFailureCategory.Unavailable);
            }
        }
    }

    private void Run(Action<SqliteConnection> work) =>
        Run<object?>(connection =>
        {
            work(connection);
            return null;
        });

    private SqliteConnection Open()
    {
        if (_connection is not null)
            return _connection;

        SqliteConnection? connection = null;
        try
        {
            var path = _options.Path;
            if (string.IsNullOrWhiteSpace(path) || Directory.Exists(path))
                throw new StorageUnavailableException(StorageFailureCategory.Unavailable);

            var folder = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false,
                DefaultTimeout = 5
            }.ToString());
            connection.Open();

            Prepare(connection);
            _connection = connection;
            return connection;
        }
        catch (StorageUnavailableException)
        {
            connection?.Dispose();
            throw;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == NotADatabase)
        {
            connection?.Dispose();
            throw new StorageUnavailableException(StorageFailureCategory.UnsupportedLayout);
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException
                                       or ArgumentException or NotSupportedException)
        {
            connection?.Dispose();
            throw new StorageUnavailableException(StorageFailureCategory.Unavailable);
        }
    }

    /// <summary>
    /// A file with no tables gets the current layout in one transaction. A version-1 file with
    /// exactly its three tables is upgraded in place (one transaction). A current file with exactly
    /// our four tables is used. Anything else is refused having written nothing.
    /// </summary>
    private static void Prepare(SqliteConnection connection)
    {
        var version = Scalar(connection, "PRAGMA user_version;");
        var tables = Scalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';");

        if (version == 0 && tables == 0)
        {
            Execute(connection, StorageSchema.CreateStatements + $"\nPRAGMA user_version = {StorageSchema.LayoutVersion};");
        }
        else if (version == StorageSchema.LayoutVersion
                 && tables == StorageSchema.TableNames.Length
                 && HasTables(connection, StorageSchema.TableNames))
        {
            // Understood as is.
        }
        else if (version == 1
                 && tables == StorageSchema.V1TableNames.Length
                 && HasTables(connection, StorageSchema.V1TableNames))
        {
            Execute(connection, StorageSchema.UpgradeFromVersion1 + $"\nPRAGMA user_version = {StorageSchema.LayoutVersion};");
        }
        else
        {
            throw new StorageUnavailableException(StorageFailureCategory.UnsupportedLayout);
        }

        using var settings = connection.CreateCommand();
        settings.CommandText = "PRAGMA synchronous = FULL;";
        settings.ExecuteNonQuery();
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var transaction = connection.BeginTransaction();
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private static bool HasTables(SqliteConnection connection, string[] names)
    {
        using var command = connection.CreateCommand();
        var parameters = names.Select((_, i) => $"$t{i}").ToArray();
        command.CommandText =
            $"SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ({string.Join(", ", parameters)});";
        for (var i = 0; i < names.Length; i++)
            command.Parameters.AddWithValue(parameters[i], names[i]);
        return Convert.ToInt64(command.ExecuteScalar()) == names.Length;
    }

    private static long Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private void Discard()
    {
        _connection?.Dispose();
        _connection = null;
    }

    public IReadOnlyList<Guid> ListChangeRequestIds() =>
        Run(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT id FROM {StorageSchema.ChangeRequests} ORDER BY rowid";

            var ids = new List<Guid>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
                ids.Add(Guid.Parse(reader.GetString(0)));
            return (IReadOnlyList<Guid>)ids.AsReadOnly();
        });

    public AutomaticAttempt? GetAutomaticAttempt(Guid changeRequestId) =>
        Run(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                $"SELECT payload FROM {StorageSchema.AutomaticAttempts} WHERE change_request_id = $id";
            command.Parameters.AddWithValue("$id", Key(changeRequestId));
            return command.ExecuteScalar() is string payload
                ? JsonSerializer.Deserialize<AutomaticAttempt>(payload, RecordJson.Options)
                : null;
        });

    public void SaveAutomaticAttempt(AutomaticAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        Run(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                $"INSERT OR REPLACE INTO {StorageSchema.AutomaticAttempts} (change_request_id, payload) " +
                "VALUES ($id, $payload)";
            command.Parameters.AddWithValue("$id", Key(attempt.ChangeRequestId));
            command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(attempt, RecordJson.Options));
            command.ExecuteNonQuery();
        });
    }

    public void ClearAutomaticAttempt(Guid changeRequestId) =>
        Run(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                $"DELETE FROM {StorageSchema.AutomaticAttempts} WHERE change_request_id = $id";
            command.Parameters.AddWithValue("$id", Key(changeRequestId));
            command.ExecuteNonQuery();
        });
}
