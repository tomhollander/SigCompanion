# Authorization security contract

- The Sigen login form belongs to the Sigen web application, not the AI chat.
- The helper places the loopback `redirect_uri` in the authorization URL
  fragment. The fragment stays in the browser and is not sent to the gateway;
  it contains no token.
- The client-local login handoff receives the Sigen access/refresh pair without
  putting either token in a URL, analytics event, console log, or chat message.
- The bundled client stores credentials in a user-local file with directory
  mode `0700` and file mode `0600` where the operating system supports POSIX
  permissions. Different endpoint configurations use different credential
  files. The AI must not read or print these files.
- The official package stores only public endpoints in `config.json`. It never
  contains an MCP key, access token, refresh token, username, or password.
- Login, token, and production MCP endpoints must use HTTPS. Plain HTTP is
  accepted only for loopback tests and the explicitly configured test MCP.
- The client sends only the access token to the gateway/MCP as a Bearer token.
  The refresh token never reaches the MCP server.
- The gateway validates the user token and either preserves the Bearer value or
  overwrites `X-Sigen-User-Token`. It must strip untrusted client values before
  setting that header.
- When account quota is enabled, the MCP resolves the stable `userId` from
  `/admin/user/me`, hashes it before using it in Redis, and briefly caches the
  token-fingerprint-to-user mapping. Client-supplied `X-Sigen-User-Id` values
  are ignored; access-token refresh does not reset the account quota.
- MCP sessions are bound to a token fingerprint; a different user token cannot
  reuse the session id.
- The external MCP tool surface is deny-by-default and uses only the exact
  deployment allowlist. `login_with_password` and `api_call` remain blocked.
- The unauthenticated public Catalog contains only Tool metadata and schemas;
  it contains no user data and uses the same exact external allowlist. Reading
  it must never trigger login.
- The AI reads the public Catalog during onboarding or when the user explicitly
  requests a Tool refresh. Normal Tool calls do not pull the Catalog
  automatically. A stale
  package cannot bypass the current server allowlist because the server checks
  the exact external allowlist during every `tools/call`.
- Public writes use preview/confirm. Direct mode is forbidden externally.
- Redis-backed RU limits cover fixed daily, minute, 10-second burst, and active
  concurrency windows. Successful reads and confirmed writes consume RU;
  preview-only writes and `get_quota_usage` do not. A quota error is not an
  authorization failure and must not trigger a new browser login.
- When refresh credentials are invalid, restart browser login. Never collect a
  password in chat.
- The bundled loopback handoff is desktop-only. Android and iOS integrations
  require an approved native callback adapter, strict `state` validation,
  secure refresh-token storage, and preferably Authorization Code with PKCE;
  they must not reuse desktop launcher commands or claim support while the
  server only returns authorization results to `127.0.0.1`.
