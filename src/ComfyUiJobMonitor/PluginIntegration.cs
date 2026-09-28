using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.ConfigFlow;
using MacroDeck.Sdk.Variables;
using Serilog;

namespace ComfyUiJobMonitor;

/// <summary>
/// The plugin's one integration. It declares a single example action: add more to <see cref="Actions"/>,
/// and opt into a capability by implementing its interface here (<c>IVariableProvider</c>,
/// <c>IEventProvider</c>, <c>IConfigFlowProvider</c>, and so on).
/// </summary>
public sealed class PluginIntegration : IPluginIntegration, IVariableProvider, IConfigFlowProvider, IDisposable
{
	private static readonly TimeSpan[] _retryDelays =
	[
		TimeSpan.FromSeconds(5),
		TimeSpan.FromSeconds(15),
		TimeSpan.FromSeconds(30),
		TimeSpan.FromMinutes(1),
		TimeSpan.FromMinutes(5),
	];

	private readonly ILogger _logger;
	private readonly ComfyUiQueueClient _queueClient;
	private Task<JobMonitorReading?>? _activeRefreshTask;
	private readonly object _sync = new();
	private JobMonitorReading? _cachedReading;
	private DateTimeOffset _retryAfter;
	private int _consecutiveFailures;
	private Uri _baseAddress = new("http://127.0.0.1:8188/");

	public IConfigFlow CreateConfigFlow() => new ComfyUiConfigFlow();
	public bool AllowsMultipleConfigurations => false;

	public IReadOnlyList<VariableDefinition> Variables { get; } =
	[
		VariableDefinition.Eager("jobs-running", VariableType.Numeric, decimalPlaces: 0, refreshInterval: TimeSpan.FromSeconds(1)) with { Id = "jobs-running", Unit = "jobs" },
		VariableDefinition.Eager("jobs-pending", VariableType.Numeric, decimalPlaces: 0, refreshInterval: TimeSpan.FromSeconds(1)) with { Id = "jobs-pending", Unit = "jobs" },
		VariableDefinition.Eager("jobs-total", VariableType.Numeric, decimalPlaces: 0, refreshInterval: TimeSpan.FromSeconds(1)) with { Id = "jobs-total", Unit = "jobs" },
		VariableDefinition.Eager("job-progress-percent", VariableType.Numeric, decimalPlaces: 1, refreshInterval: TimeSpan.FromSeconds(1)) with { Id = "job-progress-percent", Unit = "%" },
	];

	public PluginIntegration(ILogger logger, ComfyUiQueueClient queueClient)
	{
		_logger = logger.ForContext<PluginIntegration>();
		_queueClient = queueClient;
	}

	public IReadOnlyList<IActionDefinition> Actions { get; } = [];

	/// <summary>
	/// Runs once the session is established, and again after a non-resume reconnect or a configuration
	/// change, so it has to be safe to run repeatedly against an already-initialized process.
	/// </summary>
	public async Task InitializeAsync(IIntegrationContext context)
	{
		var baseAddress = new Uri("http://127.0.0.1:8188/");
		var entries = await context.Config.GetEntriesAsync();
		if (entries.Count > 0)
		{
			var address = await context.Config.GetStringAsync(entries[0].Id, ComfyUiConfigFlow.AddressField);
			if (ComfyUiConfigFlow.TryGetAddress(address, out var uri))
			{
				baseAddress = uri;
			}
		}
		lock (_sync)
		{
			_baseAddress = baseAddress;
			_cachedReading = null;
			_consecutiveFailures = 0;
			_retryAfter = default;
		}
		_logger.Information("Initialized.");
	}

	public Task ShutdownAsync() => Task.CompletedTask;

	public void Dispose() { }

	public async ValueTask<VariableReading> ReadAsync(string localId, CancellationToken cancellationToken = default)
	{
		var reading = await GetReadingAsync(cancellationToken);
		if (reading is null)
		{
			return VariableReading.Unavailable;
		}
		var current = reading.Value;

		return localId switch
		{
			"jobs-running" => VariableReading.Of(current.Running),
			"jobs-pending" => VariableReading.Of(current.Pending),
			"jobs-total" => VariableReading.Of(current.Running + current.Pending),
			"job-progress-percent" => current.ProgressPercent is { } percent
				? VariableReading.Of(percent) : VariableReading.Unavailable,
			_ => VariableReading.Unavailable,
		};
	}

	private async Task<JobMonitorReading?> GetReadingAsync(CancellationToken cancellationToken)
	{
		Task<JobMonitorReading?> refreshTask;
		lock (_sync)
		{
			var now = DateTimeOffset.UtcNow;
			if (_cachedReading is { } fresh && now - fresh.ReadAt < TimeSpan.FromSeconds(1))
			{
				return fresh;
			}

			if (now < _retryAfter)
			{
				return _cachedReading;
			}

			if (_activeRefreshTask == null || _activeRefreshTask.IsCompleted)
			{
				_activeRefreshTask = RefreshInternalAsync(_baseAddress);
			}
			refreshTask = _activeRefreshTask;
		}

		try
		{
			return await refreshTask.WaitAsync(cancellationToken);
		}
		catch (OperationCanceledException)
		{
			lock (_sync)
			{
				return _cachedReading;
			}
		}
	}

	private async Task<JobMonitorReading?> RefreshInternalAsync(Uri baseAddress)
	{
		JobMonitorReading? reading;
		try
		{
			reading = await _queueClient.GetReadingAsync(baseAddress, CancellationToken.None);
		}
		catch
		{
			reading = null;
		}

		lock (_sync)
		{
			if (reading is null)
			{
				var delay = _retryDelays[Math.Min(_consecutiveFailures, _retryDelays.Length - 1)];
				_consecutiveFailures++;
				_retryAfter = DateTimeOffset.UtcNow + delay;
				return _cachedReading;
			}

			_cachedReading = reading;
			_consecutiveFailures = 0;
			_retryAfter = default;
			return reading;
		}
	}
}
