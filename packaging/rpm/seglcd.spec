# RPM spec for SegLcd (Fedora / openSUSE / RHEL-like).
# Build:  rpmbuild -ba packaging/rpm/seglcd.spec   (Source0 in ~/rpmbuild/SOURCES, see packaging/README.md)
# Needs a .NET 11 SDK (package dotnet-sdk-11.0, e.g. from Microsoft's package feed).

%global debug_package %{nil}

Name:           seglcd
Version:        0.2.0
Release:        1%{?dist}
Summary:        CLI and D-Bus service for the Thermaltake LB360-S segment LCD
License:        GPL-3.0-or-later
URL:            https://github.com/fubar-coder/SegLcd
Source0:        %{url}/archive/refs/tags/v%{version}/SegLcd-%{version}.tar.gz

ExclusiveArch:  x86_64 aarch64
BuildRequires:  dotnet-sdk-11.0
BuildRequires:  clang
BuildRequires:  zlib-devel
BuildRequires:  systemd-rpm-macros
BuildRequires:  desktop-file-utils
BuildRequires:  libappstream-glib
# hidapi is loaded at run time (dlopen of libhidapi-hidraw.so.0).
Requires:       hidapi
# polkit authorizes changes over D-Bus (packaging/polkit).
Requires:       polkit
%{?sysusers_requires_compat}

%description
SegLcd drives the segment LCD on the pump block of the Thermaltake
LB360-S ARGB Sync AIO cooler (USB 5131:200e) on Linux. seglcd is a
command line tool for testing and analysis; seglcdd is a system service
that shows the CPU and GPU temperature live on the display and offers a
D-Bus interface on the system bus.

SegLcd is not affiliated with Thermaltake.

%package gtk
Summary:        GTK settings app for the SegLcd service
Requires:       %{name} = %{version}-%{release}
# GTK 4 and libadwaita (>= 1.6 for Adw.Spinner) are loaded at run time through Gir.Core.
Requires:       gtk4
Requires:       libadwaita >= 1.6

%description gtk
seglcd-gtk shows the state of the SegLcd service and the current sensor
values, and sets the intervals and the sensor of each value over D-Bus.

%prep
%autosetup -n SegLcd-%{version}

%ifarch x86_64
%global dotnet_rid linux-x64
%endif
%ifarch aarch64
%global dotnet_rid linux-arm64
%endif

%build
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
# NuGet packages: bundled in vendor/nuget (packaging/make-tarball.sh --vendor) for offline builds, else downloaded.
if [ -d vendor/nuget ]; then export NUGET_PACKAGES="$PWD/vendor/nuget"; else export NUGET_PACKAGES="%{_builddir}/nuget"; fi
for project in SegLcd.Tool SegLcd.Daemon SegLcd.Gtk; do
    dotnet publish "src/$project" -c Release -r %{dotnet_rid} \
        -p:Version=%{version} -p:ContinuousIntegrationBuild=true -o "build/$project"
done

%install
install -Dm755 build/SegLcd.Tool/seglcd   %{buildroot}%{_bindir}/seglcd
install -Dm755 build/SegLcd.Daemon/seglcdd %{buildroot}%{_bindir}/seglcdd
install -Dm644 packaging/systemd/system/seglcdd.service %{buildroot}%{_unitdir}/seglcdd.service
install -Dm644 packaging/sysusers/seglcd.conf %{buildroot}%{_sysusersdir}/seglcd.conf
install -Dm644 packaging/udev/70-seglcd.rules %{buildroot}%{_udevrulesdir}/70-seglcd.rules
install -Dm644 packaging/dbus/system/io.github.fubar_coder.SegLcd1.conf \
    %{buildroot}%{_datadir}/dbus-1/system.d/io.github.fubar_coder.SegLcd1.conf
install -Dm644 packaging/dbus/system/io.github.fubar_coder.SegLcd1.service \
    %{buildroot}%{_datadir}/dbus-1/system-services/io.github.fubar_coder.SegLcd1.service
install -Dm644 packaging/polkit/io.github.fubar_coder.SegLcd1.policy \
    %{buildroot}%{_datadir}/polkit-1/actions/io.github.fubar_coder.SegLcd1.policy
install -Dm644 packaging/polkit/50-seglcd.rules %{buildroot}%{_datadir}/polkit-1/rules.d/50-seglcd.rules
install -Dm644 packaging/config.example.toml %{buildroot}%{_sysconfdir}/seglcd/config.toml
for page in man/man1/seglcd.1 man/man1/seglcdd.1 man/man5/seglcdd.toml.5 \
            man/de/man1/seglcd.1 man/de/man1/seglcdd.1 man/de/man5/seglcdd.toml.5; do
    install -Dm644 "$page" "%{buildroot}%{_datadir}/$page"
done
install -Dm644 README.md README.de.md packaging/config.example.toml -t %{buildroot}%{_docdir}/%{name}
install -Dm644 udev/72-rapl-energy-read.rules udev/rapl.sysusers.conf -t %{buildroot}%{_docdir}/%{name}/examples
install -Dm755 build/SegLcd.Gtk/seglcd-gtk %{buildroot}%{_bindir}/seglcd-gtk
install -Dm644 data/io.github.fubar_coder.SegLcd.desktop \
    %{buildroot}%{_datadir}/applications/io.github.fubar_coder.SegLcd.desktop
install -Dm644 data/io.github.fubar_coder.SegLcd.metainfo.xml \
    %{buildroot}%{_datadir}/metainfo/io.github.fubar_coder.SegLcd.metainfo.xml
install -Dm644 data/icons/hicolor/scalable/apps/io.github.fubar_coder.SegLcd.svg \
    %{buildroot}%{_datadir}/icons/hicolor/scalable/apps/io.github.fubar_coder.SegLcd.svg
install -Dm644 man/man1/seglcd-gtk.1 %{buildroot}%{_mandir}/man1/seglcd-gtk.1
install -Dm644 man/de/man1/seglcd-gtk.1 %{buildroot}%{_mandir}/de/man1/seglcd-gtk.1

%check
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
if [ -d vendor/nuget ]; then export NUGET_PACKAGES="$PWD/vendor/nuget"; else export NUGET_PACKAGES="%{_builddir}/nuget"; fi
dotnet test SegLcd.slnx
desktop-file-validate %{buildroot}%{_datadir}/applications/io.github.fubar_coder.SegLcd.desktop
appstream-util validate-relax --nonet %{buildroot}%{_datadir}/metainfo/io.github.fubar_coder.SegLcd.metainfo.xml

%pre
# Empty on distributions where rpm creates sysusers.d users itself (Fedora >= 42); needed on older ones.
%sysusers_create_compat packaging/sysusers/seglcd.conf

%post
%systemd_post seglcdd.service
%udev_rules_update

%preun
%systemd_preun seglcdd.service

%postun
%systemd_postun_with_restart seglcdd.service
%udev_rules_update

%files
%license LICENSE
%doc %{_docdir}/%{name}/
%{_bindir}/seglcd
%{_bindir}/seglcdd
%dir %{_sysconfdir}/seglcd
%config(noreplace) %{_sysconfdir}/seglcd/config.toml
%{_unitdir}/seglcdd.service
%{_sysusersdir}/seglcd.conf
%{_udevrulesdir}/70-seglcd.rules
%{_datadir}/dbus-1/system.d/io.github.fubar_coder.SegLcd1.conf
%{_datadir}/dbus-1/system-services/io.github.fubar_coder.SegLcd1.service
%{_datadir}/polkit-1/actions/io.github.fubar_coder.SegLcd1.policy
%{_datadir}/polkit-1/rules.d/50-seglcd.rules
%{_mandir}/man1/seglcd.1*
%{_mandir}/man1/seglcdd.1*
%{_mandir}/man5/seglcdd.toml.5*
%lang(de) %{_mandir}/de/man1/seglcd.1*
%lang(de) %{_mandir}/de/man1/seglcdd.1*
%lang(de) %{_mandir}/de/man5/seglcdd.toml.5*

%files gtk
%license LICENSE
%{_bindir}/seglcd-gtk
%{_datadir}/applications/io.github.fubar_coder.SegLcd.desktop
%{_datadir}/metainfo/io.github.fubar_coder.SegLcd.metainfo.xml
%{_datadir}/icons/hicolor/scalable/apps/io.github.fubar_coder.SegLcd.svg
%{_mandir}/man1/seglcd-gtk.1*
%lang(de) %{_mandir}/de/man1/seglcd-gtk.1*

%changelog
* Fri Nov 20 2026 Mark Junker <fubar-coder@live.de> - 0.2.0-1
- Settings app seglcd-gtk (subpackage)
- polkit authorization: the active local user may change settings
- D-Bus: ListSensors, SensorSpecs, CpuClockMode

* Fri Nov 20 2026 Mark Junker <fubar-coder@live.de> - 0.1.0-1
- Initial package
