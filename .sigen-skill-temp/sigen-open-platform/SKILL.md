---
name: sigen-open-platform
description: Connect a local AI agent to the public Sigen MCP, open browser-based Sigen authorization without collecting a password in chat, refresh credentials, and use externally allowed Sigen tools. Use whenever the user asks to sign in to Sigen, query their Sigen user/account information, or use a Sigen Open Platform capability after installing this Skill.
---

# Sigen Open Platform

Use the bundled `scripts/sigen.mjs` client. Do not ask the user to configure
URLs, environment variables, MCP keys, access tokens, or refresh tokens.

The official package supplies its environment-specific login, MCP, public
Catalog, refresh, and region settings in `config.json`. Do not rewrite this
file during normal user requests.

Never ask the user to type a Sigen password or token into chat. The client opens
the Sigen-hosted login page and receives its result through a temporary
loopback callback.

## Handle a user request

1. Resolve `scripts/sigen.mjs` relative to this `SKILL.md`.
2. Read the public Tool Catalog during initial onboarding, or when the user
   explicitly asks to refresh, update, or view the available Sigen tools:

   ```bash
   node scripts/sigen.mjs catalog
   ```

   This command is public and must not open login. Treat its result as the
   current public capability set for the conversation. Fetch a Tool's full
   description and input schema only when needed:

   ```bash
   node scripts/sigen.mjs tool TOOL_NAME
   ```

3. Do not automatically pull the Catalog before or after a normal Tool call.
   Server-side Tool additions and description/schema changes require initial
   onboarding or an explicit user refresh, but no Skill package update.
4. Call a documented Tool or one returned by the latest user-requested refresh
   with JSON arguments:

   ```bash
   node scripts/sigen.mjs call TOOL_NAME 'JSON_ARGUMENTS'
   ```

   The server validates the Tool against its current external allowlist during
   every call. If the server reports that a Tool is unavailable, stop and tell
   the user they can request a Tool refresh.

5. For “query my user/account information”, use the bundled documented
   `get_account_profile` Tool directly:

   ```bash
   node scripts/sigen.mjs call get_account_profile '{}'
   ```

   Return only the profile information needed to answer the request. Do not
   expose credential files or unrelated personal data.

6. For quota, usage, remaining-call, or reset-time questions, call the
   zero-cost `get_quota_usage` Tool. Report the relevant daily, minute,
   10-second burst, and concurrency values from its visible response; do not
   infer current usage from the static policy or from earlier Tool calls.

Read [references/clients.md](references/clients.md) when client compatibility is
unclear.

## Use tools safely

- Call only bundled documented tools or tools returned by the latest public
  Catalog read.
- Never call or propose `login_with_password` or `api_call`.
- Keep account identifiers, station data, credentials, and callback payloads
  out of logs and final responses unless the user explicitly needs a
  non-secret identifier.
- For a write tool, show the preview and execute confirmation only after the
  user explicitly approves it.

## Recover authorization

The bundled client refreshes an expired access token and retries once. If the
refresh token is also invalid, it removes the stale local credential and opens
browser authorization again. Never fall back to password entry in chat.

## Troubleshoot

- Use `node scripts/sigen.mjs status` to check only whether local authorization
  exists. Never read the credential file directly.
- Use `node scripts/sigen.mjs doctor` to verify the local runtime, endpoints,
  public Catalog, and authorization status without printing credentials.
- Use `node scripts/sigen.mjs tools` only to diagnose the authenticated MCP
  `tools/list` protocol; unlike `catalog`, it may trigger login.
- Use `node scripts/sigen.mjs login` to restart browser authorization.
- Use `node scripts/sigen.mjs logout` only when the user asks to disconnect the
  local Sigen account.
- A plain `401 Bearer` means the current access token was rejected.
- `daily_quota_exceeded` means the account must wait until `reset_at`.
  `minute_rate_limit_exceeded` and `burst_rate_limit_exceeded` mean wait until
  `retry_at`; `concurrency_limit_exceeded` means wait for an active Tool call
  to finish. Do not restart login for any quota error.
- Use `get_quota_usage` to check the current account's quota without consuming
  RU. Do not make a different read call merely to inspect hidden quota metadata.
- `daily_quota_unavailable` is temporary and may be retried later without
  reauthorizing.
- An empty tool list means the external allowlist is empty.
- If `get_account_profile` belongs to another user, stop immediately and report
  a gateway/token-isolation incident.
- If the AI client cannot execute local Node.js scripts, report that this
  package cannot complete its secure loopback handoff in that client. Do not
  expose tokens in chat as a workaround.
- The bundled helper supports desktop Windows, macOS, and Linux only. Android
  and iOS require a native authorization adapter and a server-approved mobile
  callback (preferably Authorization Code with PKCE); never run desktop opener
  commands or claim mobile support for the current `127.0.0.1` loopback flow.

Read [references/security.md](references/security.md) before changing or
debugging the login/token flow.
