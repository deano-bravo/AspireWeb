var builder = DistributedApplication.CreateBuilder(args);

builder.AddKubernetesEnvironment("k8s");

var postgres = builder.AddPostgres("postgres");
if (builder.ExecutionContext.IsPublishMode)
{
    // Persistent data only when deploying; local runs and tests get an ephemeral container.
    postgres.WithDataVolume("aspireweb-postgres-data");
}
else
{
    postgres.WithPgWeb();
}

var appdb = postgres.AddDatabase("appdb");

var migrations = builder.AddProject<Projects.AspireWeb_MigrationService>("migrationservice")
    .WithReference(appdb)
    .WaitFor(appdb);

var apiService = builder.AddProject<Projects.AspireWeb_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithReference(appdb)
    .WaitForCompletion(migrations);

builder.AddProject<Projects.AspireWeb_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(appdb)
    .WaitForCompletion(migrations)
    .WithReference(apiService)
    .WaitFor(apiService);

await builder.Build().RunAsync();
