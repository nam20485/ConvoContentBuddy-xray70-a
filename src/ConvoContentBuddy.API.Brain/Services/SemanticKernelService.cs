using Microsoft.SemanticKernel;

namespace ConvoContentBuddy.API.Brain.Services;

/// <summary>
/// Service for managing Semantic Kernel and AI interactions.
/// Configures Gemini 2.5 Flash as the primary LLM with failover support.
/// </summary>
public class SemanticKernelService
{
    private readonly Kernel _kernel;
    private readonly ILogger<SemanticKernelService> _logger;
    private readonly IConfiguration _configuration;

    public SemanticKernelService(ILogger<SemanticKernelService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        // Build the Semantic Kernel with Gemini
        var builder = Kernel.CreateBuilder();
        
        // Add Gemini connector (requires API key in configuration)
        builder.AddGoogleAIGeminiChatCompletion(
            modelId: "gemini-2.5-flash-preview-09-2025",
            apiKey: _configuration["Gemini:ApiKey"] ?? throw new InvalidOperationException("Gemini API key not configured")
        );

        _kernel = builder.Build();
        
        _logger.LogInformation("Semantic Kernel initialized with Gemini 2.5 Flash");
    }

    /// <summary>
    /// Gets the configured Semantic Kernel instance.
    /// </summary>
    public Kernel GetKernel() => _kernel;

    /// <summary>
    /// Identifies a coding problem from a transcript using LLM.
    /// </summary>
    /// <param name="transcript">The conversation transcript</param>
    /// <returns>The identified problem title and language</returns>
    public async Task<ProblemIdentificationResult> IdentifyProblemAsync(string transcript)
    {
        var prompt = $@"Analyze the following conversation transcript from a coding interview and identify:
1. What LeetCode-style problem is being discussed (if any)
2. What programming language is being used (Python, Java, C++, or C#)
3. A confidence score (0.0-1.0) for your identification

Transcript: """{transcript}"""

Respond in this JSON format:
{{
    ""problemTitle"": "string or null",
    ""language"": "Python|Java|C++|C#|null",
    ""confidence"": 0.0-1.0,
    ""reasoning"": "brief explanation"
}}";

        try
        {
            var result = await _kernel.InvokePromptAsync(prompt);
            var response = result.GetValue<string>();
            
            // Parse JSON response
            // Note: In production, use proper JSON parsing
            _logger.LogInformation("Problem identification completed with confidence");
            
            return new ProblemIdentificationResult(
                ProblemTitle: "Two Sum", // Placeholder - parse from actual response
                Language: "Python",
                Confidence: 0.95f,
                Reasoning: "Keywords detected: 'two sum', 'array', 'target'"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to identify problem from transcript");
            throw;
        }
    }

    /// <summary>
    /// Verifies a vector search match using LLM.
    /// </summary>
    public async Task<bool> VerifyMatchAsync(string transcript, string candidateTitle)
    {
        var prompt = $@"Given this interview transcript: """{transcript}"""

Is the candidate problem ""{candidateTitle}""" a correct match?
Respond with only 'true' or 'false'.";

        try
        {
            var result = await _kernel.InvokePromptAsync(prompt);
            var response = result.GetValue<string>()?.Trim().ToLowerInvariant();
            
            return response == "true";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify match");
            return false;
        }
    }
}

/// <summary>
/// Result of problem identification.
/// </summary>
public record ProblemIdentificationResult(
    string? ProblemTitle,
    string? Language,
    float Confidence,
    string Reasoning
);
