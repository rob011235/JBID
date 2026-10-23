# Copyright (c) 2026 Robert Garner
# SPDX-License-Identifier: MIT
# Build the portable download before starting or publishing the web server.
param([switch]$NoRestore)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$packageOutput = Join-Path $repoRoot 'JBIDS.LocalHelper/bin/download-package'
$downloadOutput = Join-Path $repoRoot 'JBIDS.Server/wwwroot/downloads'
New-Item -ItemType Directory -Force $packageOutput, $downloadOutput | Out-Null
$publishArguments = @('publish', (Join-Path $repoRoot 'JBIDS.LocalHelper/JBIDS.LocalHelper.csproj'), '-c', 'Release', '--no-self-contained', '-p:UseAppHost=false', '-o', $packageOutput)
if ($NoRestore) { $publishArguments += '--no-restore' }
& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) { throw 'Local helper publish failed.' }
Copy-Item -LiteralPath (Join-Path $repoRoot 'JBIDS.LocalHelper/Extract.cmd') -Destination $packageOutput
Copy-Item -LiteralPath (Join-Path $repoRoot 'JBIDS.LocalHelper/Sign-in.cmd') -Destination $packageOutput
Copy-Item -LiteralPath (Join-Path $repoRoot 'JBIDS.LocalHelper/README.md') -Destination $packageOutput
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE.txt') -Destination $packageOutput
Compress-Archive -Path (Join-Path $packageOutput '*') -DestinationPath (Join-Path $downloadOutput 'JBIDS.LocalHelper.zip') -Force
Write-Output 'Download ready: /downloads/JBIDS.LocalHelper.zip'
