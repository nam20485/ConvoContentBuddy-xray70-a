using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace ConvoContentBuddy.API.Brain.Services;

/// <summary>
/// Service for performing vector similarity searches against Qdrant.
/// </summary>
public class VectorSearchProvider
{
    private readonly QdrantClient _qdrantClient;
    private readonly ILogger<VectorSearchProvider> _logger;
    private const string CollectionName = "leetcode_problems";

    public VectorSearchProvider(IConfiguration configuration, ILogger<VectorSearchProvider> logger)
    {
        _logger = logger;
        
        var qdrantUrl = configuration.GetConnectionString("qdrant");
        _qdrantClient = new QdrantClient(qdrantUrl ?? "localhost:6334");
        
        _logger.LogInformation("VectorSearchProvider initialized with Qdrant at {Url}", qdrantUrl);
    }

    /// <summary>
    /// Searches for similar problems using vector similarity.
    /// </summary>
    /// <param name="embedding">The embedding vector to search with</param>
    /// <param name="topK">Number of results to return</param>
    /// <returns>List of matching problems with similarity scores</returns>
    public async Task<IReadOnlyList<ScoredProblem>> SearchAsync(float[] embedding, int topK = 3)
    {
        try
        {
            var results = await _qdrantClient.SearchAsync(
                collectionName: CollectionName,
                vector: embedding,
                limit: (ulong)topK
            );

            var scoredProblems = results.Select(r => new ScoredProblem(
                Id: (int)r.Id.Num,
                Score: r.Score,
                Payload: r.Payload.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.KindCase switch
                    {
                        Value.KindOneofCase.StringValue => kvp.Value.StringValue,
                        Value.KindOneofCase.IntegerValue => kvp.Value.IntegerValue.ToString(),
                        Value.KindOneofCase.DoubleValue => kvp.Value.DoubleValue.ToString(),
                        _ => kvp.Value.ToString()
                    }
                )
            )).ToList();

            _logger.LogInformation("Vector search returned {Count} results", scoredProblems.Count);
            return scoredProblems;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vector search failed");
            throw;
        }
    }

    /// <summary>
    /// Creates the collection if it doesn't exist.
    /// </summary>
    public async Task EnsureCollectionExistsAsync()
    {
        try
        {
            var collections = await _qdrantClient.ListCollectionsAsync();
            if (!collections.Contains(CollectionName))
            {
                await _qdrantClient.CreateCollectionAsync(
                    collectionName: CollectionName,
                    vectorsConfig: new VectorParams
                    {
                        Size = 1536,
                        Distance = Distance.Cosine
                    }
                );
                _logger.LogInformation("Created Qdrant collection: {CollectionName}", CollectionName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure collection exists");
            throw;
        }
    }

    /// <summary>
    /// Upserts a problem into the vector database.
    /// </summary>
    public async Task UpsertProblemAsync(int id, float[] embedding, Dictionary<string, object> payload)
    {
        var point = new PointStruct
        {
            Id = new PointId { Num = (ulong)id },
            Vectors = embedding,
            Payload = payload.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value switch
                {
                    string s => new Value { StringValue = s },
                    int i => new Value { IntegerValue = i },
                    double d => new Value { DoubleValue = d },
                    float f => new Value { DoubleValue = f },
                    _ => new Value { StringValue = kvp.Value.ToString() }
                }
            )
        };

        await _qdrantClient.UpsertAsync(CollectionName, [point]);
    }
}

/// <summary>
/// Represents a scored problem from vector search.
/// </summary>
public record ScoredProblem(
    int Id,
    float Score,
    Dictionary<string, string> Payload
);
