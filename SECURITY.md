# Security Policy

## Supported versions

AgentQL is pre-1.0. Only the latest published release receives security fixes.

| Version | Supported |
|---------|-----------|
| 0.1.x   | ✅        |
| < 0.1   | ❌        |

## Reporting a vulnerability

Please report security issues **privately** via GitHub's private vulnerability
reporting:

<https://github.com/daniel3303/AgentQL/security/advisories/new>

Do not open a public issue or pull request for a suspected vulnerability.

Because AgentQL executes LLM-generated SQL against your database, please be
especially specific about anything that could bypass the read-only transaction,
row-limit, or timeout safeguards in `QueryExecutor`.

### What to expect

- Acknowledgement within **3 business days**.
- An initial assessment and severity classification within **7 business days**.
- Coordinated disclosure once a fix is available; you will be credited unless
  you ask otherwise.
