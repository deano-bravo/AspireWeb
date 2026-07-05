using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AspireWeb.Data;

/// <summary>Shared translation of Npgsql error conditions surfaced through EF Core.</summary>
public static class PostgresErrors
{
    /// <summary>
    /// True when the exception wraps a Postgres unique-violation (23505) — a write that lost the
    /// race to a unique index. Callers turn this into a friendly 409/validation response rather
    /// than letting the raw <see cref="DbUpdateException"/> escape.
    /// </summary>
    public static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
