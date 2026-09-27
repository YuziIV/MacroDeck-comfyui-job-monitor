using MacroDeck.Plugin.Hosting;
using MacroDeck.Plugin.Serilog;
using ComfyUiJobMonitor;
using Microsoft.Extensions.DependencyInjection;

// Identity, description and icon are not set here: they come from manifest.json at the content root.
// Strings is generated from Localization/*.resx, so UseLocalization is what makes every LocalizedString
// below resolve in the user's language rather than falling back to its key.
var builder = MacroDeckPlugin.CreatePlugin(args)
	.UseMacroDeckLogging()
	.UseLocalization(Strings.LocalizationCatalog)
	.RegisterIntegration<PluginIntegration>();

builder.Services.AddHttpClient<ComfyUiQueueClient>(client =>
{
	client.Timeout = TimeSpan.FromSeconds(5);
});

var plugin = builder.Build();

await plugin.RunAsync();
