using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SecApper.Security.Models;

namespace SecApper.Security.Data;

public class SqliteDatabaseService : IDatabaseService
{
    private const int CurrentSchemaVersion = 2;
    private readonly string _databasePath;
    private readonly string _connectionString;

    public string DatabasePath => _databasePath;

    public SqliteDatabaseService(string? customDbPath = null)
    {
        _databasePath = customDbPath ?? ResolveDefaultDatabasePath();
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public static string ResolveDefaultDatabasePath()
    {
        try
        {
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string secApperDir = Path.Combine(programData, "SecApper", "FolderLocker");
            Directory.CreateDirectory(secApperDir);

            // Attempt to ensure directory is accessible to current user & admins
            TrySecureDirectory(secApperDir);

            return Path.Combine(secApperDir, "locker.db");
        }
        catch
        {
            // Fallback to local app data if ProgramData is inaccessible without admin rights
            string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string fallbackDir = Path.Combine(localApp, "SecApper", "FolderLocker");
            Directory.CreateDirectory(fallbackDir);
            return Path.Combine(fallbackDir, "locker.db");
        }
    }

    private static void TrySecureDirectory(string dirPath)
    {
        try
        {
            DirectoryInfo dInfo = new(dirPath);
            DirectorySecurity security = dInfo.GetAccessControl();
            // Grant Current User and Administrators full control
            var currentUser = WindowsIdentity.GetCurrent().User;
            if (currentUser != null)
            {
                security.AddAccessRule(new FileSystemAccessRule(
                    currentUser,
                    FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow));
            }

            SecurityIdentifier administrators = new(WellKnownSidType.BuiltinAdministratorsSid, null);
            security.AddAccessRule(new FileSystemAccessRule(
                administrators,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            dInfo.SetAccessControl(security);
        }
        catch
        {
            // Best effort; continue if already set or unprivileged
        }
    }

    private SqliteConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    public async Task<int> GetSchemaVersionAsync()
    {
        using var connection = CreateConnection();
        await connection.OpenAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task BackupDatabaseAsync(string destinationPath)
    {
        string? destDir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        using var srcConn = CreateConnection();
        await srcConn.OpenAsync();

        // Checkpoint WAL to flush all changes to main file before backup
        using (var walCmd = srcConn.CreateCommand())
        {
            walCmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            await walCmd.ExecuteNonQueryAsync();
        }

        // Use Sqlite online backup API for crash-safe online snapshot
        using var destConn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = destinationPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString());
        await destConn.OpenAsync();

        srcConn.BackupDatabase(destConn);
    }

    public async Task InitializeAsync()
    {
        string? dir = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var connection = CreateConnection();
        await connection.OpenAsync();

        // Enable WAL mode and foreign keys for durability and concurrency
        using (var pragmaCmd = connection.CreateCommand())
        {
            pragmaCmd.CommandText = "PRAGMA journal_mode = WAL; PRAGMA foreign_keys = ON;";
            await pragmaCmd.ExecuteNonQueryAsync();
        }

        int currentVer = 0;
        using (var verCmd = connection.CreateCommand())
        {
            verCmd.CommandText = "PRAGMA user_version;";
            var res = await verCmd.ExecuteScalarAsync();
            currentVer = Convert.ToInt32(res);
        }

        if (currentVer < CurrentSchemaVersion)
        {
            // Create automated backup before any schema migration
            if (File.Exists(_databasePath) && currentVer > 0)
            {
                string backupPath = _databasePath + ".backup";
                try
                {
                    await BackupDatabaseAsync(backupPath);
                }
                catch
                {
                    // If online backup fails, best-effort copy
                    try { File.Copy(_databasePath, backupPath, true); } catch { }
                }
            }

            // Apply migrations sequentially
            await MigrateSchemaAsync(connection, currentVer, CurrentSchemaVersion);
        }
    }

    private static async Task MigrateSchemaAsync(SqliteConnection connection, int fromVersion, int toVersion)
    {
        using var transaction = connection.BeginTransaction();

        if (fromVersion < 1)
        {
            // Initial base schema (Version 1)
            using var cmd1 = connection.CreateCommand();
            cmd1.Transaction = transaction;
            cmd1.CommandText = @"
                CREATE TABLE IF NOT EXISTS Folders (
                    Id TEXT PRIMARY KEY,
                    FolderPath TEXT NOT NULL UNIQUE,
                    FolderName TEXT NOT NULL,
                    Status INTEGER NOT NULL,
                    PasswordHash BLOB NOT NULL,
                    PasswordSalt BLOB NOT NULL,
                    PasswordAlgorithm TEXT NOT NULL,
                    PasswordIterations INTEGER NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL,
                    LastLockedAt TEXT,
                    LastUnlockedAt TEXT,
                    OriginalAclBackupId TEXT,
                    IconStatus INTEGER NOT NULL,
                    ProtectionMode INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS SecurityEvents (
                    Id TEXT PRIMARY KEY,
                    FolderId TEXT,
                    EventType TEXT NOT NULL,
                    ProcessName TEXT,
                    ProcessPath TEXT,
                    ProcessId INTEGER,
                    Description TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    Severity INTEGER NOT NULL,
                    ActionTaken TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS ProtectionPolicies (
                    Id TEXT PRIMARY KEY,
                    FolderId TEXT NOT NULL UNIQUE,
                    ProtectionEnabled INTEGER NOT NULL,
                    BlockUnknownProcesses INTEGER NOT NULL,
                    MassModificationThreshold INTEGER NOT NULL,
                    DeleteProtection INTEGER NOT NULL,
                    RenameProtection INTEGER NOT NULL,
                    ExtensionChangeProtection INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS PermissionBackups (
                    Id TEXT PRIMARY KEY,
                    FolderPath TEXT NOT NULL,
                    OriginalSddl TEXT NOT NULL,
                    OwnerSid TEXT,
                    GroupSid TEXT,
                    AreAccessRulesProtected INTEGER NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    HashVerification TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_folders_path ON Folders(FolderPath);
                CREATE INDEX IF NOT EXISTS idx_events_created ON SecurityEvents(CreatedAt DESC);
                CREATE INDEX IF NOT EXISTS idx_backups_path ON PermissionBackups(FolderPath);
            ";
            await cmd1.ExecuteNonQueryAsync();
        }

        if (fromVersion < 2)
        {
            // Migration to Version 2: Operation Journal & AppSettings
            using var cmd2 = connection.CreateCommand();
            cmd2.Transaction = transaction;
            cmd2.CommandText = @"
                CREATE TABLE IF NOT EXISTS OperationJournal (
                    Id TEXT PRIMARY KEY,
                    FolderId TEXT NOT NULL,
                    FolderPath TEXT NOT NULL,
                    OperationType INTEGER NOT NULL,
                    Status INTEGER NOT NULL,
                    OriginalSddl TEXT,
                    BackupId TEXT,
                    StartedAt TEXT NOT NULL,
                    CompletedAt TEXT,
                    ErrorMessage TEXT
                );

                CREATE TABLE IF NOT EXISTS AppSettings (
                    Key TEXT PRIMARY KEY,
                    Value TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_journal_folder ON OperationJournal(FolderId);
                CREATE INDEX IF NOT EXISTS idx_journal_status ON OperationJournal(Status);

                INSERT OR IGNORE INTO AppSettings (Key, Value, UpdatedAt) VALUES
                    ('AutoCheckUpdates', 'true', datetime('now')),
                    ('UpdateCheckFrequency', 'Daily', datetime('now')),
                    ('UpdateManifestUrl', 'https://raw.githubusercontent.com/adilmahboobalam/Secappers/main/latest.json', datetime('now')),
                    ('LastUpdateCheckTime', '', datetime('now'));
            ";
            await cmd2.ExecuteNonQueryAsync();
        }

        using var setVerCmd = connection.CreateCommand();
        setVerCmd.Transaction = transaction;
        setVerCmd.CommandText = $"PRAGMA user_version = {toVersion};";
        await setVerCmd.ExecuteNonQueryAsync();

        transaction.Commit();
    }

    public async Task<List<FolderRecord>> GetAllFoldersAsync()
    {
        var list = new List<FolderRecord>();
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Folders ORDER BY FolderName ASC;";
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(ReadFolderRecord(reader));
        }

        return list;
    }

    public async Task<FolderRecord?> GetFolderByIdAsync(string id)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Folders WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", id);
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return ReadFolderRecord(reader);
        }

        return null;
    }

    public async Task<FolderRecord?> GetFolderByPathAsync(string path)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Folders WHERE LOWER(FolderPath) = LOWER(@Path);";
        cmd.Parameters.AddWithValue("@Path", path);
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return ReadFolderRecord(reader);
        }

        return null;
    }

    public async Task AddFolderAsync(FolderRecord folder)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Folders (
                Id, FolderPath, FolderName, Status, PasswordHash, PasswordSalt,
                PasswordAlgorithm, PasswordIterations, CreatedAt, UpdatedAt,
                LastLockedAt, LastUnlockedAt, OriginalAclBackupId, IconStatus, ProtectionMode
            ) VALUES (
                @Id, @FolderPath, @FolderName, @Status, @PasswordHash, @PasswordSalt,
                @PasswordAlgorithm, @PasswordIterations, @CreatedAt, @UpdatedAt,
                @LastLockedAt, @LastUnlockedAt, @OriginalAclBackupId, @IconStatus, @ProtectionMode
            );";

        AddFolderParameters(cmd, folder);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateFolderAsync(FolderRecord folder)
    {
        folder.UpdatedAt = DateTime.UtcNow;

        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE Folders SET
                FolderPath = @FolderPath,
                FolderName = @FolderName,
                Status = @Status,
                PasswordHash = @PasswordHash,
                PasswordSalt = @PasswordSalt,
                PasswordAlgorithm = @PasswordAlgorithm,
                PasswordIterations = @PasswordIterations,
                UpdatedAt = @UpdatedAt,
                LastLockedAt = @LastLockedAt,
                LastUnlockedAt = @LastUnlockedAt,
                OriginalAclBackupId = @OriginalAclBackupId,
                IconStatus = @IconStatus,
                ProtectionMode = @ProtectionMode
            WHERE Id = @Id;";

        AddFolderParameters(cmd, folder);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteFolderAsync(string id)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Folders WHERE Id = @Id; DELETE FROM ProtectionPolicies WHERE FolderId = @Id; DELETE FROM OperationJournal WHERE FolderId = @Id;";
        cmd.Parameters.AddWithValue("@Id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task AddPermissionBackupAsync(PermissionBackup backup)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO PermissionBackups (
                Id, FolderPath, OriginalSddl, OwnerSid, GroupSid,
                AreAccessRulesProtected, CreatedAt, HashVerification
            ) VALUES (
                @Id, @FolderPath, @OriginalSddl, @OwnerSid, @GroupSid,
                @AreAccessRulesProtected, @CreatedAt, @HashVerification
            );";

        cmd.Parameters.AddWithValue("@Id", backup.Id);
        cmd.Parameters.AddWithValue("@FolderPath", backup.FolderPath);
        cmd.Parameters.AddWithValue("@OriginalSddl", backup.OriginalSddl);
        cmd.Parameters.AddWithValue("@OwnerSid", (object?)backup.OwnerSid ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@GroupSid", (object?)backup.GroupSid ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AreAccessRulesProtected", backup.AreAccessRulesProtected ? 1 : 0);
        cmd.Parameters.AddWithValue("@CreatedAt", backup.CreatedAt.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@HashVerification", backup.HashVerification);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<PermissionBackup?> GetPermissionBackupByIdAsync(string id)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM PermissionBackups WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", id);
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return ReadPermissionBackup(reader);
        }

        return null;
    }

    public async Task<PermissionBackup?> GetLatestPermissionBackupForFolderAsync(string folderPath)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM PermissionBackups WHERE LOWER(FolderPath) = LOWER(@FolderPath) ORDER BY CreatedAt DESC LIMIT 1;";
        cmd.Parameters.AddWithValue("@FolderPath", folderPath);
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return ReadPermissionBackup(reader);
        }

        return null;
    }

    public async Task DeletePermissionBackupAsync(string id)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM PermissionBackups WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<ProtectionPolicy?> GetPolicyForFolderAsync(string folderId)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM ProtectionPolicies WHERE FolderId = @FolderId;";
        cmd.Parameters.AddWithValue("@FolderId", folderId);
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new ProtectionPolicy
            {
                Id = reader.GetString(reader.GetOrdinal("Id")),
                FolderId = reader.GetString(reader.GetOrdinal("FolderId")),
                ProtectionEnabled = reader.GetInt32(reader.GetOrdinal("ProtectionEnabled")) == 1,
                BlockUnknownProcesses = reader.GetInt32(reader.GetOrdinal("BlockUnknownProcesses")) == 1,
                MassModificationThreshold = reader.GetInt32(reader.GetOrdinal("MassModificationThreshold")),
                DeleteProtection = reader.GetInt32(reader.GetOrdinal("DeleteProtection")) == 1,
                RenameProtection = reader.GetInt32(reader.GetOrdinal("RenameProtection")) == 1,
                ExtensionChangeProtection = reader.GetInt32(reader.GetOrdinal("ExtensionChangeProtection")) == 1
            };
        }

        return null;
    }

    public async Task UpsertPolicyAsync(ProtectionPolicy policy)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO ProtectionPolicies (
                Id, FolderId, ProtectionEnabled, BlockUnknownProcesses,
                MassModificationThreshold, DeleteProtection, RenameProtection, ExtensionChangeProtection
            ) VALUES (
                @Id, @FolderId, @ProtectionEnabled, @BlockUnknownProcesses,
                @MassModificationThreshold, @DeleteProtection, @RenameProtection, @ExtensionChangeProtection
            )
            ON CONFLICT(FolderId) DO UPDATE SET
                ProtectionEnabled = excluded.ProtectionEnabled,
                BlockUnknownProcesses = excluded.BlockUnknownProcesses,
                MassModificationThreshold = excluded.MassModificationThreshold,
                DeleteProtection = excluded.DeleteProtection,
                RenameProtection = excluded.RenameProtection,
                ExtensionChangeProtection = excluded.ExtensionChangeProtection;";

        cmd.Parameters.AddWithValue("@Id", policy.Id);
        cmd.Parameters.AddWithValue("@FolderId", policy.FolderId);
        cmd.Parameters.AddWithValue("@ProtectionEnabled", policy.ProtectionEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@BlockUnknownProcesses", policy.BlockUnknownProcesses ? 1 : 0);
        cmd.Parameters.AddWithValue("@MassModificationThreshold", policy.MassModificationThreshold);
        cmd.Parameters.AddWithValue("@DeleteProtection", policy.DeleteProtection ? 1 : 0);
        cmd.Parameters.AddWithValue("@RenameProtection", policy.RenameProtection ? 1 : 0);
        cmd.Parameters.AddWithValue("@ExtensionChangeProtection", policy.ExtensionChangeProtection ? 1 : 0);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task AddSecurityEventAsync(SecurityEvent evt)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO SecurityEvents (
                Id, FolderId, EventType, ProcessName, ProcessPath, ProcessId,
                Description, CreatedAt, Severity, ActionTaken
            ) VALUES (
                @Id, @FolderId, @EventType, @ProcessName, @ProcessPath, @ProcessId,
                @Description, @CreatedAt, @Severity, @ActionTaken
            );";

        cmd.Parameters.AddWithValue("@Id", evt.Id);
        cmd.Parameters.AddWithValue("@FolderId", (object?)evt.FolderId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@EventType", evt.EventType);
        cmd.Parameters.AddWithValue("@ProcessName", (object?)evt.ProcessName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ProcessPath", (object?)evt.ProcessPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ProcessId", (object?)evt.ProcessId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Description", evt.Description);
        cmd.Parameters.AddWithValue("@CreatedAt", evt.CreatedAt.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@Severity", (int)evt.Severity);
        cmd.Parameters.AddWithValue("@ActionTaken", evt.ActionTaken);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<SecurityEvent>> GetRecentSecurityEventsAsync(int limit = 100)
    {
        var list = new List<SecurityEvent>();
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM SecurityEvents ORDER BY CreatedAt DESC LIMIT @Limit;";
        cmd.Parameters.AddWithValue("@Limit", limit);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(ReadSecurityEvent(reader));
        }

        return list;
    }

    public async Task<List<SecurityEvent>> GetEventsForFolderAsync(string folderId, int limit = 50)
    {
        var list = new List<SecurityEvent>();
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM SecurityEvents WHERE FolderId = @FolderId ORDER BY CreatedAt DESC LIMIT @Limit;";
        cmd.Parameters.AddWithValue("@FolderId", folderId);
        cmd.Parameters.AddWithValue("@Limit", limit);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(ReadSecurityEvent(reader));
        }

        return list;
    }

    public async Task ClearEventsAsync()
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM SecurityEvents;";
        await cmd.ExecuteNonQueryAsync();
    }

    // --- Operation Journal ---

    public async Task<string> StartJournalEntryAsync(string folderId, string folderPath, JournalOperationType opType, string? originalSddl = null, string? backupId = null)
    {
        string id = Guid.NewGuid().ToString("N");
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO OperationJournal (
                Id, FolderId, FolderPath, OperationType, Status,
                OriginalSddl, BackupId, StartedAt
            ) VALUES (
                @Id, @FolderId, @FolderPath, @OperationType, @Status,
                @OriginalSddl, @BackupId, @StartedAt
            );";

        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@FolderId", folderId);
        cmd.Parameters.AddWithValue("@FolderPath", folderPath);
        cmd.Parameters.AddWithValue("@OperationType", (int)opType);
        cmd.Parameters.AddWithValue("@Status", (int)JournalStatus.Started);
        cmd.Parameters.AddWithValue("@OriginalSddl", (object?)originalSddl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@BackupId", (object?)backupId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@StartedAt", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));

        await cmd.ExecuteNonQueryAsync();
        return id;
    }

    public async Task UpdateJournalStatusAsync(string journalId, JournalStatus status, string? errorMessage = null)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE OperationJournal SET
                Status = @Status,
                CompletedAt = @CompletedAt,
                ErrorMessage = @ErrorMessage
            WHERE Id = @Id;";

        bool isCompleted = status == JournalStatus.Completed || status == JournalStatus.RolledBack || status == JournalStatus.Failed;
        cmd.Parameters.AddWithValue("@Id", journalId);
        cmd.Parameters.AddWithValue("@Status", (int)status);
        cmd.Parameters.AddWithValue("@CompletedAt", isCompleted ? DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) : DBNull.Value);
        cmd.Parameters.AddWithValue("@ErrorMessage", (object?)errorMessage ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<OperationJournalRecord>> GetIncompleteJournalEntriesAsync()
    {
        var list = new List<OperationJournalRecord>();
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT * FROM OperationJournal
            WHERE Status NOT IN (@Completed, @RolledBack)
            ORDER BY StartedAt ASC;";
        cmd.Parameters.AddWithValue("@Completed", (int)JournalStatus.Completed);
        cmd.Parameters.AddWithValue("@RolledBack", (int)JournalStatus.RolledBack);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(ReadJournalRecord(reader));
        }

        return list;
    }

    public async Task<OperationJournalRecord?> GetLatestJournalForFolderAsync(string folderId)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT * FROM OperationJournal
            WHERE FolderId = @FolderId
            ORDER BY StartedAt DESC LIMIT 1;";
        cmd.Parameters.AddWithValue("@FolderId", folderId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return ReadJournalRecord(reader);
        }

        return null;
    }

    // --- AppSettings ---

    public async Task<string?> GetSettingAsync(string key, string? defaultValue = null)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Value FROM AppSettings WHERE Key = @Key;";
        cmd.Parameters.AddWithValue("@Key", key);

        var val = await cmd.ExecuteScalarAsync();
        if (val == null || val == DBNull.Value)
            return defaultValue;

        return Convert.ToString(val);
    }

    public async Task SetSettingAsync(string key, string value)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO AppSettings (Key, Value, UpdatedAt)
            VALUES (@Key, @Value, @UpdatedAt)
            ON CONFLICT(Key) DO UPDATE SET
                Value = excluded.Value,
                UpdatedAt = excluded.UpdatedAt;";

        cmd.Parameters.AddWithValue("@Key", key);
        cmd.Parameters.AddWithValue("@Value", value);
        cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));

        await cmd.ExecuteNonQueryAsync();
    }

    // --- Helpers ---

    private static void AddFolderParameters(SqliteCommand cmd, FolderRecord folder)
    {
        cmd.Parameters.AddWithValue("@Id", folder.Id);
        cmd.Parameters.AddWithValue("@FolderPath", folder.FolderPath);
        cmd.Parameters.AddWithValue("@FolderName", folder.FolderName);
        cmd.Parameters.AddWithValue("@Status", (int)folder.Status);
        cmd.Parameters.Add("@PasswordHash", SqliteType.Blob).Value = folder.PasswordHash;
        cmd.Parameters.Add("@PasswordSalt", SqliteType.Blob).Value = folder.PasswordSalt;
        cmd.Parameters.AddWithValue("@PasswordAlgorithm", folder.PasswordAlgorithm);
        cmd.Parameters.AddWithValue("@PasswordIterations", folder.PasswordIterations);
        cmd.Parameters.AddWithValue("@CreatedAt", folder.CreatedAt.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@UpdatedAt", folder.UpdatedAt.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@LastLockedAt", folder.LastLockedAt.HasValue ? folder.LastLockedAt.Value.ToString("o", CultureInfo.InvariantCulture) : DBNull.Value);
        cmd.Parameters.AddWithValue("@LastUnlockedAt", folder.LastUnlockedAt.HasValue ? folder.LastUnlockedAt.Value.ToString("o", CultureInfo.InvariantCulture) : DBNull.Value);
        cmd.Parameters.AddWithValue("@OriginalAclBackupId", (object?)folder.OriginalAclBackupId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@IconStatus", (int)folder.IconStatus);
        cmd.Parameters.AddWithValue("@ProtectionMode", (int)folder.ProtectionMode);
    }

    private static FolderRecord ReadFolderRecord(SqliteDataReader reader)
    {
        return new FolderRecord
        {
            Id = reader.GetString(reader.GetOrdinal("Id")),
            FolderPath = reader.GetString(reader.GetOrdinal("FolderPath")),
            FolderName = reader.GetString(reader.GetOrdinal("FolderName")),
            Status = (FolderStatus)reader.GetInt32(reader.GetOrdinal("Status")),
            PasswordHash = (byte[])reader["PasswordHash"],
            PasswordSalt = (byte[])reader["PasswordSalt"],
            PasswordAlgorithm = reader.GetString(reader.GetOrdinal("PasswordAlgorithm")),
            PasswordIterations = reader.GetInt32(reader.GetOrdinal("PasswordIterations")),
            CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            UpdatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("UpdatedAt")), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            LastLockedAt = reader.IsDBNull(reader.GetOrdinal("LastLockedAt")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("LastLockedAt")), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            LastUnlockedAt = reader.IsDBNull(reader.GetOrdinal("LastUnlockedAt")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("LastUnlockedAt")), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            OriginalAclBackupId = reader.IsDBNull(reader.GetOrdinal("OriginalAclBackupId")) ? null : reader.GetString(reader.GetOrdinal("OriginalAclBackupId")),
            IconStatus = (IconStatus)reader.GetInt32(reader.GetOrdinal("IconStatus")),
            ProtectionMode = (ProtectionMode)reader.GetInt32(reader.GetOrdinal("ProtectionMode"))
        };
    }

    private static SecurityEvent ReadSecurityEvent(SqliteDataReader reader)
    {
        return new SecurityEvent
        {
            Id = reader.GetString(reader.GetOrdinal("Id")),
            FolderId = reader.IsDBNull(reader.GetOrdinal("FolderId")) ? null : reader.GetString(reader.GetOrdinal("FolderId")),
            EventType = reader.GetString(reader.GetOrdinal("EventType")),
            ProcessName = reader.IsDBNull(reader.GetOrdinal("ProcessName")) ? null : reader.GetString(reader.GetOrdinal("ProcessName")),
            ProcessPath = reader.IsDBNull(reader.GetOrdinal("ProcessPath")) ? null : reader.GetString(reader.GetOrdinal("ProcessPath")),
            ProcessId = reader.IsDBNull(reader.GetOrdinal("ProcessId")) ? null : reader.GetInt32(reader.GetOrdinal("ProcessId")),
            Description = reader.GetString(reader.GetOrdinal("Description")),
            CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            Severity = (EventSeverity)reader.GetInt32(reader.GetOrdinal("Severity")),
            ActionTaken = reader.GetString(reader.GetOrdinal("ActionTaken"))
        };
    }

    private static PermissionBackup ReadPermissionBackup(SqliteDataReader reader)
    {
        return new PermissionBackup
        {
            Id = reader.GetString(reader.GetOrdinal("Id")),
            FolderPath = reader.GetString(reader.GetOrdinal("FolderPath")),
            OriginalSddl = reader.GetString(reader.GetOrdinal("OriginalSddl")),
            OwnerSid = reader.IsDBNull(reader.GetOrdinal("OwnerSid")) ? null : reader.GetString(reader.GetOrdinal("OwnerSid")),
            GroupSid = reader.IsDBNull(reader.GetOrdinal("GroupSid")) ? null : reader.GetString(reader.GetOrdinal("GroupSid")),
            AreAccessRulesProtected = reader.GetInt32(reader.GetOrdinal("AreAccessRulesProtected")) == 1,
            CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            HashVerification = reader.GetString(reader.GetOrdinal("HashVerification"))
        };
    }

    private static OperationJournalRecord ReadJournalRecord(SqliteDataReader reader)
    {
        return new OperationJournalRecord
        {
            Id = reader.GetString(reader.GetOrdinal("Id")),
            FolderId = reader.GetString(reader.GetOrdinal("FolderId")),
            FolderPath = reader.GetString(reader.GetOrdinal("FolderPath")),
            OperationType = (JournalOperationType)reader.GetInt32(reader.GetOrdinal("OperationType")),
            Status = (JournalStatus)reader.GetInt32(reader.GetOrdinal("Status")),
            OriginalSddl = reader.IsDBNull(reader.GetOrdinal("OriginalSddl")) ? null : reader.GetString(reader.GetOrdinal("OriginalSddl")),
            BackupId = reader.IsDBNull(reader.GetOrdinal("BackupId")) ? null : reader.GetString(reader.GetOrdinal("BackupId")),
            StartedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("StartedAt")), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            CompletedAt = reader.IsDBNull(reader.GetOrdinal("CompletedAt")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("CompletedAt")), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            ErrorMessage = reader.IsDBNull(reader.GetOrdinal("ErrorMessage")) ? null : reader.GetString(reader.GetOrdinal("ErrorMessage"))
        };
    }
}
