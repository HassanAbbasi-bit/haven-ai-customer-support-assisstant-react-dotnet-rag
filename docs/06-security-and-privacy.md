# Security and privacy

## Trust boundaries

The browser, user messages and uploaded files are untrusted. Retrieved documents can contain malicious instructions. Ollama output is untrusted until validated. SQL ownership and publication records are authoritative. The model cannot grant access, submit tickets, change roles, execute tools, fetch URLs or read arbitrary files.

## Authentication and authorization

- Use ASP.NET Core Identity password hashing, unique normalized emails, validated password policy, lockout and throttled authentication. Bootstrap an admin through a one-time developer command and secrets; no public admin signup or hardcoded credentials.
- Implement signed JWT access tokens with issuer, audience, subject, role, issued/expiry time and unique token ID. Initial access lifetime 15 minutes; validate signature, issuer, audience and lifetime with small bounded clock skew. Keep access tokens in memory, not localStorage.
- Rotate opaque refresh tokens in HttpOnly, Secure, SameSite cookies; store only hashes; expire after seven days initially. Reuse revokes the token family. Logout revokes refresh sessions; describe the remaining short access-token lifetime or add a denylist for immediate revocation. Require immediate invalidation for admin role removal through a security-stamp/session check.
- Protect cookie-authenticated refresh/logout/guest operations with origin checks and antiforgery tokens as appropriate. CORS uses an explicit allowlist, never wildcard credentials. Local HTTPS is required for browser security behavior testing.
- Anonymous chat uses a high-entropy server-generated guest cookie mapped to SQL; never use a conversation UUID as authentication. Guest sessions expire after 24 hours initially. Sign-in does not automatically merge guest history without an explicit ownership-checked migration flow; v1 can leave histories separate with clear copy.
- Authorize every conversation, message, citation, ticket, feedback and admin resource at the API. Cross-owner reads return 404 to reduce enumeration. Admin access to support context requires an authorized purpose and is audited.
- Public account registration, password recovery and verified-email delivery need a real mail provider before public launch. The local demo may use seeded customer accounts or a development-only mailbox; do not claim email verification is operational. Disable nonfunctional recovery links rather than creating dead flows.

ASP.NET Identity bearer endpoints are not a shortcut to assume standards-based JWT support. Explicit JWT creation/validation is required by this project's stack. Review the [official Identity guidance](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0) when implementing the boundary.

## Content and upload controls

Enforce extension, content type, PDF signature, UTF-8 decoding, byte/page/text caps and extraction deadline. Use random private storage names outside web root, no path traversal, no inline execution and no unrestricted file download URL. Run extraction under a low-privilege identity with resource limits and bounded concurrency. Production use requires malware scanning/quarantine before extraction; if unavailable, keep deployment confined to the trusted local demo.

Render escaped text or a strict Markdown allowlist with raw HTML disabled. Test XSS in chat, document titles, citations, feedback and ticket fields. Disallow unsafe links and model-supplied source URLs. Do not fetch external URLs embedded in documents or questions. Prompt injection defenses combine instruction/data separation, no model tools, retrieval authorization and output validation; prompting alone is insufficient.

## Privacy and retention defaults

| Data | Initial policy |
| --- | --- |
| Guest conversation | Expires at 24 hours; purge content within the next daily cleanup run |
| Registered history | 30-day rolling retention; owner deletion removes from normal access immediately |
| Feedback linked to deleted chat | Delete identifiable linkage and comment with chat; retain only anonymous aggregate counts if safe |
| Tickets | 90 days after closure; user-approved excerpts are independent retained records disclosed at submission |
| Draft tickets | Browser memory only; do not persist email/order reference in localStorage |
| Logs/traces | 14 days, no raw messages, tokens, passwords, emails or complete document contents by default |
| Source versions | Retain while published or required by retained citations; withdrawn content inaccessible to customers; purge after references expire |
| Deleted files/vectors | Logical revocation immediate; cleanup job target <= 24 hours with monitored retry |
| Backups | Initial seven-day local backup rotation; explain delayed physical removal from backups |

These are product defaults, not legal compliance claims. Before a public deployment, confirm requirements for the actual operating jurisdiction and publish an appropriate privacy notice. Use synthetic data for the internship demo. Ticket consent explains the exact text and contact information being retained.

## Abuse and dependency controls

Start with per-session chat 10/minute, per-IP guest creation 10/hour, login 5/minute plus Identity lockout, admin upload 5/minute and a global bounded generation queue. Tune after load tests; avoid relying on IP alone behind shared networks. Trust forwarded headers only from configured proxies. Cap ticket/feedback submissions and validate all lengths.

Use parameterized EF queries, least-privilege SQL credentials, private Qdrant/Ollama networking, encrypted HTTPS at the application boundary, and protected persistent volumes. Development SQL certificate trust bypass must never become a production default. Keep connection strings, JWT keys and passwords in user-secrets/environment secret stores; commit only a blank `.env.example` when implementation begins. Redact telemetry and scan repository history for accidental secrets.

CI must check vulnerable dependencies and licenses. Pin artifacts and review updates. Security tests include missing/expired/wrong-audience tokens, role escalation, revoked sessions, CSRF, IDOR, malicious PDFs, upload traversal, prompt injection, stored XSS and private-source leakage. A known critical authorization or leakage defect blocks release regardless of average evaluation score.
