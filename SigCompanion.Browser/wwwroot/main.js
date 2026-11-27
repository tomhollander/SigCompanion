import { dotnet } from './_framework/dotnet.js'

const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error(`Expected to be running in a browser`);

const dotnetRuntime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

const config = dotnetRuntime.getConfig();

dotnetRuntime.setModuleImports("batteryAudio", {
    async playBatterySound(wav) {
        const url = URL.createObjectURL(new Blob([wav], { type: "audio/wav" }));
        const audio = new Audio(url);
        const cleanup = () => URL.revokeObjectURL(url);
        audio.addEventListener("ended", cleanup, { once: true });
        audio.addEventListener("error", cleanup, { once: true });
        try {
            await audio.play();
        } catch (error) {
            cleanup();
            throw error;
        }
    }
});

await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);
