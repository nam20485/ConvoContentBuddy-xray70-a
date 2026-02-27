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
    private readonly ILogger<HybridRetrieverService> _logger;

    public HybridRetrieverService(
        VectorSearchProvider vectorSearch,
        GraphTraversalProvider graphTraversal,
        SemanticKernelService semanticKernel,
        ModelFailoverManager failoverManager,
        ILogger<HybridRetrieverService> logger)
    {
        _vectorSearch = vectorSearch;
        _graphTraversal = graphTraversal;
        _semanticKernel = semanticKernel;
        _failoverManager = failoverManager;
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
            var embedding = await GenerateEmbeddingAsync(transcript);

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
                async () => await _semanticKernel.VerifyMatchAsync(transcript, topMatch.Payload["title"])
            );

            if (!isVerified)
            {
                _logger.LogWarning("Top vector match failed LLM verification");
                // Try second match
                if (vectorResults.Count > 1)
                {
                    var secondMatch = vectorResults[1];
                    isVerified = await _semanticKernel.VerifyMatchAsync(transcript, secondMatch.Payload["title"]);
                    if (isVerified)
                    {
                        topMatch = secondMatch;
                    }
                }
            }

            // Step 4: Graph Expansion - Get related problems
            var relatedProblems = await _graphTraversal.GetRelatedProblemsAsync(topMatch.Id, maxDepth: 2);

            // Step 5: Identify programming language
            var identification = await _semanticKernel.IdentifyProblemAsync(transcript);

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
    private async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        // Placeholder - in production, call Gemini embedding API
        // Return a 1536-dimensional vector
        _logger.LogDebug("Generating embedding for text: {Length} chars", text.Length);
        
        // Simulated embedding (random normalized vector)
        var random = new Random(text.GetHashCode());
        var embedding = new float[1536];
        for (int i = 0; i < 1536; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1);
        }
        
        // Normalize
        var magnitude = MathF.Sqrt(embedding.Sum(x => x * x));
        for (int i = 0; i < 1536; i++)
        {
            embedding[i] /= magnitude;
        }
        
        return embedding;
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
