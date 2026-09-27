# ComfyUI Job Monitor

Macro Deck 3 plugin providing live ComfyUI queue and progress variables for buttons and widgets.

## Use

Install the plugin from the Macro Deck Store in the **Macro Deck desktop app**; Macro Deck starts and authorizes it. To set it up:

1. Open **Integrations → ComfyUI Job Monitor** and choose **Connect to ComfyUI**.
2. Enter the full HTTP or HTTPS address of your ComfyUI server **as seen from the computer running Macro Deck**. For ComfyUI on the same computer, use `http://127.0.0.1:8188`. Save the setup. You can change the address in the integration settings later.
3. Add a widget such as **History Graph** or an **Action Button** in Macro Deck, and select one of these variables to show the queue status:

| Variable | Meaning |
| --- | --- |
| `jobs-running` | Running jobs |
| `jobs-pending` | Waiting jobs |
| `jobs-total` | Running plus waiting jobs |
| `job-progress-percent` | Progress of the current job (percent) |

The plugin requests `/queue` about once a second from the configured server. If `/job-progress` is also available, it reports job progress; otherwise only that variable is unavailable. It does not upload queue contents, read generated images or contact services other than the configured ComfyUI server. If you use a remote address, choose a network and URL you trust; credentials embedded in the URL are rejected.

The plugin's icon and documentation were created with AI assistance. The running plugin does not provide AI interaction or generate content; it only reads job status from your selected ComfyUI server.

To test before publishing, start Macro Deck, create a one-time token under **Developer Tools → Plugin tokens**, save it as `MacroDeck:Plugin:EnrollmentToken` in the **source project's .NET User Secrets**, and run the **Macro Deck - Real Host** debug profile in `src/ComfyUiJobMonitor/Properties/launchSettings.json`. Remove the token from User Secrets after pairing; subsequent debug launches reuse the ignored `.macrodeck-dev-state/`. Configure a local ComfyUI server, then try a different reachable address and verify the job counts follow the new server. Check that counts still work if `/job-progress` is absent, and that an unreachable address shows unavailable readings. Store installations need no developer enrollment token.

## Build and publish

Requires the .NET 10 SDK and the [Macro Deck plugin CLI](https://docs.macro-deck.app/). The packaged plugin uses the .NET 10 runtime supplied by Macro Deck 3. Tested hosts: Windows x64 and Linux x64. A macOS arm64 build target is included but has not been tested.

```bash
dotnet build
dotnet test
macrodeck-plugin test --project src/ComfyUiJobMonitor --report markdown --output conformance.md
macrodeck-plugin build --source src/ComfyUiJobMonitor --output ./artifacts
macrodeck-plugin inspect --artifact ./artifacts/com.yussefabdelwahab.comfyui-job-monitor-1.0.2.macroDeckPlugin
```

Publish the source as a public repository at [YuziIV/MacroDeck-comfyui-job-monitor](https://github.com/YuziIV/MacroDeck-comfyui-job-monitor). In the [Creator Portal](https://docs.macro-deck.app/creator-portal/publish-plugin/), use a **Plugin / Integration** Project with Package ID `com.yussefabdelwahab.comfyui-job-monitor`, then connect this repository under **Builds**. Publish a new GitHub release tagged `v1.0.2`: previous releases `v1.0.0` and `v1.0.1` refer to older commits, and re-running those workflows cannot pick up the updated SDK. Publishing the new release runs `.github/workflows/release.yml`, which builds and uploads the plugin without a publishing secret. In the Portal, select the build, create a release, add it to a submission and submit for review. Check that the Portal owner shown for the Project matches `publisher.name` in the manifest. The Store requires passing conformance reports on every declared platform and a supported Macro Deck SDK. Generated artifacts and local debug state are ignored by Git; upload source, not `bin/`, `obj/` or a local credential.

Macro Deck packages are pinned together at `3.0.0-beta.14` in `Directory.Packages.props`, above the [Store SDK minimum](https://api.macro-deck.app/api/v1/public/dependency-policy/sdk) of `3.0.0-beta.12`. Check the build's dependency report under **Builds** in the Creator Portal before submitting for review.

The installed package is run and authorized by Macro Deck. The `Properties/launchSettings.json` profile is only for interactive developer debugging against a locally running host; it is not used by store installs.

## License

MIT. See [LICENSE](LICENSE).
