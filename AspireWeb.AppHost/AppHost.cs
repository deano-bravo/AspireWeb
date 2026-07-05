var builder = DistributedApplication.CreateBuilder(args);

builder.AddKubernetesEnvironment("k8s");

// Symmetric key (base64, 32 bytes) for the web-to-api JWT. Local dev value comes from
// user-secrets: dotnet user-secrets set Parameters:jwt-signing-key <key> --project AspireWeb.AppHost
var jwtSigningKey = builder.AddParameter("jwt-signing-key", secret: true);

// Env-var form of ApiJwtDefaults.SigningKeyConfigurationKey ("Auth:ApiJwt:SigningKey"): "__"
// is configuration's separator for environment variables. Kept as a literal because the AppHost
// deliberately does not reference ServiceDefaults.
const string jwtSigningKeyEnvVar = "Auth__ApiJwt__SigningKey";

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
    .WaitForCompletion(migrations)
    .WithEnvironment(jwtSigningKeyEnvVar, jwtSigningKey);

builder.AddProject<Projects.AspireWeb_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(appdb)
    .WaitForCompletion(migrations)
    .WithReference(apiService)
    .WaitFor(apiService)
    .WithEnvironment(jwtSigningKeyEnvVar, jwtSigningKey);

await builder.Build().RunAsync();
