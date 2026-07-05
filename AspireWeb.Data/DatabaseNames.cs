namespace AspireWeb.Data;

/// <summary>
/// Aspire connection names shared by the hosts that resolve the data source (API, Web,
/// migration service) via <c>AddNpgsqlDataSource</c>. The AppHost declares the database with
/// the same literal — it does not reference this library, so keep AppHost.cs in sync.
/// </summary>
public static class DatabaseNames
{
    /// <summary>The Postgres database resource name (see AppHost.cs <c>AddDatabase</c>).</summary>
    public const string AppDatabase = "appdb";
}
