import { platform } from "node:os";
import { spawn } from "node:child_process";

const DEFAULT_LAUNCH_TIMEOUT_MS = 10_000;

export function browserCommand(url, operatingSystem = platform()) {
  if (operatingSystem === "darwin") return ["/usr/bin/open", [url]];
  if (operatingSystem === "win32") {
    // Do not pass authorization URLs through cmd.exe: its command parser treats
    // '&' in the query string as a command separator and truncates the URL.
    return ["rundll32.exe", ["url.dll,FileProtocolHandler", url]];
  }
  if (operatingSystem === "linux") return ["xdg-open", [url]];
  return null;
}

export function openExternalUrl(
  url,
  {
    operatingSystem = platform(),
    spawnImpl = spawn,
    timeoutMs = DEFAULT_LAUNCH_TIMEOUT_MS,
  } = {},
) {
  const command = browserCommand(url, operatingSystem);
  if (!command) return Promise.resolve(false);

  const [file, args] = command;
  const launchTimeoutMs = Number.isFinite(timeoutMs) && timeoutMs > 0
    ? timeoutMs
    : DEFAULT_LAUNCH_TIMEOUT_MS;

  return new Promise((resolve) => {
    let child;
    let settled = false;
    let started = false;
    let timer;
    const finish = (opened) => {
      if (settled) return;
      settled = true;
      if (timer) clearTimeout(timer);
      resolve(opened);
    };

    try {
      child = spawnImpl(file, args, {
        shell: false,
        stdio: "ignore",
        windowsHide: true,
      });
    } catch {
      finish(false);
      return;
    }

    child.once("spawn", () => {
      started = true;
    });
    child.once("error", () => finish(false));
    child.once("close", (code) => finish(started && code === 0));
    timer = setTimeout(() => {
      if (typeof child.unref === "function") child.unref();
      finish(false);
    }, launchTimeoutMs);
  });
}
