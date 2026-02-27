using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ConvoContentBuddy.API.Brain.Services;

/// <summary>
/// Orchestrates the hybrid retrieval pipeline combining vector search, graph traversal, and LLM verification.
/// </summary>
public class HybridRetrieverService
{
    private readonly VectorSearchProvider _vectorSearch;
    private readonly GraphTraversalProvider _graphTraversal;
    private readonly SemanticKernelService _semanticKernel;
    private readonly ModelFailoverManager _failoverManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HybridRetrieverService> _logger;

    public HybridRetrieverService(
        VectorSearchProvider vectorSearch,
        GraphTraversalProvider graphTraversal,
        SemanticKernelService semanticKernel,
        ModelFailoverManager failoverManager,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<HybridRetrieverService> logger)
    {
        _vectorSearch = vectorSearch;
        _graphTraversal = graphTraversal;
        _semanticKernel = semanticKernel;
        _failoverManager = failoverManager;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Processes a transcript through the hybrid retrieval pipeline.
    /// Flow: Vector Search → Graph Expansion → LLM Verification → Search Grounding
    /// </summary>
    /// <param name="transcript">The user's conversation transcript</param>
    /// <returns>Retrieval result with solution or safe mode fallback</returns>
    public async Task<RetrievalResult> ProcessTranscriptAsync(string transcript)
    {
        try
        {
            _logger.LogInformation("Processing transcript through hybrid pipeline");

            // Step 1: Generate embedding from transcript (using Gemini text-embedding-004)
            var embedding = await _failoverManager.ExecuteWithFailoverAsync(
                () => GenerateEmbeddingAsync(transcript, useFallbackModel: false),
                () => GenerateEmbeddingAsync(transcript, useFallbackModel: true));

            // Step 2: Vector Search - Query Qdrant for top-3 candidates
            var vectorResults = await _vectorSearch.SearchAsync(embedding, topK: 3);
            
            if (!vectorResults.Any())
            {
                _logger.LogWarning("No vector matches found");
                return await _failoverManager.ExecuteSafeModeFallbackAsync(transcript, null);
            }

            var topMatch = vectorResults.First();
            
            // Step 3: LLM Verification - Confirm the match using Gemini
            var isVerified = await _failoverManager.ExecuteWithFailoverAsync(
                () => _semanticKernel.VerifyMatchAsync(transcript, topMatch.Payload["title"], useFallbackModel: false),
                () => _semanticKernel.VerifyMatchAsync(transcript, topMatch.Payload["title"], useFallbackModel: true)
            );

            if (!isVerified)
            {
                _logger.LogWarning("Top vector match failed LLM verification");
                // Try second match
                if (vectorResults.Count > 1)
                {
                    var secondMatch = vectorResults[1];
                    isVerified = await _failoverManager.ExecuteWithFailoverAsync(
                        () => _semanticKernel.VerifyMatchAsync(transcript, secondMatch.Payload["title"], useFallbackModel: false),
                        () => _semanticKernel.VerifyMatchAsync(transcript, secondMatch.Payload["title"], useFallbackModel: true)
                    );

                    if (isVerified)
                    {
                        topMatch = secondMatch;
                    }
                }
            }

            // Step 4: Graph Expansion - Get related problems
            var relatedProblems = await _graphTraversal.GetRelatedProblemsAsync(topMatch.Id, maxDepth: 2);

            // Step 5: Identify programming language
            var identification = await _failoverManager.ExecuteWithFailoverAsync(
                () => _semanticKernel.IdentifyProblemAsync(transcript, useFallbackModel: false),
                () => _semanticKernel.IdentifyProblemAsync(transcript, useFallbackModel: true)
            );

            // Step 6: Construct result
            var result = new RetrievalResult
            {
                Success = true,
                ProblemId = topMatch.Id,
                Title = topMatch.Payload["title"],
                Difficulty = topMatch.Payload.GetValueOrDefault("difficulty", "Unknown"),
                Confidence = topMatch.Score * (isVerified ? 1.0f : 0.7f),
                RelatedProblems = relatedProblems.ToList(),
                DetectedLanguage = identification.Language ?? "Python",
                ProcessingTime = DateTime.UtcNow
            };

            _logger.LogInformation("Hybrid retrieval completed: {Title} (confidence: {Confidence})", 
                result.Title, result.Confidence);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hybrid retrieval failed, executing failover");
            return await _failoverManager.ExecuteSafeModeFallbackAsync(transcript, ex);
        }
    }

    /// <summary>
    /// Generates an embedding for the transcript using Gemini text-embedding-004.
    /// </summary>
    private async Task<float[]> GenerateEmbeddingAsync(string text, bool useFallbackModel)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be empty when generating embeddings.", nameof(text));
        }

        var apiKey = useFallbackModel
            ? _configuration["Gemini:Tier2ApiKey"] ?? _configuration["Gemini:ApiKey"]
            : _configuration["Gemini:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Gemini API key not configured for embedding generation.");
        }

        var modelId = useFallbackModel
            ? _configuration["Gemini:Tier2EmbeddingModelId"]
                ?? _configuration["Gemini:EmbeddingModelId"]
                ?? "text-embedding-004"
            : _configuration["Gemini:EmbeddingModelId"] ?? "text-embedding-004";
        modelId = NormalizeModelId(modelId);

        var expectedDimensions = _configuration.GetValue<int?>("Qdrant:Dimensions") ?? 1536;
        var requestPayload = new GeminiEmbeddingRequest(
            Content: new GeminiContent([new GeminiPart(text)]),
            TaskType: "RETRIEVAL_QUERY",
            OutputDimensionality: expectedDimensions > 0 ? expectedDimensions : null);

        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{modelId}:embedContent?key={apiKey}";
        var client = _httpClientFactory.CreateClient(nameof(HybridRetrieverService));
        using var response = await client.PostAsJsonAsync(endpoint, requestPayload);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Gemini embedding API call failed ({(int)response.StatusCode}): {errorBody}");
        }

        var payload = await response.Content.ReadFromJsonAsync<GeminiEmbeddingResponse>();
        var values = payload?.Embedding?.Values;
        if (values is null || values.Count == 0)
        {
            throw new InvalidOperationException("Gemini embedding API returned no embedding values.");
        }

        var embedding = values.ToArray();
        if (expectedDimensions > 0 && embedding.Length != expectedDimensions)
        {
            embedding = ResizeVector(embedding, expectedDimensions);
        }

        NormalizeVector(embedding);
        _logger.LogDebug(
            "Generated embedding ({Dimensions} dims) using {Tier} model {ModelId}",
            embedding.Length,
            useFallbackModel ? "Tier2" : "Tier1",
            modelId);

        return embedding;
    }

    private static string NormalizeModelId(string modelId)
    {
        return modelId.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? modelId["models/".Length..]
            : modelId;
    }

    private static float[] ResizeVector(float[] vector, int targetDimensions)
    {
        if (vector.Length == targetDimensions)
        {
            return vector;
        }

        var resized = new float[targetDimensions];
        var lengthToCopy = Math.Min(vector.Length, targetDimensions);
        Array.Copy(vector, resized, lengthToCopy);
        return resized;
    }

    private static void NormalizeVector(float[] vector)
    {
        var magnitude = MathF.Sqrt(vector.Sum(x => x * x));
        if (magnitude == 0f)
        {
            throw new InvalidOperationException("Embedding vector magnitude is zero.");
        }

        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] /= magnitude;
        }
    }

    private sealed record GeminiEmbeddingRequest(
        [property: JsonPropertyName("content")] GeminiContent Content,
        [property: JsonPropertyName("taskType")] string TaskType,
        [property: JsonPropertyName("outputDimensionality")] int? OutputDimensionality);

    private sealed record GeminiContent([property: JsonPropertyName("parts")] IReadOnlyList<GeminiPart> Parts);

    private sealed record GeminiPart([property: JsonPropertyName("text")] string Text);

    private sealed class GeminiEmbeddingResponse
    {
        [JsonPropertyName("embedding")]
        public GeminiEmbeddingPayload? Embedding { get; init; }
    }

    private sealed class GeminiEmbeddingPayload
    {
        [JsonPropertyName("values")]
        public List<float>? Values { get; init; }
    }
}

/// <summary>
/// Result of the hybrid retrieval process.
/// </summary>
public class RetrievalResult
{
    public bool Success { get; set; }
    public bool SafeMode { get; set; }
    public int ProblemId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public List<RelatedProblem> RelatedProblems { get; set; } = new();
    public string DetectedLanguage { get; set; } = "Python";
    public DateTime ProcessingTime { get; set; }
    public string? ErrorMessage { get; set; }
}
