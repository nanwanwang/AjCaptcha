param(
    [string]$Distro = "Ubuntu-24.04"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$windowsRepoRoot = $repoRoot.Path
$drive = $windowsRepoRoot.Substring(0, 1).ToLowerInvariant()
$rest = $windowsRepoRoot.Substring(2).Replace('\', '/')
$linuxRepoRoot = "/mnt/$drive$rest"
wsl.exe -d $Distro -- bash -lc "chmod +x '$linuxRepoRoot/scripts/verify-linux.sh' && '$linuxRepoRoot/scripts/verify-linux.sh' '$linuxRepoRoot'"
