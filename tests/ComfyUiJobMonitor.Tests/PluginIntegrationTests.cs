using NUnit.Framework;
using Serilog;
using System.Net;
using System.Text;

namespace ComfyUiJobMonitor.Tests;

/// <summary>
/// Behaviour tests through <see cref="PluginTestHarness"/>: the plugin's own capability handlers run,
/// but nothing crosses a socket. This is where you test what your integration does.
/// </summary>
[TestFixture]
public sealed class PluginIntegrationTests
{
	[Test]
	public static void The_queue_variables_are_declared()
	{
		using var httpClient = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:8188/") };
		var integration = new PluginIntegration(
			new LoggerConfiguration().CreateLogger(),
			new ComfyUiQueueClient(httpClient));

		Assert.That(integration.Variables.Select(definition => definition.Id), Is.EquivalentTo([
			"jobs-running",
			"jobs-pending",
			"jobs-total",
			"job-progress-percent",
		]));
	}

	[TestCase("http://127.0.0.1:8188", true)]
	[TestCase("https://comfy.local:9000/instance", true)]
	[TestCase("http://user:pass@comfy.local", false)]
	[TestCase("file:///tmp/comfy", false)]
	[TestCase("http://comfy.local/?token=secret", false)]
	public void Only_server_urls_without_embedded_credentials_are_accepted(string url, bool valid)
	{
		Assert.That(ComfyUiConfigFlow.TryGetAddress(url, out _), Is.EqualTo(valid));
	}

	[Test]
	public async Task Queue_counts_work_without_the_optional_progress_endpoint()
	{
		var requests = new List<Uri>();
		using var client = new HttpClient(new StubHandler(request =>
		{
			requests.Add(request.RequestUri!);
			return request.RequestUri!.AbsolutePath.EndsWith("/queue", StringComparison.Ordinal)
				? new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StringContent("{\"queue_running\":[1],\"queue_pending\":[2,3]}", Encoding.UTF8, "application/json")
				}
				: new HttpResponseMessage(HttpStatusCode.NotFound);
		}));
		var reading = await new ComfyUiQueueClient(client).GetReadingAsync(new Uri("http://comfy.local:8188/instance/"), CancellationToken.None);
		Assert.That(reading?.Running, Is.EqualTo(1));
		Assert.That(reading?.Pending, Is.EqualTo(2));
		Assert.That(reading?.ProgressPercent, Is.Null);
		Assert.That(requests.Select(uri => uri.AbsolutePath), Is.EqualTo(new List<string> { "/instance/queue", "/instance/job-progress" }));
	}

	private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			=> Task.FromResult(respond(request));
	}
}

/// <summary>
/// The localization set is generated from <c>Localization/*.resx</c>, so these guard the wiring rather
/// than any wording: a missing catalog registration leaves every label showing its raw key.
/// </summary>
[TestFixture]
public sealed class LocalizationTests
{
	[Test]
	public void The_catalog_is_scoped_to_the_plugin_id()
	{
		Assert.That(Strings.LocalizationCatalog.Scope, Is.EqualTo("plugin:com.yuziiv.comfyui-job-monitor"));
	}

	[Test]
	public void English_is_the_default_culture()
	{
		Assert.That(Strings.LocalizationCatalog.DefaultCulture, Is.EqualTo("en"));
		Assert.That(Strings.LocalizationCatalog.Cultures, Does.Contain("en"));
	}

	[Test]
	public void The_plugin_strings_come_from_the_catalog()
	{
		Assert.That(Strings.LocalizationCatalog.KeysOf("en"), Does.Contain("Plugin.Name"));
	}

	[Test]
	public void Every_key_the_default_culture_declares_resolves_to_text()
	{
		foreach (var key in Strings.LocalizationCatalog.KeysOf("en"))
		{
			Assert.That(Strings.LocalizationCatalog.TryGetTemplate("en", key, out var text), Is.True);
			Assert.That(text, Is.Not.Empty);
		}
	}
}
