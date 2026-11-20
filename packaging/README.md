# Packaging

| | Packages (AUR, RPM, DEB) | Per user (`install-user.sh`) |
|---|---|---|
| Service | system service, user `seglcd`, system bus | `systemctl --user`, session bus |
| Config | `/etc/seglcd/config.toml` | `~/.config/seglcd/config.toml` |
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

Then: `sudo systemctl enable --now seglcdd`; to control it via D-Bus `sudo usermod -aG seglcd $USER`.
CPU package power (optional): install `/usr/share/doc/seglcd/examples/72-rapl-energy-read.rules` and add
`SupplementaryGroups=rapl` via `sudo systemctl edit seglcdd`.

## Release

Bump `<Version>` in `Directory.Build.props`, `pkgver` (PKGBUILD), `Version`/`%changelog` (spec) and
`debian/changelog`, then `git tag v<version> && git push origin v<version>`.

## Tested

- AUR: `makepkg` incl. `check()`; `namcap` only notes the run-time-loaded hidapi.
- RPM: Fedora 45 container incl. `%check`; `rpmlint` clean; install creates the user.
- DEB: Debian 13 container (.NET via `dotnet-install.sh`); `lintian` only AppStream hints; install creates the user
  and enables the unit.
- Not yet: the installed system service on real hardware.
