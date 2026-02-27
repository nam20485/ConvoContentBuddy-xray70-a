using System.Text.Json;
using Microsoft.SemanticKernel;

namespace ConvoContentBuddy.API.Brain.Services;

/// <summary>
/// Service for managing Semantic Kernel and AI interactions.
/// Configures Gemini 2.5 Flash as the primary LLM with failover support.
/// </summary>
public class SemanticKernelService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Kernel _primaryKernel;
    private readonly Kernel _tier2Kernel;
    private readonly ILogger<SemanticKernelService> _logger;
    private readonly string _primaryModelId;
    private readonly string _tier2ModelId;

    public SemanticKernelService(ILogger<SemanticKernelService> logger, IConfiguration configuration)
    {
        _logger = logger;

        var primaryApiKey = configuration["Gemini:ApiKey"]
            ?? throw new InvalidOperationException("Gemini API key not configured");
        _primaryModelId = configuration["Gemini:ModelId"] ?? "gemini-2.5-flash-preview-09-2025";

        _tier2ModelId = configuration["Gemini:Tier2ModelId"] ?? "gemini-2.0-flash";
        var tier2ApiKey = configuration["Gemini:Tier2ApiKey"] ?? primaryApiKey;

        _primaryKernel = BuildKernel(_primaryModelId, primaryApiKey);
        _tier2Kernel = BuildKernel(_tier2ModelId, tier2ApiKey);

        _logger.LogInformation(
            "Semantic Kernel initialized. Tier1 model: {Tier1Model}, Tier2 model: {Tier2Model}",
            _primaryModelId,
            _tier2ModelId);
    }

    /// <summary>
    /// Gets the configured Semantic Kernel instance.
    /// </summary>
    public Kernel GetKernel() => _primaryKernel;

    /// <summary>
    /// Identifies a coding problem from a transcript using LLM.
    /// </summary>
    /// <param name="transcript">The conversation transcript</param>
    /// <returns>The identified problem title and language</returns>
    public async Task<ProblemIdentificationResult> IdentifyProblemAsync(string transcript, bool useFallbackModel = false)
    {
        var selectedKernel = useFallbackModel ? _tier2Kernel : _primaryKernel;
        var selectedModel = useFallbackModel ? _tier2ModelId : _primaryModelId;

        var prompt = $@"Analyze the following coding interview transcript and identify:
1. The LeetCode-style problem title (or null if unknown)
2. The programming language (Python, Java, C++, C#, or null)
3. A confidence score from 0.0 to 1.0
4. Brief reasoning

Transcript:
{transcript}

Return only a JSON object with these properties:
- problemTitle
- language
- confidence
- reasoning

Do not include markdown fences or additional text.";

        try
        {
            var result = await selectedKernel.InvokePromptAsync(prompt);
            var response = result.GetValue<string>();
            var parsed = ParseIdentificationResponse(response);

            _logger.LogInformation(
                "Problem identification completed using {Model} with confidence {Confidence}",
                selectedModel,
                parsed.Confidence);

            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to identify problem from transcript using model {Model}", selectedModel);
            throw;
        }
    }

    /// <summary>
    /// Verifies a vector search match using LLM.
    /// </summary>
    public async Task<bool> VerifyMatchAsync(string transcript, string candidateTitle, bool useFallbackModel = false)
    {
        var selectedKernel = useFallbackModel ? _tier2Kernel : _primaryKernel;

        var prompt = $@"Given this interview transcript:
{transcript}

Is the candidate problem ""{candidateTitle}"" a correct match?
Respond with only 'true' or 'false'.";

        try
        {
            var result = await selectedKernel.InvokePromptAsync(prompt);
            var response = result.GetValue<string>();
            return TryParseBoolean(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify match");
            return false;
        }
    }

    private static Kernel BuildKernel(string modelId, string apiKey)
    {
        var builder = Kernel.CreateBuilder();
#pragma warning disable SKEXP0070
        builder.AddGoogleAIGeminiChatCompletion(modelId: modelId, apiKey: apiKey);
#pragma warning restore SKEXP0070
        return builder.Build();
    }

    private static ProblemIdentificationResult ParseIdentificationResponse(string? response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            throw new InvalidOperationException("Model returned an empty response for problem identification.");
        }

        var json = ExtractJsonObject(response);
        var parsed = JsonSerializer.Deserialize<ProblemIdentificationPayload>(json, JsonOptions)
            ?? throw new InvalidOperationException("Model returned an invalid JSON payload for problem identification.");

        var confidence = float.Clamp(parsed.Confidence, 0f, 1f);

        return new ProblemIdentificationResult(
            ProblemTitle: parsed.ProblemTitle?.Trim(),
            Language: string.IsNullOrWhiteSpace(parsed.Language) ? null : parsed.Language.Trim(),
            Confidence: confidence,
            Reasoning: string.IsNullOrWhiteSpace(parsed.Reasoning) ? "No reasoning provided." : parsed.Reasoning.Trim()
        );
    }

    private static string ExtractJsonObject(string response)
    {
        var start = response.IndexOf('{');
        var end = response.LastIndexOf('}');

        if (start < 0 || end <= start)
        {
            throw new InvalidOperationException($"Model response is not valid JSON: {response}");
        }

        return response[start..(end + 1)];
    }

    private static bool TryParseBoolean(string? response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return false;
        }

        var trimmed = response.Trim();
        if (bool.TryParse(trimmed, out var direct))
        {
            return direct;
        }

        var firstToken = trimmed.Split(['\n', '\r', ' '], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (firstToken is not null && bool.TryParse(firstToken, out var firstTokenValue))
        {
            return firstTokenValue;
        }

        if (trimmed.Contains("\"isMatch\":true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (trimmed.Contains("\"isMatch\":false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return false;
    }

    private sealed class ProblemIdentificationPayload
    {
        public string? ProblemTitle { get; init; }
        public string? Language { get; init; }
        public float Confidence { get; init; }
        public string? Reasoning { get; init; }
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
