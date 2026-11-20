# Packaging

| | Packages (AUR, RPM, DEB) | Per user (`install-user.sh`) |
|---|---|---|
| Service | system service, user `seglcd`, system bus | `systemctl --user`, session bus |
| Config | `/etc/seglcd/config.toml`; D-Bus `SaveConfig`: `/var/lib/seglcd/config.toml` | `~/.config/seglcd/config.toml` |
| Device access | udev rule with group `seglcd` + `uaccess` | udev rule `udev/70-seglcd.rules` (`uaccess`) |

Use only one of them.

## Layout

| Path | Purpose |
|---|---|
| `install-user.sh`, `uninstall-user.sh` | per-user install/remove (`--help`) |
| `config.example.toml` | default configuration with all options |
| `systemd/{system,user}/seglcdd.service` | system unit (hardened, `User=seglcd`) / user unit |
| `dbus/system/`, `dbus/session/` | D-Bus activation; `system/…conf` is the bus policy (read: all, control: root + group `seglcd`) |
| `sysusers/seglcd.conf`, `udev/70-seglcd.rules` | system user and package udev rule |
| `arch/seglcd`, `arch/seglcd-git` | AUR recipes (release tag / git main) |
| `rpm/seglcd.spec`, `debian/` | RPM and Debian recipes |
| `make-tarball.sh` | source tarball from `HEAD`; `--vendor` bundles NuGet packages for offline builds |

Each recipe spells out its build (`dotnet publish` of `SegLcd.Tool` and `SegLcd.Daemon`, Native AOT) and every
installed file. `tests/SegLcd.Tests/PackagingTests.cs` checks that all recipes install the same files and versions.

## Build requirements

.NET 11 SDK, `clang`, zlib headers. Arch: `dotnet-sdk-bin` (AUR); Fedora: `dotnet-sdk-11.0`; Debian/Ubuntu: from
Microsoft's package feed. Runtime dependency: `hidapi`. NuGet needs network access unless the tarball was built with
`--vendor`.

## Building

```sh
# Arch
cd packaging/arch/seglcd && updpkgsums && makepkg -si        # seglcd-git needs no tag

# Fedora / openSUSE
spectool -g -R packaging/rpm/seglcd.spec && rpmbuild -ba packaging/rpm/seglcd.spec

# Debian / Ubuntu (source tree unpacked from the tarball as seglcd_<version>.orig.tar.gz)
cp -r packaging/debian debian && dpkg-buildpackage -us -uc
```

The service starts automatically when the display is present (udev `SYSTEMD_WANTS`; prevent with
`sudo systemctl mask seglcdd`). To control it via D-Bus: `sudo usermod -aG seglcd $USER`.
CPU package power (optional): install `/usr/share/doc/seglcd/examples/72-rapl-energy-read.rules` and add
`SupplementaryGroups=rapl` via `sudo systemctl edit seglcdd`.

## Release

Bump `<Version>` in `Directory.Build.props`, `pkgver` (PKGBUILD), `Version`/`%changelog` (spec) and
`debian/changelog`, commit to `main`, then tag and push:

```sh
git tag -a v<version> -F notes.md --cleanup=verbatim   # message = release notes (Markdown)
git push origin v<version>
```

The message of an annotated tag becomes the release notes (`--cleanup=verbatim` keeps `#` headings); a lightweight
tag (`git tag v<version>`) lists the commits since the previous tag instead.

The tag triggers `.github/workflows/release.yml`: it checks that the tag is on `main` and matches `<Version>`, runs
the tests, writes the release notes, builds the source tarball and the Arch, RPM and
Debian packages in containers (`.github/ci/build-*.sh`, x86_64), uploads them as build artifacts and creates a
**draft release** with changelog and packages. Publish the draft on GitHub after checking it.

## Testing

`tools/test-package.sh` installs a built DEB or RPM in a Debian 13 or Fedora container with systemd and checks user,
unit, service start, D-Bus policy, `SaveConfig`, reload and removal. It needs podman and runs without root:

```sh
tools/test-package.sh seglcd_0.1.0-1_amd64.deb
tools/test-package.sh seglcd-0.1.0-1.fc45.x86_64.rpm /dev/hidraw5   # also drive the display
```

With a device, stop the host service first. Inside the container `seglcd` gets your UID and reaches the display
through your `uaccess` ACL; udev does not run there, so the script starts the service itself. `--keep` leaves the
container running (`podman exec -it seglcd-deb-test bash`).

Tested on real hardware: AUR, DEB (Debian 13) and RPM (Fedora 45), including the system service.
