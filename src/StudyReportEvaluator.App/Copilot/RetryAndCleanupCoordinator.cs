using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using StudyReportEvaluator.App.Logging;
using StudyReportEvaluator.Core.Domain;

namespace StudyReportEvaluator.App.Copilot;

public enum EvaluationAttemptFailureKind
{
    SchemaInvalid,
    Network,
    Timeout,
    Authentication,
    Cancelled,
    Fatal,
    Cleanup,
}

public abstract class EvaluationAttemptException : Exception
{
    private protected EvaluationAttemptException(
        EvaluationAttemptFailureKind failureKind,
        string safeMessage)
        : base(safeMessage)
    {
        FailureKind = failureKind;
    }

    public EvaluationAttemptFailureKind FailureKind { get; }
}

public sealed class EvaluationSchemaException : EvaluationAttemptException
{
    public EvaluationSchemaException()
        : base(EvaluationAttemptFailureKind.SchemaInvalid, "The structured evaluation result is invalid.")
    {
    }
}

public sealed class EvaluationNetworkException : EvaluationAttemptException
{
    public EvaluationNetworkException()
        : base(EvaluationAttemptFailureKind.Network, "The evaluation transport failed.")
    {
    }
}

public sealed class EvaluationAttemptTimeoutException : EvaluationAttemptException
{
    public EvaluationAttemptTimeoutException()
        : base(EvaluationAttemptFailureKind.Timeout, "The evaluation attempt timed out.")
    {
    }
}

public sealed class EvaluationAuthenticationException : EvaluationAttemptException
{
    public EvaluationAuthenticationException()
        : base(EvaluationAttemptFailureKind.Authentication, "Copilot authentication is required.")
    {
    }
}

public sealed class EvaluationFatalException : EvaluationAttemptException
{
    public EvaluationFatalException()
        : base(EvaluationAttemptFailureKind.Fatal, "The evaluation runtime failed.")
    {
    }
}

public sealed class EvaluationCleanupException : EvaluationAttemptException
{
    public EvaluationCleanupException()
        : base(EvaluationAttemptFailureKind.Cleanup, "The ephemeral session cleanup failed.")
    {
    }
}

public sealed class EvaluationTokenUsage
{
    public EvaluationTokenUsage(
        bool isAvailable,
        long inputTokens,
        long outputTokens,
        long reasoningTokens,
        long cacheReadTokens,
        long cacheWriteTokens)
    {
        if (inputTokens < 0
            || outputTokens < 0
            || reasoningTokens < 0
            || cacheReadTokens < 0
            || cacheWriteTokens < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(inputTokens));
        }

        IsAvailable = isAvailable;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        ReasoningTokens = reasoningTokens;
        CacheReadTokens = cacheReadTokens;
        CacheWriteTokens = cacheWriteTokens;
    }

    public static EvaluationTokenUsage Unavailable { get; } = new(false, 0, 0, 0, 0, 0);

    public bool IsAvailable { get; }

    public long InputTokens { get; }

    public long OutputTokens { get; }

    public long ReasoningTokens { get; }

    public long CacheReadTokens { get; }

    public long CacheWriteTokens { get; }

    public EvaluationTokenUsage Add(EvaluationTokenUsage other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (!other.IsAvailable)
        {
            return this;
        }

        if (!IsAvailable)
        {
            return other;
        }

        return new EvaluationTokenUsage(
            true,
            SaturatingAdd(InputTokens, other.InputTokens),
            SaturatingAdd(OutputTokens, other.OutputTokens),
            SaturatingAdd(ReasoningTokens, other.ReasoningTokens),
            SaturatingAdd(CacheReadTokens, other.CacheReadTokens),
            SaturatingAdd(CacheWriteTokens, other.CacheWriteTokens));
    }

    public override string ToString() =>
        $"{nameof(EvaluationTokenUsage)} {{ IsAvailable = {IsAvailable}, InputTokens = {InputTokens}, OutputTokens = {OutputTokens}, ReasoningTokens = {ReasoningTokens}, CacheReadTokens = {CacheReadTokens}, CacheWriteTokens = {CacheWriteTokens}, Content = <redacted> }}";

    private static long SaturatingAdd(long left, long right) =>
        left > long.MaxValue - right ? long.MaxValue : left + right;
}

public interface IEphemeralEvaluationAttempt
{
    string SessionId { get; }

    EvaluationTokenUsage TokenUsage => EvaluationTokenUsage.Unavailable;

    Task<QuantificationResult> ExecuteAsync(CancellationToken cancellationToken);

    Task AbortAsync(CancellationToken cancellationToken);

    Task DisposeSessionAsync(CancellationToken cancellationToken);

    Task DeleteSessionAsync(CancellationToken cancellationToken);
}

public enum EphemeralEvaluationStatus
{
    Succeeded,
    AiOutputInvalid,
    AiTimeout,
    NetworkFailed,
    AuthRequired,
    Cancelled,
    CleanupFailed,
    Fatal,
}

public sealed class EphemeralEvaluationResult
{
    private EphemeralEvaluationResult(
        EphemeralEvaluationStatus status,
        int attemptCount,
        QuantificationResult? acceptedResult,
        EvaluationTokenUsage tokenUsage)
    {
        Status = status;
        AttemptCount = attemptCount;
        AcceptedResult = acceptedResult;
        TokenUsage = tokenUsage;
    }

    public EphemeralEvaluationStatus Status { get; }

    public string StatusCode => Status switch
    {
        EphemeralEvaluationStatus.Succeeded => "SUCCESS",
        EphemeralEvaluationStatus.AiOutputInvalid => "AI_OUTPUT_INVALID",
        EphemeralEvaluationStatus.AiTimeout => "AI_TIMEOUT",
        EphemeralEvaluationStatus.NetworkFailed => "NETWORK_FAILED",
        EphemeralEvaluationStatus.AuthRequired => "AUTH_REQUIRED",
        EphemeralEvaluationStatus.Cancelled => "CANCELLED",
        EphemeralEvaluationStatus.CleanupFailed => "CLEANUP_FAILED",
        EphemeralEvaluationStatus.Fatal => "AI_RUNTIME_FAILED",
        _ => "AI_RUNTIME_FAILED",
    };

    public int AttemptCount { get; }

    public QuantificationResult? AcceptedResult { get; }

    public EvaluationTokenUsage TokenUsage { get; }

    public bool IsSuccess =>
        Status == EphemeralEvaluationStatus.Succeeded
        && AcceptedResult is not null;

    public override string ToString() =>
        $"EphemeralEvaluationResult {{ Status = {StatusCode}, AttemptCount = {AttemptCount}, Content = <redacted> }}";

    internal static EphemeralEvaluationResult Succeeded(
        int attemptCount,
        QuantificationResult acceptedResult,
        EvaluationTokenUsage? tokenUsage = null) =>
        new(
            EphemeralEvaluationStatus.Succeeded,
            attemptCount,
            acceptedResult,
            tokenUsage ?? EvaluationTokenUsage.Unavailable);

    internal static EphemeralEvaluationResult Failed(
        EphemeralEvaluationStatus status,
        int attemptCount,
        EvaluationTokenUsage? tokenUsage = null)
    {
        if (status == EphemeralEvaluationStatus.Succeeded)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        return new EphemeralEvaluationResult(
            status,
            attemptCount,
            null,
            tokenUsage ?? EvaluationTokenUsage.Unavailable);
    }
}

public sealed class RetryAndCleanupCoordinator
{
    public const int MaximumSchemaRetries = 1;
    public const int MaximumTransientRetries = 2;
    public const int MaximumSchemaAttempts = 2;
    public const int MaximumTransientAttempts = 3;

    private readonly SafeLogger _logger;

    public RetryAndCleanupCoordinator(SafeLogger? logger = null)
    {
        _logger = logger ?? SafeLogger.None;
    }

    public async Task<EphemeralEvaluationResult> ExecuteAsync(
        Func<int, IEphemeralEvaluationAttempt> attemptFactory,
        TimeSpan attemptTimeout,
        TimeSpan cleanupTimeout,
        int maxConcurrency,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attemptFactory);
        ValidateFiniteTimeout(attemptTimeout, nameof(attemptTimeout));
        ValidateFiniteTimeout(cleanupTimeout, nameof(cleanupTimeout));
        if (maxConcurrency is < 1 or > 3)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxConcurrency),
                "Evaluation concurrency must be between 1 and 3.");
        }

        int attemptCount = 0;
        int schemaRetryCount = 0;
        int transientRetryCount = 0;
        EvaluationTokenUsage tokenUsage = EvaluationTokenUsage.Unavailable;
        HashSet<string> observedSessionIds = new(StringComparer.Ordinal);

        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Log(
                    SafeLogSeverity.Information,
                    SafeLogEventCode.EvaluationCancelled,
                    attemptCount,
                    AttemptLimitFor(EvaluationAttemptFailureKind.Cancelled),
                    maxConcurrency,
                    SafeLogFailureCategory.Cancelled,
                    identity: null);
                return EphemeralEvaluationResult.Failed(
                    EphemeralEvaluationStatus.Cancelled,
                    attemptCount,
                    tokenUsage);
            }

            attemptCount++;
            IEphemeralEvaluationAttempt attempt;
            try
            {
                attempt = attemptFactory(attemptCount)
                    ?? throw new InvalidOperationException("The attempt factory returned no attempt.");
            }
            catch
            {
                Log(
                    SafeLogSeverity.Error,
                    SafeLogEventCode.EvaluationAttemptFailed,
                    attemptCount,
                    AttemptLimitFor(EvaluationAttemptFailureKind.Fatal),
                    maxConcurrency,
                    SafeLogFailureCategory.Fatal,
                    identity: null);
                return EphemeralEvaluationResult.Failed(
                    EphemeralEvaluationStatus.Fatal,
                    attemptCount,
                    tokenUsage);
            }

            SafeLogIdentity? identity = SafeLogIdentity.TryCreateSession(attempt.SessionId, out SafeLogIdentity? safeIdentity)
                ? safeIdentity
                : null;
            bool validUniqueSessionId = !string.IsNullOrWhiteSpace(attempt.SessionId)
                && attempt.SessionId.Length <= 128
                && observedSessionIds.Add(attempt.SessionId);

            Log(
                SafeLogSeverity.Information,
                SafeLogEventCode.EvaluationAttemptStarted,
                attemptCount,
                MaximumTransientAttempts,
                maxConcurrency,
                SafeLogFailureCategory.None,
                identity);

            QuantificationResult? acceptedResult = null;
            EvaluationAttemptFailureKind? failureKind = validUniqueSessionId
                ? null
                : EvaluationAttemptFailureKind.Fatal;
            bool abortRequired = !validUniqueSessionId;
            Task<QuantificationResult>? executionTask = null;

            if (failureKind is null)
            {
                using CancellationTokenSource timeoutSource = new(attemptTimeout);
                using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    timeoutSource.Token);
                CancellationToken finiteToken = linkedSource.Token;

                try
                {
                    executionTask = attempt.ExecuteAsync(finiteToken)
                        ?? throw new InvalidOperationException("The evaluation attempt returned no task.");
                    acceptedResult = await executionTask.WaitAsync(finiteToken).ConfigureAwait(false)
                        ?? throw new InvalidOperationException("The evaluation attempt returned no result.");
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    failureKind = EvaluationAttemptFailureKind.Cancelled;
                    abortRequired = true;
                }
                catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
                {
                    failureKind = EvaluationAttemptFailureKind.Timeout;
                    abortRequired = true;
                    ObserveLateFault(executionTask);
                }
                catch (TimeoutException)
                {
                    failureKind = EvaluationAttemptFailureKind.Timeout;
                    abortRequired = true;
                }
                catch (Exception exception) when (IsNetworkException(exception))
                {
                    failureKind = EvaluationAttemptFailureKind.Network;
                    abortRequired = true;
                }
                catch (EvaluationAttemptException exception)
                {
                    failureKind = NormalizeFailureKind(exception.FailureKind);
                    abortRequired = failureKind is EvaluationAttemptFailureKind.Network
                        or EvaluationAttemptFailureKind.Timeout
                        or EvaluationAttemptFailureKind.Fatal;
                }
                catch
                {
                    failureKind = EvaluationAttemptFailureKind.Fatal;
                    abortRequired = true;
                }
            }

            bool cleanupSucceeded = await CleanupAsync(
                attempt,
                abortRequired,
                cleanupTimeout).ConfigureAwait(false);
            try
            {
                tokenUsage = tokenUsage.Add(attempt.TokenUsage);
            }
            catch
            {
                // Numeric usage is advisory and cannot alter evaluation or cleanup semantics.
            }

            if (!cleanupSucceeded)
            {
                Log(
                    SafeLogSeverity.Error,
                    SafeLogEventCode.EvaluationCleanupFailed,
                    attemptCount,
                    AttemptLimitFor(EvaluationAttemptFailureKind.Cleanup),
                    maxConcurrency,
                    SafeLogFailureCategory.Cleanup,
                    identity);
                return EphemeralEvaluationResult.Failed(
                    EphemeralEvaluationStatus.CleanupFailed,
                    attemptCount,
                    tokenUsage);
            }

            if (acceptedResult is not null)
            {
                Log(
                    SafeLogSeverity.Information,
                    SafeLogEventCode.EvaluationAttemptSucceeded,
                    attemptCount,
                    MaximumTransientAttempts,
                    maxConcurrency,
                    SafeLogFailureCategory.None,
                    identity);
                return EphemeralEvaluationResult.Succeeded(
                    attemptCount,
                    acceptedResult,
                    tokenUsage);
            }

            EvaluationAttemptFailureKind terminalFailure = failureKind ?? EvaluationAttemptFailureKind.Fatal;
            if (cancellationToken.IsCancellationRequested
                || terminalFailure == EvaluationAttemptFailureKind.Cancelled)
            {
                Log(
                    SafeLogSeverity.Information,
                    SafeLogEventCode.EvaluationCancelled,
                    attemptCount,
                    AttemptLimitFor(EvaluationAttemptFailureKind.Cancelled),
                    maxConcurrency,
                    SafeLogFailureCategory.Cancelled,
                    identity);
                return EphemeralEvaluationResult.Failed(
                    EphemeralEvaluationStatus.Cancelled,
                    attemptCount,
                    tokenUsage);
            }

            Log(
                SeverityFor(terminalFailure),
                SafeLogEventCode.EvaluationAttemptFailed,
                attemptCount,
                AttemptLimitFor(terminalFailure),
                maxConcurrency,
                ToSafeCategory(terminalFailure),
                identity);

            if (ShouldRetry(
                    terminalFailure,
                    attemptCount,
                    ref schemaRetryCount,
                    ref transientRetryCount))
            {
                Log(
                    SafeLogSeverity.Information,
                    SafeLogEventCode.EvaluationRetryScheduled,
                    attemptCount,
                    AttemptLimitFor(terminalFailure),
                    maxConcurrency,
                    ToSafeCategory(terminalFailure),
                    identity);
                continue;
            }

            return EphemeralEvaluationResult.Failed(
                MapTerminalStatus(terminalFailure),
                attemptCount,
                tokenUsage);
        }
    }

    private static async Task<bool> CleanupAsync(
        IEphemeralEvaluationAttempt attempt,
        bool abortRequired,
        TimeSpan cleanupTimeout)
    {
        bool succeeded = true;
        if (abortRequired)
        {
            succeeded &= await InvokeCleanupOperationAsync(
                attempt.AbortAsync,
                cleanupTimeout).ConfigureAwait(false);
        }

        succeeded &= await InvokeCleanupOperationAsync(
            attempt.DisposeSessionAsync,
            cleanupTimeout).ConfigureAwait(false);
        succeeded &= await InvokeCleanupOperationAsync(
            attempt.DeleteSessionAsync,
            cleanupTimeout).ConfigureAwait(false);
        return succeeded;
    }

    private static async Task<bool> InvokeCleanupOperationAsync(
        Func<CancellationToken, Task> operation,
        TimeSpan timeout)
    {
        using CancellationTokenSource cleanupSource = new(timeout);
        Task? operationTask = null;
        try
        {
            operationTask = operation(cleanupSource.Token)
                ?? throw new InvalidOperationException("The cleanup operation returned no task.");
            await operationTask.WaitAsync(cleanupSource.Token).ConfigureAwait(false);
            return true;
        }
        catch
        {
            ObserveLateFault(operationTask);
            return false;
        }
    }

    private static bool ShouldRetry(
        EvaluationAttemptFailureKind failureKind,
        int attemptCount,
        ref int schemaRetryCount,
        ref int transientRetryCount)
    {
        if (attemptCount >= MaximumTransientAttempts)
        {
            return false;
        }

        switch (failureKind)
        {
            case EvaluationAttemptFailureKind.SchemaInvalid
                when schemaRetryCount < MaximumSchemaRetries:
                schemaRetryCount++;
                return true;

            case EvaluationAttemptFailureKind.Network or EvaluationAttemptFailureKind.Timeout
                when transientRetryCount < MaximumTransientRetries:
                transientRetryCount++;
                return true;

            default:
                return false;
        }
    }

    private static int AttemptLimitFor(EvaluationAttemptFailureKind failureKind) =>
        failureKind switch
        {
            EvaluationAttemptFailureKind.SchemaInvalid => MaximumSchemaAttempts,
            EvaluationAttemptFailureKind.Network or EvaluationAttemptFailureKind.Timeout =>
                MaximumTransientAttempts,
            _ => 1,
        };

    private static EvaluationAttemptFailureKind NormalizeFailureKind(
        EvaluationAttemptFailureKind failureKind) =>
        failureKind switch
        {
            EvaluationAttemptFailureKind.SchemaInvalid => EvaluationAttemptFailureKind.SchemaInvalid,
            EvaluationAttemptFailureKind.Network => EvaluationAttemptFailureKind.Network,
            EvaluationAttemptFailureKind.Timeout => EvaluationAttemptFailureKind.Timeout,
            EvaluationAttemptFailureKind.Authentication => EvaluationAttemptFailureKind.Authentication,
            EvaluationAttemptFailureKind.Cancelled => EvaluationAttemptFailureKind.Cancelled,
            EvaluationAttemptFailureKind.Fatal => EvaluationAttemptFailureKind.Fatal,
            EvaluationAttemptFailureKind.Cleanup => EvaluationAttemptFailureKind.Fatal,
            _ => EvaluationAttemptFailureKind.Fatal,
        };

    private static EphemeralEvaluationStatus MapTerminalStatus(
        EvaluationAttemptFailureKind failureKind) =>
        failureKind switch
        {
            EvaluationAttemptFailureKind.SchemaInvalid => EphemeralEvaluationStatus.AiOutputInvalid,
            EvaluationAttemptFailureKind.Network => EphemeralEvaluationStatus.NetworkFailed,
            EvaluationAttemptFailureKind.Timeout => EphemeralEvaluationStatus.AiTimeout,
            EvaluationAttemptFailureKind.Authentication => EphemeralEvaluationStatus.AuthRequired,
            EvaluationAttemptFailureKind.Cancelled => EphemeralEvaluationStatus.Cancelled,
            _ => EphemeralEvaluationStatus.Fatal,
        };

    private static SafeLogSeverity SeverityFor(EvaluationAttemptFailureKind failureKind) =>
        failureKind is EvaluationAttemptFailureKind.Fatal or EvaluationAttemptFailureKind.Cleanup
            ? SafeLogSeverity.Error
            : SafeLogSeverity.Warning;

    private static SafeLogFailureCategory ToSafeCategory(
        EvaluationAttemptFailureKind failureKind) =>
        failureKind switch
        {
            EvaluationAttemptFailureKind.SchemaInvalid => SafeLogFailureCategory.SchemaInvalid,
            EvaluationAttemptFailureKind.Network => SafeLogFailureCategory.Network,
            EvaluationAttemptFailureKind.Timeout => SafeLogFailureCategory.Timeout,
            EvaluationAttemptFailureKind.Authentication => SafeLogFailureCategory.Authentication,
            EvaluationAttemptFailureKind.Cancelled => SafeLogFailureCategory.Cancelled,
            EvaluationAttemptFailureKind.Cleanup => SafeLogFailureCategory.Cleanup,
            _ => SafeLogFailureCategory.Fatal,
        };

    private static bool IsNetworkException(Exception exception) =>
        exception is IOException
            or HttpRequestException
            or SocketException
            or WebSocketException;

    private void Log(
        SafeLogSeverity severity,
        SafeLogEventCode eventCode,
        int attemptNumber,
        int attemptLimit,
        int maxConcurrency,
        SafeLogFailureCategory failureCategory,
        SafeLogIdentity? identity) =>
        _logger.Log(
            severity,
            eventCode,
            new SafeLogDimensions(
                attemptNumber,
                attemptLimit,
                maxConcurrency,
                failureCategory),
            identity);

    private static void ObserveLateFault(Task? task)
    {
        if (task is null || task.IsCompleted)
        {
            _ = task?.Exception;
            return;
        }

        _ = task.ContinueWith(
            static completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    internal static void ValidateFiniteTimeout(TimeSpan timeout, string parameterName)
    {
        const double MaximumCancellationDelayMilliseconds = uint.MaxValue - 1d;
        if (timeout <= TimeSpan.Zero
            || timeout == Timeout.InfiniteTimeSpan
            || timeout.TotalMilliseconds > MaximumCancellationDelayMilliseconds)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "The timeout must be positive and finite.");
        }
    }
}