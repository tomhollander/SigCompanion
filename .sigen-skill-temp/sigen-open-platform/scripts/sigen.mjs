#!/usr/bin/env node

import { createServer } from "node:http";
import { createHash, randomBytes } from "node:crypto";
import { chmod, mkdir, readFile, rename, rm, writeFile } from "node:fs/promises";
import { homedir, platform } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { openExternalUrl } from "./browser.mjs";

const PUBLIC_CLIENT_AUTHORIZATION = "Basic c2lnZW46c2lnZW4=";
const CALLBACK_PATH = "/callback";
const REQUEST_TIMEOUT_MS = 20_000;

function fail(message, status = 1) {
  process.stderr.write(`${message}\n`);
  process.exit(status);
}

function usage(message, status = 2) {
  if (message) process.stderr.write(`${message}\n\n`);
  process.stderr.write(`Usage:
  node scripts/sigen.mjs call <tool-name> [JSON-arguments]
  node scripts/sigen.mjs catalog
  node scripts/sigen.mjs tool <tool-name>
  node scripts/sigen.mjs tools
  node scripts/sigen.mjs login [--client-name NAME] [--no-open] [--timeout SECONDS]
  node scripts/sigen.mjs status
  node scripts/sigen.mjs doctor
  node scripts/sigen.mjs refresh
  node scripts/sigen.mjs logout
`);
  process.exit(status);
}

async function readPackageConfig() {
  const path = join(dirname(fileURLToPath(import.meta.url)), "..", "config.json");
  let parsed;
  try {
    parsed = JSON.parse(await readFile(path, "utf8"));
  } catch {
    fail("Sigen package config.json is missing or invalid.");
  }
  for (const key of ["login_url", "mcp_url", "token_url", "catalog_url", "region"]) {
    if (typeof parsed?.[key] !== "string" || !parsed[key].trim()) {
      fail(`Sigen package config '${key}' is missing.`);
    }
  }
  return parsed;
}

const PACKAGE_CONFIG = await readPackageConfig();
const LOGIN_URL = process.env.SIGEN_LOGIN_URL || PACKAGE_CONFIG.login_url;
const MCP_URL = process.env.SIGEN_PUBLIC_MCP_URL || PACKAGE_CONFIG.mcp_url;
const TOKEN_URL = process.env.SIGEN_TOKEN_URL || PACKAGE_CONFIG.token_url;
const CATALOG_URL = process.env.SIGEN_TOOL_CATALOG_URL || PACKAGE_CONFIG.catalog_url;
const REGION = process.env.SIGEN_REGION || PACKAGE_CONFIG.region;
if (!/^[a-z0-9-]{1,32}$/i.test(REGION)) fail("Sigen region is invalid.");
const ENVIRONMENT_ID = createHash("sha256")
  .update(`${REGION}\n${LOGIN_URL}\n${MCP_URL}\n${TOKEN_URL}`, "utf8")
  .digest("hex")
  .slice(0, 12);

function credentialPath() {
  if (process.env.SIGEN_CREDENTIAL_FILE) return process.env.SIGEN_CREDENTIAL_FILE;
  const filename = `credentials-${ENVIRONMENT_ID}.json`;
  if (platform() === "win32" && process.env.APPDATA) {
    return join(process.env.APPDATA, "Sigen Open Platform", filename);
  }
  if (platform() === "darwin") {
    return join(homedir(), "Library", "Application Support", "Sigen Open Platform", filename);
  }
  return join(
    process.env.XDG_CONFIG_HOME || join(homedir(), ".config"),
    "sigen-open-platform",
    filename,
  );
}

const CREDENTIAL_FILE = credentialPath();

function isLoopback(hostname) {
  return hostname === "localhost" || hostname === "127.0.0.1" || hostname === "::1";
}

function validateEndpoint(value, label, { allowKnownTestHttp = false } = {}) {
  let url;
  try {
    url = new URL(value);
  } catch {
    fail(`${label} is invalid.`);
  }
  const knownTestHttp =
    allowKnownTestHttp &&
    url.protocol === "http:" &&
    url.hostname === "svc-test.sigencloud.com" &&
    url.port === "3334";
  if (url.protocol !== "https:" && !isLoopback(url.hostname) && !knownTestHttp) {
    fail(`${label} must use HTTPS (HTTP is allowed only for loopback or the configured test MCP).`);
  }
  return url;
}

async function readCredentials() {
  try {
    const parsed = JSON.parse(await readFile(CREDENTIAL_FILE, "utf8"));
    if (!parsed?.access_token && !parsed?.refresh_token) return null;
    if (parsed.environment_id !== ENVIRONMENT_ID) return null;
    return parsed;
  } catch (error) {
    if (error?.code === "ENOENT") return null;
    fail("Sigen credentials could not be read. Run logout, then sign in again.");
  }
}

async function writeCredentials(credentials) {
  const directory = dirname(CREDENTIAL_FILE);
  await mkdir(directory, { recursive: true, mode: 0o700 });
  await chmod(directory, 0o700).catch(() => {});
  const temporary = `${CREDENTIAL_FILE}.${process.pid}.${randomBytes(6).toString("hex")}.tmp`;
  await writeFile(temporary, `${JSON.stringify(credentials)}\n`, { mode: 0o600 });
  await chmod(temporary, 0o600).catch(() => {});
  await rename(temporary, CREDENTIAL_FILE);
  await chmod(CREDENTIAL_FILE, 0o600).catch(() => {});
}

async function deleteCredentials() {
  await rm(CREDENTIAL_FILE, { force: true });
}

function expiresAt(expiresIn) {
  const seconds = Number(expiresIn);
  return Number.isFinite(seconds) && seconds > 0 ? Date.now() + seconds * 1000 : undefined;
}

function html(message, success) {
  const color = success ? "#087443" : "#b42318";
  return `<!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>Sigen authorization</title></head><body style="font-family:system-ui;margin:48px;line-height:1.5"><h1 style="color:${color}">${message}</h1><p>You can close this tab and return to your AI application.</p></body></html>`;
}

function parseForm(body) {
  return Object.fromEntries(new URLSearchParams(body));
}

async function login({ clientName = "AI application", noOpen = false, timeoutSeconds = 300 } = {}) {
  const loginEndpoint = validateEndpoint(LOGIN_URL, "Sigen login URL");
  const state = randomBytes(32).toString("base64url");
  let settled = false;
  let complete;
  const completed = new Promise((resolve, reject) => {
    complete = { resolve, reject };
  });

  const server = createServer((request, response) => {
    const requestUrl = new URL(request.url || "/", "http://127.0.0.1");
    if (request.method !== "POST" || requestUrl.pathname !== CALLBACK_PATH) {
      response.writeHead(404, { "content-type": "text/plain; charset=utf-8" });
      response.end("Not found");
      return;
    }

    let body = "";
    request.setEncoding("utf8");
    request.on("data", (chunk) => {
      body += chunk;
      if (body.length > 64 * 1024) request.destroy();
    });
    request.on("end", async () => {
      if (settled) {
        response.writeHead(409, { "content-type": "text/html; charset=utf-8" });
        response.end(html("Authorization already completed", false));
        return;
      }
      try {
        const result = parseForm(body);
        if (result.state !== state) throw new Error("Authorization state did not match.");
        if (result.type !== "sigen.authorization.success" || result.version !== "1") {
          throw new Error("Unexpected Sigen authorization response.");
        }
        if (!result.access_token || !result.refresh_token) {
          throw new Error("The authorization response did not include both tokens.");
        }

        const credentials = {
          access_token: result.access_token,
          refresh_token: result.refresh_token,
          token_type: "Bearer",
          region: result.region || REGION,
          environment_id: ENVIRONMENT_ID,
          expires_at: expiresAt(result.expires_in),
          username: result.username || undefined,
          saved_at: new Date().toISOString(),
        };
        await writeCredentials(credentials);
        settled = true;
        response.writeHead(200, {
          "content-type": "text/html; charset=utf-8",
          "cache-control": "no-store",
          pragma: "no-cache",
        });
        response.end(html("Sigen authorization completed", true));
        complete.resolve(credentials);
      } catch (error) {
        response.writeHead(400, {
          "content-type": "text/html; charset=utf-8",
          "cache-control": "no-store",
        });
        response.end(html("Sigen authorization failed", false));
        if (!settled) complete.reject(error);
      }
    });
  });

  await new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(0, "127.0.0.1", resolve);
  });
  const address = server.address();
  const callbackUrl = `http://127.0.0.1:${address.port}${CALLBACK_PATH}`;
  loginEndpoint.searchParams.set("state", state);
  loginEndpoint.searchParams.set("client_name", clientName);
  loginEndpoint.searchParams.set("sigen-region", REGION);
  loginEndpoint.hash = new URLSearchParams({ redirect_uri: callbackUrl }).toString();

  const timer = setTimeout(() => {
    if (!settled) complete.reject(new Error("Sigen authorization timed out."));
  }, Math.max(5, timeoutSeconds) * 1000);

  try {
    const opened = noOpen ? false : await openExternalUrl(loginEndpoint.href);
    if (opened) {
      process.stdout.write("Sigen sign-in opened in your browser. Complete it there.\n");
    } else {
      process.stdout.write(`Open this Sigen sign-in URL in a browser:\n${loginEndpoint.href}\n`);
    }
    const credentials = await completed;
    process.stdout.write(
      credentials.username
        ? `Sigen authorization completed for ${credentials.username}.\n`
        : "Sigen authorization completed.\n",
    );
    return credentials;
  } finally {
    clearTimeout(timer);
    await new Promise((resolve) => server.close(resolve));
  }
}

async function decodeJsonResponse(response, label) {
  const text = await response.text();
  let parsed;
  try {
    parsed = JSON.parse(text);
  } catch {
    throw new Error(`${label} returned an invalid response.`);
  }
  return parsed;
}

async function publicJson(url, label) {
  const endpoint = validateEndpoint(url, label);
  const response = await fetch(endpoint, {
    headers: { accept: "application/json" },
    signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
  });
  const payload = await decodeJsonResponse(response, label);
  if (!response.ok) throw new Error(`${label} failed with HTTP ${response.status}.`);
  return payload;
}

async function readPublicCatalog() {
  return publicJson(CATALOG_URL, "Sigen public Tool Catalog");
}

async function readPublicTool(toolName) {
  const catalogEndpoint = validateEndpoint(CATALOG_URL, "Sigen public Tool Catalog URL");
  const pathname = catalogEndpoint.pathname.replace(/\/$/, "");
  catalogEndpoint.pathname = `${pathname}/tools/${encodeURIComponent(toolName)}`;
  catalogEndpoint.search = "";
  catalogEndpoint.hash = "";
  return publicJson(catalogEndpoint.href, "Sigen public Tool detail");
}

async function refreshCredentials(credentials) {
  if (!credentials?.refresh_token) throw new Error("No Sigen refresh token is available.");
  const endpoint = validateEndpoint(TOKEN_URL, "Sigen token URL");
  const body = new URLSearchParams({
    grant_type: "refresh_token",
    refresh_token: credentials.refresh_token,
  });
  const response = await fetch(endpoint, {
    method: "POST",
    headers: {
      accept: "application/json",
      authorization: PUBLIC_CLIENT_AUTHORIZATION,
      "content-type": "application/x-www-form-urlencoded",
      "sigen-region": credentials.region || REGION,
      "AUTH-CLIENT-ID": "sigen",
      "sg-pkg": "web",
    },
    body,
    signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
  });
  const payload = await decodeJsonResponse(response, "Sigen token refresh");
  const data = payload?.data || payload;
  if (!response.ok || payload?.code !== undefined && payload.code !== 0 || !data?.access_token) {
    throw new Error("Sigen refresh token is no longer valid.");
  }
  const updated = {
    ...credentials,
    access_token: data.access_token,
    refresh_token: data.refresh_token || credentials.refresh_token,
    expires_at: expiresAt(data.expires_in),
    username: data.username || credentials.username,
    saved_at: new Date().toISOString(),
  };
  await writeCredentials(updated);
  return updated;
}

async function usableCredentials() {
  let credentials = await readCredentials();
  if (!credentials) return login();
  const expiring = credentials.expires_at && credentials.expires_at <= Date.now() + 60_000;
  if (!expiring) return credentials;
  try {
    return await refreshCredentials(credentials);
  } catch {
    await deleteCredentials();
    return login();
  }
}

function parseSse(text) {
  const messages = [];
  for (const block of text.split(/\r?\n\r?\n/)) {
    const data = block
      .split(/\r?\n/)
      .filter((line) => line.startsWith("data:"))
      .map((line) => line.slice(5).trimStart())
      .join("\n");
    if (!data) continue;
    try {
      messages.push(JSON.parse(data));
    } catch {
      // Ignore non-JSON SSE events.
    }
  }
  return messages;
}

async function parseMcpResponse(response) {
  const text = await response.text();
  if (!text) return null;
  const contentType = response.headers.get("content-type") || "";
  if (contentType.includes("text/event-stream") || text.startsWith("event:")) {
    return parseSse(text).at(-1) || null;
  }
  try {
    return JSON.parse(text);
  } catch {
    throw new Error("Sigen MCP returned an invalid response.");
  }
}

async function mcpRequest(endpoint, credentials, payload, sessionId, protocolVersion) {
  const headers = {
    accept: "application/json, text/event-stream",
    authorization: `Bearer ${credentials.access_token}`,
    "content-type": "application/json",
    "x-sigen-region": credentials.region || REGION,
  };
  if (sessionId) headers["mcp-session-id"] = sessionId;
  if (protocolVersion) headers["mcp-protocol-version"] = protocolVersion;
  const response = await fetch(endpoint, {
    method: "POST",
    headers,
    body: JSON.stringify(payload),
    signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
  });
  if (response.status === 401 || response.status === 403) {
    const error = new Error("Sigen access token was rejected.");
    error.authorizationFailed = true;
    throw error;
  }
  if (!response.ok) throw new Error(`Sigen MCP request failed with HTTP ${response.status}.`);
  return {
    message: await parseMcpResponse(response),
    sessionId: response.headers.get("mcp-session-id") || sessionId,
  };
}

async function callMcp(credentials, method, params) {
  const endpoint = validateEndpoint(MCP_URL, "Sigen MCP URL", { allowKnownTestHttp: true });
  let requestId = 1;
  const initialized = await mcpRequest(
    endpoint,
    credentials,
    {
      jsonrpc: "2.0",
      id: requestId++,
      method: "initialize",
      params: {
        protocolVersion: "2025-03-26",
        capabilities: {},
        clientInfo: { name: "sigen-open-platform-skill", version: "1.0.0" },
      },
    },
  );
  if (initialized.message?.error) throw new Error(initialized.message.error.message || "MCP initialize failed.");
  const protocolVersion = initialized.message?.result?.protocolVersion || "2025-03-26";
  await mcpRequest(
    endpoint,
    credentials,
    { jsonrpc: "2.0", method: "notifications/initialized" },
    initialized.sessionId,
    protocolVersion,
  );
  const response = await mcpRequest(
    endpoint,
    credentials,
    { jsonrpc: "2.0", id: requestId, method, params },
    initialized.sessionId,
    protocolVersion,
  );
  if (response.message?.error) throw new Error(response.message.error.message || "MCP call failed.");
  return response.message?.result;
}

async function runMcp(method, params) {
  let credentials = await usableCredentials();
  try {
    return await callMcp(credentials, method, params);
  } catch (error) {
    if (!error?.authorizationFailed) throw error;
    try {
      credentials = await refreshCredentials(credentials);
    } catch {
      await deleteCredentials();
      credentials = await login();
    }
    return callMcp(credentials, method, params);
  }
}

function parseLoginOptions(args) {
  const options = {};
  for (let index = 0; index < args.length; index += 1) {
    const arg = args[index];
    if (arg === "--client-name") options.clientName = args[++index] || usage("Missing client name.");
    else if (arg === "--no-open") options.noOpen = true;
    else if (arg === "--timeout") {
      options.timeoutSeconds = Number(args[++index]);
      if (!Number.isFinite(options.timeoutSeconds)) usage("Invalid timeout.");
    } else usage(`Unknown login option: ${arg}`);
  }
  return options;
}

async function main() {
  const [command, ...args] = process.argv.slice(2);
  if (!command || command === "--help" || command === "-h") usage(undefined, command ? 0 : 2);

  if (command === "login") {
    await login(parseLoginOptions(args));
    return;
  }
  if (command === "status") {
    const credentials = await readCredentials();
    if (!credentials) {
      process.stdout.write("Sigen is not authorized.\n");
      process.exitCode = 1;
      return;
    }
    const suffix = credentials.username ? ` for ${credentials.username}` : "";
    process.stdout.write(`Sigen is authorized${suffix}.\n`);
    return;
  }
  if (command === "doctor") {
    const [major, minor] = process.versions.node.split(".").map(Number);
    const supportedNode = major > 18 || major === 18 && minor >= 17;
    validateEndpoint(LOGIN_URL, "Sigen login URL");
    validateEndpoint(MCP_URL, "Sigen MCP URL", { allowKnownTestHttp: true });
    validateEndpoint(TOKEN_URL, "Sigen token URL");
    validateEndpoint(CATALOG_URL, "Sigen public Tool Catalog URL");
    const credentials = await readCredentials();
    const catalog = await readPublicCatalog();
    process.stdout.write(`${JSON.stringify({
      ok: supportedNode,
      region: REGION,
      node_version: process.versions.node,
      minimum_node_version: "18.17.0",
      authorized: Boolean(credentials),
      catalog_version: catalog.catalog_version,
      tool_count: catalog.tool_count,
    }, null, 2)}\n`);
    if (!supportedNode) process.exitCode = 1;
    return;
  }
  if (command === "logout") {
    await deleteCredentials();
    process.stdout.write("Local Sigen authorization was removed.\n");
    return;
  }
  if (command === "refresh") {
    const credentials = await readCredentials();
    if (!credentials) fail("Sigen is not authorized. Run login first.");
    await refreshCredentials(credentials);
    process.stdout.write("Sigen access token was refreshed.\n");
    return;
  }
  if (command === "catalog") {
    const catalog = await readPublicCatalog();
    process.stdout.write(`${JSON.stringify(catalog, null, 2)}\n`);
    return;
  }
  if (command === "tool") {
    const [toolName, ...rest] = args;
    if (!toolName || rest.length) usage("Provide one tool name.");
    if (!/^[A-Za-z0-9_.-]{1,128}$/.test(toolName)) usage("Invalid tool name.");
    const detail = await readPublicTool(toolName);
    process.stdout.write(`${JSON.stringify(detail, null, 2)}\n`);
    return;
  }
  if (command === "tools") {
    const result = await runMcp("tools/list", {});
    process.stdout.write(`${JSON.stringify(result, null, 2)}\n`);
    return;
  }
  if (command === "call") {
    const [toolName, rawArguments = "{}", ...rest] = args;
    if (!toolName || rest.length) usage("Provide one tool name and optional JSON arguments.");
    if (!/^[A-Za-z0-9_.-]{1,128}$/.test(toolName)) usage("Invalid tool name.");
    let toolArguments;
    try {
      toolArguments = JSON.parse(rawArguments);
    } catch {
      usage("Tool arguments must be valid JSON.");
    }
    const result = await runMcp("tools/call", { name: toolName, arguments: toolArguments });
    process.stdout.write(`${JSON.stringify(result, null, 2)}\n`);
    return;
  }
  usage(`Unknown command: ${command}`);
}

main().catch((error) => {
  const message = error?.name === "TimeoutError" ? "The Sigen request timed out." : error?.message;
  fail(message || "The Sigen operation failed.");
});
