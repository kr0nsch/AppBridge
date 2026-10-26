# Security policy

## Supported versions

Only the latest release receives fixes.

## Reporting a vulnerability

Please report security problems privately through
[GitHub security advisories](https://github.com/kr0nsch/FreeReturn/security/advisories/new)
rather than in a public issue. Include the FreeReturn version, what you found, and how to
reproduce it. You should get a reply within a week.

## How FreeReturn handles secrets

- The Apple Account password is passed to the bundled ipatool for a single sign-in request
  and is never stored or logged by FreeReturn. While that request runs, it is visible on
  ipatool's command line to other programs running under the same Windows user.
- ipatool's session file is encrypted with a random passphrase. The passphrase is stored
  with Windows DPAPI (current-user scope) in `%LOCALAPPDATA%\FreeReturn\ipatool\keychain.key`
  (or the portable `Data` folder).
- Diagnostic logs never contain passwords, verification codes or passphrases, and device
  identifiers are shortened.
- The bundled ipatool binary is downloaded from its official GitHub release and checked
  against a pinned SHA-256 checksum (`build/ipatool.json`) during every build.
