using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using GitHub.Copilot;
using StudyReportEvaluator.App.Logging;
using StudyReportEvaluator.Core.Domain;
using StudyReportEvaluator.Core.Prompting;

namespace StudyReportEvaluator.App.Copilot;

public sealed class EphemeralEvaluationRunnerOptions
{
    public static TimeSpan DefaultAttemptTimeout { get; } = TimeSpan.FromSeconds(120);

    public static TimeSpan DefaultCleanupTimeout { get; } = TimeSpan.FromSeconds(15);

    public const int DefaultMaxConcurrency = 1;
    public const int MaximumMaxConcurrency = 3;

    public EphemeralEvaluationRunnerOptions(
        TimeSpan? attemptTimeout = null,
        TimeSpan? cleanupTimeout = null,
        int maxConcurrency = DefaultMaxConcurrency)
    {
        AttemptTimeout = attemptTimeout ?? DefaultAttemptTimeout;
        CleanupTimeout = cleanupTimeout ?? DefaultCleanupTimeout;
        RetryAndCleanupCoordinator.ValidateFiniteTimeout(AttemptTimeout, nameof(attemptTimeout));
        RetryAndCleanupCoordinator.ValidateFiniteTimeout(CleanupTimeout, nameof(cleanupTimeout));

        if (maxConcurrency is < DefaultMaxConcurrency or > MaximumMaxConcurrency)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxConcurrency),
                "Evaluation concurrency must be between 1 and 3.");
        }

        MaxConcurrency = maxConcurrency;
    }

    public TimeSpan AttemptTimeout { get; }

    public TimeSpan CleanupTimeout { get; }

    public int MaxConcurrency { get; }
}

public interface IEphemeralCopilotTransportFactory
{
    IEphemeralCopilotTransport Create();
}

public interface IEphemeralCopilotTransport : IAsyncDisposable
{
    Task StartAsync(CancellationToken cancellationToken);

    Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken);

    Task<IEphemeralCopilotSession> CreateSessionAsync(
        SessionConfig config,
        CancellationToken cancellationToken);

    Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}

public interface IEphemeralCopilotSession : IAsyncDisposable
{
    string SessionId { get; }

    Task SendAndWaitAsync(
        MessageOptions options,
        CancellationToken cancellationToken);

    Task AbortAsync(CancellationToken cancellationToken);
}

public sealed class EphemeralEvaluationRunner
{
    private const string SessionIdPrefix = "a03-";

    private readonly IEphemeralCopilotTransportFactory _transportFactory;
    private readonly EvaluationSchemaFactory _schemaFactory;
    private readonly RetryAndCleanupCoordinator _coordinator;
    private readonly EphemeralEvaluationRunnerOptions _options;

    public EphemeralEvaluationRunner()
        : this(
            new SdkEphemeralCopilotTransportFactory(new CopilotClientFactory()),
            options: null,
            logger: null)
    {
    }

    public EphemeralEvaluationRunner(
        IEphemeralCopilotTransportFactory transportFactory,
        EphemeralEvaluationRunnerOptions? options = null,
        SafeLogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(transportFactory);
        _transportFactory = transportFactory;
        _schemaFactory = new EvaluationSchemaFactory();
        _coordinator = new RetryAndCleanupCoordinator(logger);
        _options = options ?? new EphemeralEvaluationRunnerOptions();
    }

    public EphemeralEvaluationRunnerOptions Options => _options;

    public Task<EphemeralEvaluationResult> EvaluateAsync(
        SafeEvaluationPayload payload,
        string modelId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ValidateModelId(modelId);

        return _coordinator.ExecuteAsync(
            _ => CreateAttempt(payload, modelId),
            _options.AttemptTimeout,
            _options.CleanupTimeout,
            _options.MaxConcurrency,
            cancellationToken);
    }

    private IEphemeralEvaluationAttempt CreateAttempt(
        SafeEvaluationPayload payload,
        string modelId)
    {
        IEphemeralCopilotTransport transport = _transportFactory.Create()
            ?? throw new InvalidOperationException("The transport factory returned no transport.");
        return new CopilotEvaluationAttempt(
            transport,
            _schemaFactory,
            payload,
            modelId,
            CreateSessionId());
    }

    private static string CreateSessionId() =>
        $"{SessionIdPrefix}{Guid.NewGuid():N}";

    private static void ValidateModelId(string modelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        if (modelId.Length > 256
            || !string.Equals(modelId, modelId.Trim(), StringComparison.Ordinal)
            || modelId.Any(char.IsControl))
        {
            throw new ArgumentException("The model ID is invalid.", nameof(modelId));
        }
    }
}

internal sealed class CopilotEvaluationAttempt : IEphemeralEvaluationAttempt
{
    private readonly IEphemeralCopilotTransport _transport;
    private readonly SubmitQuantificationTool _collector;
    private readonly SessionConfig _sessionConfig;
    private readonly MessageOptions _messageOptions;
    private IEphemeralCopilotSession? _session;
    private string? _createdSessionId;

    internal CopilotEvaluationAttempt(
        IEphemeralCopilotTransport transport,
        EvaluationSchemaFactory schemaFactory,
        SafeEvaluationPayload payload,
        string modelId,
        string sessionId)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(schemaFactory);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        _transport = transport;
        _sessionConfig = schemaFactory.CreateSessionConfig(payload, out _collector);
        _sessionConfig.SessionId = sessionId;
        _sessionConfig.Model = modelId;
        _messageOptions = new MessageOptions
        {
            Prompt = payload.RenderedPrompt,
            Attachments = [],
            DisplayPrompt = null,
            RequestHeaders = null,
        };
        SessionId = sessionId;
    }

    public string SessionId { get; }

    public async Task<QuantificationResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _transport.StartAsync(cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        if (!await _transport.IsAuthenticatedAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new EvaluationAuthenticationException();
        }

        cancellationToken.ThrowIfCancellationRequested();
        IEphemeralCopilotSession session = await _transport
            .CreateSessionAsync(_sessionConfig, cancellationToken)
            .ConfigureAwait(false);
        _session = session
            ?? throw new EvaluationFatalException();
        _createdSessionId = session.SessionId;
        if (!string.Equals(SessionId, session.SessionId, StringComparison.Ordinal))
        {
            throw new EvaluationFatalException();
        }

        cancellationToken.ThrowIfCancellationRequested();
        await session.SendAndWaitAsync(_messageOptions, cancellationToken).ConfigureAwait(false);

        if (_collector.TryGetAcceptedResult(out QuantificationResult? acceptedResult))
        {
            return acceptedResult;
        }

        throw new EvaluationSchemaException();
    }

    public Task AbortAsync(CancellationToken cancellationToken)
    {
        IEphemeralCopilotSession? session = Volatile.Read(ref _session);
        return session is null
            ? Task.CompletedTask
            : session.AbortAsync(cancellationToken);
    }

    public async Task DisposeSessionAsync(CancellationToken cancellationToken)
    {
        IEphemeralCopilotSession? session = Volatile.Read(ref _session);
        if (session is not null)
        {
            await session.DisposeAsync().AsTask().WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task DeleteSessionAsync(CancellationToken cancellationToken)
    {
        bool succeeded = true;
        string? createdSessionId = Volatile.Read(ref _createdSessionId);
        if (createdSessionId is not null)
        {
            try
            {
                await _transport
                    .DeleteSessionAsync(createdSessionId, cancellationToken)
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                succeeded = false;
            }
        }

        try
        {
            await _transport
                .StopAsync(cancellationToken)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            succeeded = false;
        }

        try
        {
            await _transport
                .DisposeAsync()
                .AsTask()
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            succeeded = false;
        }

        if (!succeeded)
        {
            throw new EvaluationCleanupException();
        }
    }
}

internal sealed class SdkEphemeralCopilotTransportFactory : IEphemeralCopilotTransportFactory
{
    private readonly ICopilotClientFactory _clientFactory;

    internal SdkEphemeralCopilotTransportFactory(ICopilotClientFactory clientFactory)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        _clientFactory = clientFactory;
    }

    public IEphemeralCopilotTransport Create() =>
        new SdkEphemeralCopilotTransport(_clientFactory);
}

internal sealed class SdkEphemeralCopilotTransport : IEphemeralCopilotTransport
{
    private readonly ICopilotClientFactory _clientFactory;
    private CopilotClient? _client;

    internal SdkEphemeralCopilotTransport(ICopilotClientFactory clientFactory)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        _clientFactory = clientFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CopilotClientCreationResult creation = await _clientFactory
            .CreateAsync(cancellationToken)
            .ConfigureAwait(false);
        if (creation.Status == CopilotClientCreationStatus.Cancelled
            && cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        if (creation.Status != CopilotClientCreationStatus.Created
            || creation.Client is null)
        {
            throw new EvaluationFatalException();
        }

        _client = creation.Client;
        await TranslateAsync(
            () => creation.Client.StartAsync(cancellationToken)).ConfigureAwait(false);
    }

    public Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken) =>
        TranslateAsync(async () =>
        {
            CopilotClient client = GetClient();
            GetAuthStatusResponse response = await client
                .GetAuthStatusAsync(cancellationToken)
                .ConfigureAwait(false);
            return response.IsAuthenticated;
        });

    public Task<IEphemeralCopilotSession> CreateSessionAsync(
        SessionConfig config,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);
        return TranslateAsync<IEphemeralCopilotSession>(async () =>
        {
            CopilotSession session = await GetClient()
                .CreateSessionAsync(config, cancellationToken)
                .ConfigureAwait(false);
            return new SdkEphemeralCopilotSession(session);
        });
    }

    public Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        return GetClient().DeleteSessionAsync(sessionId, cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        CopilotClient? client = Volatile.Read(ref _client);
        if (client is not null)
        {
            await client.StopAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        CopilotClient? client = Interlocked.Exchange(ref _client, null);
        if (client is not null)
        {
            await client.DisposeAsync().ConfigureAwait(false);
        }
    }

    private CopilotClient GetClient() =>
        Volatile.Read(ref _client)
        ?? throw new EvaluationFatalException();

    private static async Task TranslateAsync(Func<Task> operation)
    {
        try
        {
            await operation().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TimeoutException)
        {
            throw new EvaluationAttemptTimeoutException();
        }
        catch (Exception exception) when (IsNetworkException(exception))
        {
            throw new EvaluationNetworkException();
        }
        catch (EvaluationAttemptException)
        {
            throw;
        }
        catch
        {
            throw new EvaluationFatalException();
        }
    }

    private static async Task<T> TranslateAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TimeoutException)
        {
            throw new EvaluationAttemptTimeoutException();
        }
        catch (Exception exception) when (IsNetworkException(exception))
        {
            throw new EvaluationNetworkException();
        }
        catch (EvaluationAttemptException)
        {
            throw;
        }
        catch
        {
            throw new EvaluationFatalException();
        }
    }

    private static bool IsNetworkException(Exception exception) =>
        exception is IOException
            or HttpRequestException
            or SocketException
            or WebSocketException;
}

internal sealed class SdkEphemeralCopilotSession : IEphemeralCopilotSession
{
    private readonly CopilotSession _session;

    internal SdkEphemeralCopilotSession(CopilotSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
    }

    public string SessionId => _session.SessionId;

    public async Task SendAndWaitAsync(
        MessageOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        try
        {
            _ = await _session
                .SendAndWaitAsync(
                    options,
                    timeout: null,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TimeoutException)
        {
            throw new EvaluationAttemptTimeoutException();
        }
        catch (Exception exception) when (
            exception is IOException
                or HttpRequestException
                or SocketException
                or WebSocketException)
        {
            throw new EvaluationNetworkException();
        }
        catch (EvaluationAttemptException)
        {
            throw;
        }
        catch
        {
            throw new EvaluationFatalException();
        }
    }

    public Task AbortAsync(CancellationToken cancellationToken) =>
        _session.AbortAsync(cancellationToken);

    public ValueTask DisposeAsync() => _session.DisposeAsync();
}