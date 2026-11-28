## Linux Packaging (Flatpak)

Build on Linux or in a WSL2 Linux distribution with the **Linux .NET 10 SDK** and Flatpak tooling installed. On Ubuntu or Debian:

```sh
sudo apt install flatpak flatpak-builder
flatpak remote-add --user --if-not-exists flathub https://dl.flathub.org/repo/flathub.flatpakrepo
flatpak install --user flathub org.freedesktop.Platform//25.08 org.freedesktop.Sdk//25.08
```

From the repository root in Linux or WSL2, build and install the bundle:

```sh
bash packaging/flatpak/build.sh
flatpak install --user "./artifacts/flatpak/SigCompanion-$(flatpak --default-arch).flatpak"
flatpak run com.thirtyhippos.sigcompanion
```

The build produces a self-contained `.flatpak` file for the build machine's architecture (`x86_64` or `aarch64`). Upload that file to a release to distribute it directly; this is not a Flathub submission and does not provide automatic updates. Recipients need Flatpak and `org.freedesktop.Platform//25.08` from Flathub, but not the .NET SDK, Flatpak Builder, or the Freedesktop SDK.

Running the app requires an X11 or XWayland session (WSLg for WSL2) and D-Bus. On WSL2, enable systemd if the distro has no system D-Bus socket at `/run/dbus/system_bus_socket`. If only the session bus is missing, launch with `dbus-run-session -- flatpak run com.thirtyhippos.sigcompanion`. To smoke-test the dashboard without Sigenergy credentials, sign in as `demo` with a nonempty placeholder password. Testing local Modbus requires network access to the inverter from the Linux environment.

