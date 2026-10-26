# Contributing

Thanks for helping improve FreeReturn!

## Getting started

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
2. Run `./build/Get-Ipatool.ps1` once to fetch the pinned ipatool binary.
3. Build and run: `dotnet run --project src/FreeReturn -p:Platform=x64`.
4. Run the tests: `dotnet test tests/FreeReturn.Core.Tests`.

Use `--demo` to work on the UI without iPhones or an Apple Account:

```powershell
dotnet run --project src/FreeReturn -p:Platform=x64 -- --demo
```

## Guidelines

- Keep the flow simple: four steps, one primary button per screen.
- Every user-visible string goes into `src/FreeReturn/Strings/en-US/Resources.resw` **and**
  `ru-RU/Resources.resw`. A unit test checks that both files have the same keys and placeholders.
- Keep `README.md` (Russian, the main page) and `README.en.md` (English) in sync.
- Never log or persist passwords, verification codes or passphrases.
- Device and App Store code lives in `FreeReturn.Core` behind `IDeviceService`,
  `IDeviceWatcher` and `IAppStoreClient`, so it can be tested with fakes.
- Run the tests before opening a pull request. If you change USB code, please say which
  iPhone models and iOS versions you tried.

## Releasing

1. Update `CHANGELOG.md`.
2. Tag the commit: `git tag v1.2.3 && git push origin v1.2.3`.
3. The release workflow builds the installer and portable ZIP, and publishes them with checksums.
