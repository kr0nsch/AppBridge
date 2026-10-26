# Changelog

All notable changes to FreeReturn are listed here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
[Semantic Versioning](https://semver.org/).

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

[1.0.0]: https://github.com/kr0nsch/FreeReturn/releases/tag/v1.0.0
