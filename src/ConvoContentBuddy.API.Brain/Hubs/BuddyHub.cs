using Microsoft.AspNetCore.SignalR;

namespace ConvoContentBuddy.API.Brain.Hubs;

/// <summary>
/// SignalR hub for real-time communication between Blazor UI and Brain API.
/// Handles transcript streaming and solution push notifications.
/// </summary>
public class BuddyHub : Hub<IBuddyHubClient>
{
    private readonly ILogger<BuddyHub> _logger;

    public BuddyHub(ILogger<BuddyHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called when a client connects to the hub.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await Clients.Caller.ReceiveSystemMessage("Connected to ConvoContentBuddy");
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogError(exception, "Client disconnected with error: {ConnectionId}", Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Receives transcript chunks from the Blazor client.
    /// </summary>
    /// <param name="transcriptChunk">The transcript text chunk</param>
    public async Task SendTranscriptChunk(string transcriptChunk)
    {
        _logger.LogDebug("Received transcript chunk from {ConnectionId}: {Chunk}", 
            Context.ConnectionId, 
            transcriptChunk[..Math.Min(50, transcriptChunk.Length)]);
        
        // Broadcast to all clients (for testing) or process via Brain
        await Clients.Caller.ReceiveTranscriptAck($"Received: {transcriptChunk[..Math.Min(50, transcriptChunk.Length)]}...");
    }

    /// <summary>
    /// Pushes a solution to all connected clients.
    /// </summary>
    public async Task PushSolution(SolutionDto solution)
    {
        _logger.LogInformation("Pushing solution: {Title}", solution.Title);
        await Clients.All.ReceiveSolution(solution);
    }

    /// <summary>
    /// Pushes a safe mode alert to all clients.
    /// </summary>
    public async Task PushSafeModeAlert(string message)
    {
        _logger.LogWarning("Pushing safe mode alert: {Message}", message);
        await Clients.All.ReceiveSafeModeAlert(message);
    }
}

/// <summary>
/// Interface defining client methods callable from the server.
/// </summary>
public interface IBuddyHubClient
{
    Task ReceiveTranscriptAck(string message);
    Task ReceiveSolution(SolutionDto solution);
    Task ReceiveSafeModeAlert(string message);
    Task ReceiveSystemMessage(string message);
}

/// <summary>
/// Data transfer object for solutions.
/// </summary>
public record SolutionDto(
    int Id,
    string Title,
    string TitleSlug,
    string Difficulty,
    string Description,
    string[] Topics,
    Dictionary<string, string> CodeSnippets,
    string TimeComplexity,
    string SpaceComplexity,
    float Confidence
);
