using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.ConfigFlow;

namespace ComfyUiJobMonitor;

public sealed class ComfyUiConfigFlow : IConfigFlow
{
	public const string AddressField = "address";
	private const string StepId = "connection";

	public Task<ConfigFlowResult> StartAsync(IConfigFlowContext context, CancellationToken cancellationToken)
		=> Task.FromResult(ConfigFlowResult.Step(BuildStep()));

	public Task<ConfigFlowResult> SubmitAsync(string stepId, IReadOnlyDictionary<string, object?> input,
		IConfigFlowContext context, CancellationToken cancellationToken)
	{
		if (stepId != StepId)
		{
			return Task.FromResult(ConfigFlowResult.Error(BuildStep(), Strings.ConfigFlow.UnknownStep()));
		}

		var address = input.GetValueOrDefault(AddressField)?.ToString();
		if (!TryGetAddress(address, out _))
		{
			var error = Strings.ConfigFlow.InvalidAddress();
			return Task.FromResult(ConfigFlowResult.Error(BuildStep(), error,
				new Dictionary<string, LocalizedText> { [AddressField] = error }));
		}

		return Task.FromResult(ConfigFlowResult.Complete("ComfyUI"));
	}

	public static bool TryGetAddress(string? value, out Uri address)
	{
		address = null!;
		if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var parsed) ||
			parsed.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(parsed.UserInfo) ||
			!string.IsNullOrEmpty(parsed.Query) || !string.IsNullOrEmpty(parsed.Fragment))
		{
			return false;
		}
		address = new Uri(parsed.AbsoluteUri.TrimEnd('/') + "/");
		return true;
	}

	private static ConfigFlowStep BuildStep() => new()
	{
		StepId = StepId,
		Title = Strings.ConfigFlow.Title(),
		Description = Strings.ConfigFlow.Description(),
		Fields = [ActionParameter.Text(AddressField, label: Strings.ConfigFlow.Address.Label(),
			defaultValue: "http://127.0.0.1:8188", required: true)]
	};
}
