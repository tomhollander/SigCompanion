#!/usr/bin/env bash
set -euo pipefail

app_id=com.thirtyhippos.sigcompanion
runtime_branch=25.08
runtime_repo=https://dl.flathub.org/repo/flathub.flatpakrepo
script_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
repo_dir=$(cd -- "$script_dir/../.." && pwd)

for tool in dotnet flatpak flatpak-builder; do
	if ! command -v "$tool" >/dev/null; then
		printf 'Missing required command: %s\n' "$tool" >&2
		exit 1
	fi
done

arch=$(flatpak --default-arch)
case "$arch" in
	x86_64) rid=linux-x64 ;;
	aarch64) rid=linux-arm64 ;;
	*) printf 'Unsupported Flatpak architecture: %s\n' "$arch" >&2; exit 1 ;;
esac

for runtime in org.freedesktop.Platform org.freedesktop.Sdk; do
	if ! flatpak info --arch="$arch" "$runtime//$runtime_branch" >/dev/null 2>&1; then
		printf 'Missing %s//%s for %s. Install both runtimes as described in README.md.\n' \
			"$runtime" "$runtime_branch" "$arch" >&2
		exit 1
	fi
done

# Keep the publish and OSTree staging on the Linux filesystem under WSL.
work_dir=$(mktemp -d "${TMPDIR:-/tmp}/sigcompanion-flatpak.XXXXXXXX")
trap 'rm -rf -- "$work_dir"' EXIT

dotnet publish "$repo_dir/SigCompanion.Desktop/SigCompanion.Desktop.csproj" \
	--configuration Release --runtime "$rid" --self-contained true \
	--artifacts-path "$work_dir/dotnet" --output "$work_dir/publish"

cp -- "$script_dir/$app_id.yml" "$script_dir/$app_id.desktop" \
	"$script_dir/$app_id.metainfo.xml" "$repo_dir/LICENSE.txt" "$work_dir/"
cp -- "$repo_dir/WindowsPackaging/Images/Square44x44Logo.altform-unplated_targetsize-256.png" \
	"$work_dir/icon.png"

bundle="SigCompanion-$arch.flatpak"
(
	cd -- "$work_dir"
	flatpak-builder --arch="$arch" --disable-download --disable-rofiles-fuse --repo=repo build "$app_id.yml"
	flatpak build-bundle --arch="$arch" --runtime-repo="$runtime_repo" repo "$bundle" "$app_id"
)

output_dir="$repo_dir/artifacts/flatpak"
mkdir -p -- "$output_dir"
install -m 644 -- "$work_dir/$bundle" "$output_dir/$bundle"
printf 'Flatpak bundle: %s\n' "$output_dir/$bundle"