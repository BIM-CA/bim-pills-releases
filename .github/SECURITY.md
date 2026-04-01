# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| 1.0.x   | Yes       |
| < 1.0   | No        |

## Reporting a Vulnerability

If you discover a security vulnerability in BIMPills, please report it responsibly:

1. **Email:** soporte@bim-ca.com
2. **Subject:** `[SECURITY] BIMPills — <brief description>`
3. **Do NOT** open a public issue for security vulnerabilities.

We will acknowledge receipt within 48 hours and aim to provide a fix within 7 business days for critical issues.

## Security Measures

- **DPAPI encryption** for API keys and credentials at rest (Windows Data Protection API)
- **TypeNameHandling.None** in all JSON deserialization to prevent type injection
- **Process.Start whitelist** for file extensions and URL schemes
- **No secrets in source code** — all credentials are encrypted per-user, per-machine

## Scope

This policy covers the BIMPills Revit plugin source code and its installer. Third-party dependencies (ClosedXML, Newtonsoft.Json) are tracked via Dependabot.
