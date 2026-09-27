using System.Text.Json;

namespace ComfyUiJobMonitor;

public sealed class ComfyUiQueueClient
{
	private readonly HttpClient _httpClient;

	public ComfyUiQueueClient(HttpClient httpClient)
	{
		_httpClient = httpClient;
	}

	public async Task<JobMonitorReading?> GetReadingAsync(Uri baseAddress, CancellationToken cancellationToken)
	{
		try
		{
			using var queueResponse = await _httpClient.GetAsync(new Uri(baseAddress, "queue"), cancellationToken);
			queueResponse.EnsureSuccessStatusCode();
			await using var queueStream = await queueResponse.Content.ReadAsStreamAsync(cancellationToken);
			using var queueDocument = await JsonDocument.ParseAsync(queueStream, cancellationToken: cancellationToken);

			var queue = queueDocument.RootElement;
			double? progress = null;
			try
			{
				using var progressResponse = await _httpClient.GetAsync(new Uri(baseAddress, "job-progress"), cancellationToken);
				progressResponse.EnsureSuccessStatusCode();
				await using var progressStream = await progressResponse.Content.ReadAsStreamAsync(cancellationToken);
				using var progressDocument = await JsonDocument.ParseAsync(progressStream, cancellationToken: cancellationToken);
				progress = progressDocument.RootElement.GetProperty("percent").GetDouble();
			}
			catch (HttpRequestException) { }
			catch (JsonException) { }
			catch (KeyNotFoundException) { }
			catch (InvalidOperationException) { }
			catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
			return new JobMonitorReading(
				queue.GetProperty("queue_running").GetArrayLength(),
				queue.GetProperty("queue_pending").GetArrayLength(),
				progress,
				DateTimeOffset.UtcNow);
		}
		catch (HttpRequestException)
		{
			return null;
		}
		catch (JsonException)
		{
			return null;
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			return null;
		}
	}
}

public readonly record struct JobMonitorReading(int Running, int Pending, double? ProgressPercent, DateTimeOffset ReadAt);
