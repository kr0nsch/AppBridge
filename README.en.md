<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/media/banner-en-dark.png">
    <img src="docs/media/banner-en-light.png" width="880" alt="FreeReturn — move your App Store apps to another iPhone">
  </picture>
</p>

<p align="center">
  <a href="https://github.com/kr0nsch/FreeReturn/releases/latest"><img src="https://img.shields.io/github/v/release/kr0nsch/FreeReturn?label=download&color=007aff&style=for-the-badge" alt="Download the latest version"></a>
</p>

<p align="center">
  <a href="https://github.com/kr0nsch/FreeReturn/actions/workflows/ci.yml"><img src="https://github.com/kr0nsch/FreeReturn/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078d4" alt="Windows 10 and 11">
  <a href="LICENSE"><img src="https://img.shields.io/github/license/kr0nsch/FreeReturn?color=34c759" alt="MIT license"></a>
  <a href="https://github.com/kr0nsch/FreeReturn/releases"><img src="https://img.shields.io/github/downloads/kr0nsch/FreeReturn/total?color=5856d6" alt="Downloads"></a>
  <a href="README.md"><img src="https://img.shields.io/badge/README-%D0%BD%D0%B0%20%D1%80%D1%83%D1%81%D1%81%D0%BA%D0%BE%D0%BC-lightgrey" alt="README на русском"></a>
</p>

**FreeReturn** moves your App Store apps from one iPhone to another, right from a Windows PC. Connect both phones, tick the apps you want and press Install: FreeReturn downloads the original apps from Apple with your Apple Account and installs them over USB. Free, no jailbreak, no ads.

<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/media/demo-en-dark.gif">
    <img src="docs/media/demo-en-light.gif" width="760" alt="FreeReturn in action: choosing iPhones, picking apps, signing in to the App Store and installing">
  </picture>
  <br>
  <sub>Recorded in demo mode with simulated iPhones and apps.</sub>
</p>

## How it works

1. **Connect the iPhone with your apps.** FreeReturn lists every app installed from the App Store.
2. **Pick the apps.** Search and Select All are there when you need them.
3. **Connect the other iPhone.** Keep both plugged in or swap them one after the other.
4. **Sign in to the App Store and press Install.** The next app downloads while the previous one installs.

<p align="center">
  <img src="docs/media/how-it-works-en.svg" width="880" alt="Diagram: iPhone with your apps → FreeReturn on a PC ← App Store; FreeReturn → your other iPhone">
</p>

## Screenshots

<table>
  <tr>
    <td width="33%" align="center">
      <picture>
        <source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/en-dark/1-connect.png">
        <img src="docs/screenshots/en-light/1-connect.png" alt="Connect an iPhone">
      </picture>
      <br><sub><b>Connect</b></sub>
    </td>
    <td width="33%" align="center">
      <picture>
        <source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/en-dark/2-choose-apps.png">
        <img src="docs/screenshots/en-light/2-choose-apps.png" alt="Choose apps">
      </picture>
      <br><sub><b>Choose apps</b></sub>
    </td>
    <td width="33%" align="center">
      <picture>
        <source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/en-dark/3-target.png">
        <img src="docs/screenshots/en-light/3-target.png" alt="Choose the other iPhone">
      </picture>
      <br><sub><b>Other iPhone</b></sub>
    </td>
  </tr>
  <tr>
    <td width="33%" align="center">
      <picture>
        <source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/en-dark/5-ready.png">
        <img src="docs/screenshots/en-light/5-ready.png" alt="Ready to install">
      </picture>
      <br><sub><b>Options</b></sub>
    </td>
    <td width="33%" align="center">
      <picture>
        <source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/en-dark/6-installing.png">
        <img src="docs/screenshots/en-light/6-installing.png" alt="Installing apps">
      </picture>
      <br><sub><b>Installing</b></sub>
    </td>
    <td width="33%" align="center">
      <picture>
        <source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/en-dark/7-done.png">
        <img src="docs/screenshots/en-light/7-done.png" alt="All done">
      </picture>
      <br><sub><b>Done</b></sub>
    </td>
  </tr>
</table>

## Features

- **Four simple steps**, one button per screen.
- **Original apps from Apple.** Every app is downloaded from Apple's servers with your Apple Account — nothing comes from third-party sites.
- **All over USB.** No iTunes, Python or command line: FreeReturn finds the iPhone, shows the "Trust This Computer?" prompt, reads the apps with their icons and installs them.
- **Fast.** Downloads and installs run side by side, every app shows its progress, and if one can't be installed you see why, with a Try Again button.
- **Your choice of version:** the latest from the App Store, or the same version as on the first iPhone (while Apple still offers it).
- **Free apps** can be added to your Apple Account automatically when they're not in your purchases yet.
- **Light and dark mode, English and Russian.**
- **Installer and portable version.** Nothing else to install: .NET and the Windows App SDK are included.

## Tested on real devices

| From | To | Apps | Result |
|---|---|---|---|
| iPhone 16 Pro | iPhone 14 | Sber, VK, Mail.ru Mail | ✅ Transferred |

Tried it on your devices? Tell us in the [issues](https://github.com/kr0nsch/FreeReturn/issues) and we'll add it to the table.

## Download

Get the latest version from **[Releases](https://github.com/kr0nsch/FreeReturn/releases/latest)**:

| File | What it is |
|---|---|
| `FreeReturn-<version>-setup-x64.exe` | **Installer (recommended).** Installs for your user account, no administrator rights needed. Start menu shortcut, uninstall from Settings. |
| `FreeReturn-<version>-portable-x64.zip` | **Portable version.** Unzip anywhere, even a USB stick, and run `FreeReturn.exe`. Settings and downloads stay in the `Data` folder next to it. |
| `SHA256SUMS.txt` | Checksums to verify the files. |

**System requirements:** Windows 10 version 2004 or later, or Windows 11. x64 PCs; on Arm PCs, Windows 11 (through its built-in emulation).

> [!NOTE]
> Every release file is built by GitHub Actions straight from this repository and carries a signed build provenance attestation (Sigstore). Verify it with `gh attestation verify <file> -R kr0nsch/FreeReturn`.
> There is no Authenticode certificate yet, so SmartScreen may warn about an unknown publisher — choose **More info → Run anyway**.

## What you need

- **The Apple Devices app** from the [Microsoft Store](https://apps.microsoft.com/detail/9NP83LWLPZ9K) (or iTunes). It contains the driver Windows uses to talk to iPhones; FreeReturn tells you if it's missing.
- **A USB cable** that supports data, and both iPhones unlocked. Tap **Trust** when an iPhone asks whether to trust this computer.
- **The Apple Account** the apps were downloaded with (or one that can get them for free).
- An internet connection.

## FAQ

<details>
<summary><b>Does it move app data too?</b></summary>

No, FreeReturn installs the apps themselves. Documents, chats and settings come back as usual — from iCloud, a backup, or by signing in inside the app.
</details>

<details>
<summary><b>Which apps can be moved?</b></summary>

The ones your Apple Account can get from the App Store. FreeReturn can add free apps to your account for you, just like tapping Get in the App Store (you can turn this off). Paid apps must already be bought with that account. Apps pulled from the App Store can only be installed while Apple still serves them to their owners. Apps from outside the App Store (TestFlight, developer builds) aren't shown, because they can't be downloaded again.
</details>

<details>
<summary><b>Is it safe to enter my Apple Account password?</b></summary>

Your password is passed straight to the bundled, open-source [ipatool](https://github.com/majd/ipatool), which signs in to Apple. FreeReturn never stores it, never logs it and never sends it anywhere else. The App Store session is stored encrypted, with the key protected by Windows (DPAPI) for your user account; Sign Out removes it. FreeReturn has no servers, ads or telemetry. See [SECURITY.md](SECURITY.md).
</details>

<details>
<summary><b>Do I need a jailbreak? Does it bypass Apple's protection?</b></summary>

No and no. Apps are installed just like from the App Store and stay tied to your Apple Account (FairPlay protection is not bypassed).
</details>

<details>
<summary><b>The iPhone isn't listed</b></summary>

Use a cable that carries data, plugged straight into the PC, unlock the iPhone and make sure the Apple Devices app is installed. Try another USB port.
</details>

<details>
<summary><b>"Tap Trust on the iPhone" doesn't go away</b></summary>

Unlock the iPhone — the prompt only appears on an unlocked screen. If you tapped Don't Trust, press Try Again or replug the cable.
</details>

<details>
<summary><b>"Not in this Apple Account's purchases"</b></summary>

The app was downloaded with a different Apple Account. Sign in with that account, or turn on "Get free apps you don't own yet" (for free apps).
</details>

<details>
<summary><b>Where does FreeReturn keep its data?</b></summary>

| | Installer | Portable |
|---|---|---|
| Settings, logs, App Store session | `%LOCALAPPDATA%\FreeReturn` | `Data\` next to `FreeReturn.exe` |
| Downloaded apps (`.ipa`) | `Downloads\FreeReturn` (changeable in Settings) | `Data\Downloads` |

Downloaded `.ipa` files contain your Apple Account email (Apple adds it to every purchase) — don't share them.
</details>

<details>
<summary><b>Something went wrong</b></summary>

Open **Settings → Diagnostic logs → Show** and attach the latest log to an [issue](https://github.com/kr0nsch/FreeReturn/issues/new/choose). Logs contain no passwords or codes.
</details>

## Building from source

You need Windows 10/11, the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), and [Inno Setup 6](https://jrsoftware.org/isinfo.php) for the installer.

```powershell
# ipatool (pinned version, SHA-256 verified)
./build/Get-Ipatool.ps1

# Tests
dotnet test tests/FreeReturn.Core.Tests

# Run
dotnet run --project src/FreeReturn -p:Platform=x64

# Portable ZIP and installer into artifacts/release
./build/build.ps1 -Version 1.0.0
```

Start the app with `--demo` (or set `FREERETURN_DEMO=1`) for simulated iPhones and a simulated App Store — handy for UI work without devices or an Apple Account.

Pushing a tag like `v1.2.3` runs [GitHub Actions](.github/workflows/release.yml): build, tests, and a release with the installer, the portable version and checksums.

<details>
<summary><b>Project layout</b></summary>

| Path | Contents |
|---|---|
| `src/FreeReturn` | WinUI 3 app: screens, view models, styles, translations (`Strings/`) |
| `src/FreeReturn.Core` | iPhone access over USB (trust, app list, installation), ipatool integration, transfer pipeline |
| `tests/FreeReturn.Core.Tests` | Unit tests, including a check against the real ipatool |
| `installer` | Inno Setup script and installer artwork |
| `build` | Release script, ipatool download with checksum pinning, icon generator |
| `legacy/python` | The first cross-platform Python prototype (unmaintained) |

FreeReturn is built on [WinUI 3](https://github.com/microsoft/microsoft-ui-xaml) / Windows App SDK, [Netimobiledevice](https://github.com/artehe/Netimobiledevice) (iPhone access over USB) and [ipatool](https://github.com/majd/ipatool) (App Store downloads). Licenses are in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).
</details>

## License

[MIT](LICENSE). FreeReturn is not affiliated with or endorsed by Apple Inc. iPhone and App Store are trademarks of Apple Inc. Use it only with apps you are entitled to.
