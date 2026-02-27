using Aspire.Hosting;
using Aspire.Hosting.Lifecycle;

var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure Services
var redis = builder.AddRedis("redis")
    .WithDataVolume()
    .WithRedisInsight();

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin();

var qdrant = builder.AddQdrant("qdrant")
    .WithDataVolume();

// Database connections
var postgresDb = postgres.AddDatabase("convocontentbuddy");

// API Brain Service - Triple Modular Redundancy (3 replicas)
var apiBrain = builder.AddProject<Projects.ConvoContentBuddy_API_Brain>("api-brain")
    .WithReference(redis)
    .WithReference(postgresDb)
    .WithReference(qdrant)
    .WithReplicas(3)
    .WithHealthCheck("/health");

// Web UI
var webUi = builder.AddProject<Projects.ConvoContentBuddy_UI_Web>("web-ui")
    .WithReference(apiBrain)
    .WithReference(redis)
    .WithHealthCheck("/health");

// Data Seeder (background worker)
var seeder = builder.AddProject<Projects.ConvoContentBuddy_Data_Seeder>("data-seeder")
    .WithReference(postgresDb)
    .WithReference(qdrant);

// Wait for dependencies before starting
apiBrain.WaitFor(redis);
apiBrain.WaitFor(postgres);
apiBrain.WaitFor(qdrant);

webUi.WaitFor(apiBrain);

await builder.Build().RunAsync();
