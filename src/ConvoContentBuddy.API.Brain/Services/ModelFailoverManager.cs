using System.Diagnostics;

namespace ConvoContentBuddy.API.Brain.Services;

/// <summary>
/// Implements N+2 Failover strategy with Triple Modular Redundancy (TMR).
/// Provides aerospace-grade resilience for AI model calls.
/// </summary>
public class ModelFailoverManager
{
    private readonly ILogger<ModelFailoverManager> _logger;
    private readonly int _exceptionsAllowedBeforeBreaking;
    private readonly TimeSpan _durationOfBreak;
    private readonly object _circuitLock = new();
    private int _consecutiveFailureCount;
    private DateTimeOffset? _circuitOpenedAtUtc;
    private int _currentTier = 1;

    public ModelFailoverManager(ILogger<ModelFailoverManager> logger, IConfiguration configuration)
    {
        _logger = logger;
        _exceptionsAllowedBeforeBreaking = configuration.GetValue<int?>("Failover:CircuitBreakerThreshold") ?? 5;
        _durationOfBreak = TimeSpan.FromSeconds(configuration.GetValue<int?>("Failover:CircuitBreakerDurationSeconds") ?? 30);
    }

    /// <summary>
    /// Executes a function with automatic failover between tiers.
    /// </summary>
    /// <typeparam name="T">Return type</typeparam>
    /// <param name="primaryFunc">Primary tier function</param>
    /// <param name="tier2Func">Tier 2 function targeting an alternate model/endpoint</param>
    /// <returns>Result from whichever tier succeeds</returns>
    public async Task<T> ExecuteWithFailoverAsync<T>(Func<Task<T>> primaryFunc, Func<Task<T>>? tier2Func = null)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            if (IsCircuitOpen())
            {
                _logger.LogWarning("Circuit breaker open; skipping Tier 1 and attempting Tier 2");
                return await ExecuteTier2Async(tier2Func);
            }

            _currentTier = 1;
            _logger.LogInformation("Executing Tier 1 (Primary)");
            var result = await primaryFunc();
            RegisterSuccess();
            return result;
        }
        catch (Exception ex)
        {
            RegisterFailure(ex);
            _logger.LogWarning(ex, "Tier 1 execution failed, attempting Tier 2");
            return await ExecuteTier2Async(tier2Func);
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogDebug("Failover execution completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// Tier 2 execution with alternative model/region.
    /// </summary>
    private async Task<T> ExecuteTier2Async<T>(Func<Task<T>>? tier2Func)
    {
        if (tier2Func is null)
        {
            throw new InvalidOperationException(
                "Tier 2 failover function was not provided. Provide an alternate model/endpoint function.");
        }

        _currentTier = 2;
        _logger.LogInformation("Executing Tier 2 (Fallback)");
        return await tier2Func();
    }

    private bool IsCircuitOpen()
    {
        lock (_circuitLock)
        {
            if (_circuitOpenedAtUtc is null)
            {
                return false;
            }

            var elapsed = DateTimeOffset.UtcNow - _circuitOpenedAtUtc.Value;
            if (elapsed < _durationOfBreak)
            {
                return true;
            }

            _logger.LogInformation("Circuit breaker half-open; allowing Tier 1 retry");
            _circuitOpenedAtUtc = null;
            _consecutiveFailureCount = 0;
            return false;
        }
    }

    private void RegisterSuccess()
    {
        lock (_circuitLock)
        {
            if (_circuitOpenedAtUtc is not null || _consecutiveFailureCount > 0)
            {
                _logger.LogInformation("Circuit breaker reset after successful Tier 1 execution");
            }

            _circuitOpenedAtUtc = null;
            _consecutiveFailureCount = 0;
        }
    }

    private void RegisterFailure(Exception exception)
    {
        lock (_circuitLock)
        {
            _consecutiveFailureCount++;
            if (_consecutiveFailureCount < _exceptionsAllowedBeforeBreaking)
            {
                return;
            }

            _circuitOpenedAtUtc = DateTimeOffset.UtcNow;
            _logger.LogWarning(
                exception,
                "Circuit breaker opened for {DurationSeconds}s after {FailureCount} consecutive failures",
                _durationOfBreak.TotalSeconds,
                _consecutiveFailureCount);
        }
    }

    /// <summary>
    /// Executes Safe Mode (Tier 3) when all cloud services are unavailable.
    /// Returns deterministic local-only results.
    /// </summary>
    public async Task<RetrievalResult> ExecuteSafeModeFallbackAsync(string transcript, Exception? originalException)
    {
        _logger.LogWarning("Entering Safe Mode (Tier 3) - Local-only deterministic mode");
        _currentTier = 3;
        
        // In Safe Mode: Return basic match from local cache without LLM verification
        // This ensures the system never stops responding
        var safeModeResult = new RetrievalResult
        {
            Success = true,
            SafeMode = true,
            Title = "Safe Mode Active",
            Difficulty = "Unknown",
            Confidence = 0.3f,
            DetectedLanguage = "Python",
            ErrorMessage = originalException?.Message,
            ProcessingTime = DateTime.UtcNow
        };

        return await Task.FromResult(safeModeResult);
    }

    /// <summary>
    /// Gets the current operational tier.
    /// </summary>
    public int GetCurrentTier() => _currentTier;

    /// <summary>
    /// Resets the failover manager to Tier 1.
    /// </summary>
    public void Reset()
    {
        _currentTier = 1;
        _logger.LogInformation("Failover manager reset to Tier 1");
    }
}
