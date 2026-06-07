#!/usr/bin/env bash
set -euo pipefail

dir="${1:-sarif-results}"

if [ -z "$dir" ]; then
  echo "CodeQL SARIF directory input is empty." >&2
  exit 1
fi

if [ ! -d "$dir" ]; then
  echo "CodeQL SARIF directory not found: $dir" >&2
  exit 1
fi

shopt -s nullglob
sarif_files=("$dir"/*.sarif "$dir"/*.sarif.json)

if [ "${#sarif_files[@]}" -eq 0 ]; then
  echo "No CodeQL SARIF files found in directory: $dir" >&2
  exit 1
fi

if ! command -v jq >/dev/null 2>&1; then
  echo "jq is required to verify CodeQL SARIF results." >&2
  exit 1
fi

jq_filter='
  def valid_sarif:
    type == "object"
    and (.runs | type == "array")
    and (.runs | length > 0);

  def rule_level($run; $result):
    (
      if ($result.ruleIndex? | type) == "number" then
        $run.tool.driver.rules[$result.ruleIndex].defaultConfiguration.level?
      else
        null
      end
    ) // (
      ($run.tool.driver.rules // [])
      | map(select(.id == ($result.ruleId // "") or .name == ($result.ruleId // "")))
      | first
      | .defaultConfiguration.level?
    ) // "warning";

  def effective_level($run; $result):
    $result.level // rule_level($run; $result);

  if all(.[]; valid_sarif) then
    [
      .[]
      | .runs[]? as $run
      | ($run.results // [])[]? as $result
      | effective_level($run; $result)
      | select(. != "note" and . != "none")
    ]
    | length
  else
    error("one or more SARIF files do not contain a non-empty runs array")
  end
'

if ! count="$(jq -s "$jq_filter" "${sarif_files[@]}" 2>&1)"; then
  echo "Failed to parse CodeQL SARIF results: $count" >&2
  exit 1
fi

if ! [[ "$count" =~ ^[0-9]+$ ]]; then
  echo "Failed to parse CodeQL SARIF results: expected numeric result count, got '$count'." >&2
  exit 1
fi

if [ "$count" -gt 0 ]; then
  echo "CodeQL analysis detected $count blocking result(s). Build aborted." >&2
  exit 1
fi

echo "Verified ${#sarif_files[@]} CodeQL SARIF file(s): no blocking results."
