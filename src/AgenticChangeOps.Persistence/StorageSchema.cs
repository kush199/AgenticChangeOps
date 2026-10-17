namespace AgenticChangeOps.Persistence;

/// <summary>
/// Layout version 2: four tables, versioned through <c>PRAGMA user_version</c>. Version 1 had the
/// first three; a version-1 file is upgraded in place by adding the attempt table.
/// </summary>
internal static class StorageSchema
{
    public const int LayoutVersion = 2;

    public const string ChangeRequests = "change_requests";
    public const string AuditEntries = "audit_entries";
    public const string AgentOutputs = "agent_outputs";
    public const string AutomaticAttempts = "automatic_attempts";

    public static readonly string[] V1TableNames = { ChangeRequests, AuditEntries, AgentOutputs };

    public static readonly string[] TableNames = { ChangeRequests, AuditEntries, AgentOutputs, AutomaticAttempts };

    /// <summary>The table added by layout version 2 (also used to upgrade a version-1 file).</summary>
    public const string UpgradeFromVersion1 = @"
CREATE TABLE automatic_attempts (
    change_request_id TEXT NOT NULL PRIMARY KEY,
    payload TEXT NOT NULL
);";

    public const string CreateStatements = @"
CREATE TABLE change_requests (
    id TEXT NOT NULL PRIMARY KEY,
    payload TEXT NOT NULL
);
CREATE TABLE audit_entries (
    seq INTEGER PRIMARY KEY AUTOINCREMENT,
    change_request_id TEXT NOT NULL,
    occurred_at_ticks INTEGER NOT NULL,
    payload TEXT NOT NULL
);
CREATE INDEX ix_audit_entries_order
    ON audit_entries (change_request_id, occurred_at_ticks, seq);
CREATE TABLE agent_outputs (
    change_request_id TEXT NOT NULL,
    output_type TEXT NOT NULL,
    version INTEGER NOT NULL,
    payload TEXT NOT NULL,
    PRIMARY KEY (change_request_id, output_type, version)
);" + UpgradeFromVersion1;
}
