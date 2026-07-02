var builder = DistributedApplication.CreateBuilder(args);

builder.AddKubernetesEnvironment("k8s");

var apiService = builder.AddProject<Projects.AspireWeb_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.AspireWeb_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

await builder.Build().RunAsync();
