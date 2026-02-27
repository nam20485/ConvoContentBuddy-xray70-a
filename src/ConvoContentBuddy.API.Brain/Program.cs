using ConvoContentBuddy.API.Brain.Hubs;
using ConvoContentBuddy.API.Brain.Services;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults from Aspire
builder.AddServiceDefaults();

// Add SignalR with Redis backplane for multi-instance synchronization
builder.Services.AddSignalR()
    .AddStackExchangeRedis(options =>
    {
        options.Configuration.Configuration = builder.Configuration.GetConnectionString("redis");
    });

// Add Semantic Kernel services
builder.Services.AddSingleton<SemanticKernelService>();

// Add hybrid retrieval services
builder.Services.AddSingleton<VectorSearchProvider>();
builder.Services.AddSingleton<GraphTraversalProvider>();
builder.Services.AddSingleton<HybridRetrieverService>();
builder.Services.AddSingleton<ModelFailoverManager>();

// Add CORS for Blazor WASM
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure middleware
app.UseCors("CorsPolicy");

// Map default Aspire endpoints
app.MapDefaultEndpoints();

// Map SignalR hub
app.MapHub<BuddyHub>("/buddyhub");

// Map API endpoints
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.MapPost("/api/transcript", async (TranscriptRequest request, HybridRetrieverService retriever) =>
{
    // Process transcript and return problem matches
    var result = await retriever.ProcessTranscriptAsync(request.Transcript);
    return Results.Ok(result);
});

app.Run();

// Request/Response models
public record TranscriptRequest(string Transcript);
