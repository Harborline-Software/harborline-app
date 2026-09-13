# Harborline App

> **Status: pre-release.** Harborline is under active development and is not ready for production use. APIs, schemas, storage formats and package names change without notice, and there are no supported installs yet. Source is licensed under [Apache-2.0](LICENSE); see [NOTICE](NOTICE) and the [trademark policy](TRADEMARKS.md).

Harborline App is the human interface to the Harborline API. People use it to author domain models, perform enterprise work and inspect the evidence supporting operational outcomes. Pilot assists through the same governed interfaces. React and Blazor implement the human experience, with native-device behavior inside the appropriate host adapters.

## Find the right implementation

[Application sources](apps/) contain the executable applications; [shared app modules](src/) provide session, navigation, extension and host contracts. The [host manifest](hosts/host-manifest.json) records intended targets and host status. A registered host is not necessarily runnable: consult its source and host instructions before choosing a deployment target.

For React development, start with the [React application guide](apps/react/README.md). Native Blazor host requirements are recorded in the [MAUI host guide](hosts/blazor-maui/README.md). Consult project files and manifests for package identities and supported targets rather than inferring support from directory names.

## Run the Blazor development host

Install the SDK selected by [global.json](global.json), respecting its roll-forward policy. Check installed SDKs with `dotnet --list-sdks`. Then run:

```sh
dotnet run --project apps/blazor
```

Open the URL reported by the process. [Launch settings](apps/blazor/Properties/launchSettings.json) define the development environment and address; use `--urls` to override the address. Use published output for deployment so its static assets are materialized.

Configure the Forms client with `FormsAdmin:BaseUrl`, or explicitly select the development fixture with `FormsAdmin:UseFixture=true`. Environment variables use `FormsAdmin__BaseUrl` and `FormsAdmin__UseFixture`. The [development settings](apps/blazor/appsettings.Development.json) record the host's defaults. React configuration belongs in its application guide. Fixture behavior does not establish a working API integration.

## Verify and contribute

Run the repository gate from a clean committed tree:

```sh
bash eng/verify.sh
```

The [gate script](eng/verify.sh) defines its prerequisites and checks; [.github/workflows](.github/workflows/) defines automation for this checkout. Use a Bash environment to run shell scripts. Language and host dependencies are recorded in their SDK and package manifests.

Read [CONTRIBUTING.md](CONTRIBUTING.md) before changing public behavior. Consult [repository metadata](repository.yaml) and the applicable publishing workflow for package authority and release conditions. A successful build or fixture run is evidence for that check, not a supported-install claim.
