namespace AspireWeb.Data.DesignTime;

internal static class DesignTimeConnection
{
    /// <summary>
    /// dotnet-ef only needs the provider + model to scaffold migrations, so no real
    /// credentials are required; set the env var to run design-time database commands.
    /// </summary>
    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("ASPIREWEB_DESIGNTIME_CONNECTIONSTRING")
        ?? "Host=localhost;Database=appdb;Username=postgres";
}
