namespace AgenticChangeOps.Persistence;

/// <summary>Layout version 1: three tables, versioned through <c>PRAGMA user_version</c>.</summary>
internal static class StorageSchema
{
    public const int LayoutVersion = 1;

    public const string ChangeRequests = "change_requests";
    public const string AuditEntries = "audit_entries";
    public const string AgentOutputs = "agent_outputs";

    public static readonly string[] TableNames = { ChangeRequests, AuditEntries, AgentOutputs };

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
);";
}
