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
- Release files are built by GitHub Actions from the tagged commit and carry a Sigstore build
  provenance attestation: `gh attestation verify <file> -R kr0nsch/FreeReturn`.
- Authenticode signing of `FreeReturn.exe`, its DLLs and the installer turns on automatically
  when the repository secrets `WINDOWS_SIGNING_CERT` (base64 .pfx) and
  `WINDOWS_SIGNING_CERT_PASSWORD` are set.
