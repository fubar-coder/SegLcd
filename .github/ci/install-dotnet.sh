#!/usr/bin/env bash
# Installs the .NET 11 SDK to /opt/dotnet (for distributions without a .NET 11 package).
set -euo pipefail
curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
bash /tmp/dotnet-install.sh --channel 11.0 --install-dir /opt/dotnet --no-path
ln -sf /opt/dotnet/dotnet /usr/local/bin/dotnet
dotnet --version
