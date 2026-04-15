#!/usr/bin/env bash
set -euo pipefail

repo_root="${1:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}"
sample_url="${AJCAPTCHA_SAMPLE_URL:-http://127.0.0.1:18081}"
sample_log="$(mktemp)"

cleanup() {
  if [[ -n "${sample_pid:-}" ]]; then
    kill "${sample_pid}" >/dev/null 2>&1 || true
    wait "${sample_pid}" >/dev/null 2>&1 || true
  fi
  rm -f "${sample_log}"
}
trap cleanup EXIT

cd "${repo_root}"

dotnet --info
dotnet build AjCaptcha.sln
dotnet test AjCaptcha.sln

ASPNETCORE_URLS="${sample_url}" dotnet run --no-build --no-launch-profile --project samples/AjCaptcha.Sample/AjCaptcha.Sample.csproj >"${sample_log}" 2>&1 &
sample_pid=$!

for _ in $(seq 1 30); do
  if curl -fsS -X POST "${sample_url}/captcha/get" \
    -H "Content-Type: application/json" \
    -d '{"captchaType":"blockPuzzle","clientUid":"linux-smoke","ts":1234567890}' >/dev/null 2>/dev/null; then
    exit 0
  fi
  sleep 1
done

cat "${sample_log}"
exit 1
