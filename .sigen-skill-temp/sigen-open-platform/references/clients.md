# Client requirements

The official download contains an environment-specific `config.json` beside
`SKILL.md`:

```json
{
  "region": "ENVIRONMENT_REGION",
  "login_url": "https://OFFICIAL_SITE/authorize",
  "mcp_url": "https://PUBLIC_GATEWAY/mcp",
  "token_url": "https://TOKEN_GATEWAY/auth/oauth/token",
  "catalog_url": "https://PUBLIC_GATEWAY/catalog/v1"
}
```

Package publishers set these public endpoints before distribution. End users
and AI agents do not need to edit them. Credentials are isolated by a hash of
the configured environment, so packages for different environments do not
reuse each other's local tokens.

The MCP service itself does not publish an OAuth authorization endpoint in the
gateway-token deployment.

The authorization URL is the Sigen-hosted login page. The bundled
`scripts/sigen.mjs` helper owns a temporary loopback callback, validates state,
stores credentials locally, refreshes the access token, and calls the remote
MCP without printing tokens.

On desktop, the helper passes the complete authorization URL as one argument
without invoking a shell:

- Windows: `rundll32.exe url.dll,FileProtocolHandler <complete-url>`;
- macOS: `/usr/bin/open <complete-url>`;
- Linux: `xdg-open <complete-url>`.

If the system launcher is unavailable, returns an error, or times out, the
helper prints the same complete URL for the user to open manually. It never
logs a token because tokens are not present in the authorization URL.

## Supported local AI agents

The AI agent needs all of the following:

1. access to the extracted Skill directory;
2. Node.js 18.17 or newer;
3. permission to execute a local script and open the default browser;
4. loopback access to `127.0.0.1`.

Codex, Cursor, Claude Code, and similar local agents can invoke the same client:

```bash
node scripts/sigen.mjs tools
node scripts/sigen.mjs catalog
node scripts/sigen.mjs tool get_account_profile
node scripts/sigen.mjs call get_account_profile '{}'
```

Run `catalog` during initial onboarding or when the user explicitly asks to
view or refresh the latest Sigen Tools. Run `tool` to get one Tool's full
description and JSON input schema. These public commands do not require or
trigger authorization. Normal Tool calls do not pull the Catalog automatically;
the server still checks its current external allowlist for every call. `call`
and the diagnostic `tools` command automatically authorize or refresh
credentials when needed. The user enters a password only on the Sigen-hosted
page.

## Clients without local script execution

A chat-only or hosted AI that cannot run the bundled Node.js helper cannot own
the loopback callback or safely retain the refresh token. Do not paste tokens
into such a client. A native integration for that client is required.

## Android and iOS

The bundled Node.js helper is a desktop client for Windows, macOS, and Linux.
It does not claim Android or iOS support: the current authorization result is
posted to a `127.0.0.1` loopback callback, which is not a portable mobile return
mechanism.

A native mobile integration requires a platform adapter and a server-approved
callback transport. Android should use Custom Tabs or an HTTPS `ACTION_VIEW`
intent with a verified App Link (or a dedicated, collision-resistant custom
scheme). iOS should use `ASWebAuthenticationSession` with a Universal Link or
registered callback scheme. Both platforms must validate `state`, reject an
unexpected callback scheme/domain, and keep refresh credentials in secure OS
storage such as Android Keystore-backed storage or iOS Keychain.

Authorization Code with PKCE is the recommended mobile protocol: the client
creates a verifier/challenge, receives a short-lived one-time code, and exchanges
it for tokens outside the URL. Until the Sigen authorization service supports
that callback and exchange, mobile clients must report authorization as
unsupported instead of running desktop launch commands or pretending the
loopback flow succeeded. The existing desktop token handoff remains unchanged.
