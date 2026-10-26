# Changelog

All notable changes to AppBridge are listed here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
[Semantic Versioning](https://semver.org/).

## [1.1.0] - 2026-10-27

### Added
- A start screen with two choices: **Transfer Now** (both iPhones connected, as before) and **Transfer Later**.
- **Transfer Later** works in two steps that can be weeks apart:
  - **Save Apps** downloads the apps from the old iPhone to this PC (the downloads folder).
  - **Install on New iPhone** installs saved apps over USB later, without signing in to the App Store on the PC.
- Saved apps are listed with their icons and home-screen names, including `.ipa` files kept by 1.0 or copied into the folder.
  - For each app, AppBridge picks the newest saved version that the new iPhone can run.
  - Apps the iPhone can't run (iOS too old, iPad only) are listed separately with the reason.
  - Apps already on the iPhone are marked and left unticked; older installed versions are updated.
- Saved files get readable names such as `Telegram 12.1.ipa`, and different versions of an app are kept side by side.
- Icons read from an iPhone are cached, so saved apps keep them after the phone is unplugged.

### Changed
- AppBridge opens on the start screen. Back from the first step and Done return to it.

[1.1.0]: https://github.com/kr0nsch/AppBridge/releases/tag/v1.1.0

## [1.0.0] - 2026-10-26

First release of the Windows app.

### Added
- New WinUI 3 app with a four-step flow: connect the source iPhone, choose apps, connect the target iPhone, install.
- Apple-style design with light and dark mode; English and Russian. Respects Windows high-contrast themes and the "Animation effects" setting.
- Built-in USB support for iPhones (no Python or extra tools): detection, the "Trust This Computer" prompt, reading installed apps with their icons, and installing with progress.
- App Store sign-in with two-factor authentication, using the bundled open-source ipatool.
- Downloads run while the previous app installs; per-app progress and clear reasons when an app can't be installed; retry for failed apps.
- Options to install the latest version or the same version as the source iPhone, to get free apps not yet owned, and to keep downloaded `.ipa` files.
- Installer (per-user, no administrator rights) and portable ZIP for x64.
- Diagnostic logs, single-instance window, keeps the PC awake during installation.

### Changed
- The original Python/Tkinter prototype moved to `legacy/python`.

[1.0.0]: https://github.com/kr0nsch/AppBridge/releases/tag/v1.0.0
