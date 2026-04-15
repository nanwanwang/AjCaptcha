param(
    [string]$Distro = "Ubuntu-24.04",
    [string]$Connection = "localhost:6379"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$solution = Join-Path $repoRoot "AjCaptcha.sln"

wsl.exe -u root -d $Distro -- bash -lc "redis-cli ping >/dev/null 2>&1 || (redis-server --daemonize yes && sleep 1 && redis-cli ping)"

$env:AJCAPTCHA_REDIS_TEST_CONNECTION = $Connection
dotnet test $solution --filter "Category=Redis"
