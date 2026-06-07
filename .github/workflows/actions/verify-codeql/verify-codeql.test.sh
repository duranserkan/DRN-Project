#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
verifier="$script_dir/verify-codeql.sh"
tmp_root="$(mktemp -d)"
trap 'rm -rf "$tmp_root"' EXIT

fail() {
  echo "FAIL: $*" >&2
  exit 1
}

assert_success() {
  local name="$1"
  shift
  local output

  if ! output="$("$@" 2>&1)"; then
    echo "$output"
    fail "$name expected success"
  fi

  echo "ok - $name"
}

assert_failure_contains() {
  local name="$1"
  local expected="$2"
  shift 2
  local output
  local status

  set +e
  output="$("$@" 2>&1)"
  status=$?
  set -e

  if [ "$status" -eq 0 ]; then
    echo "$output"
    fail "$name expected failure"
  fi

  if [[ "$output" != *"$expected"* ]]; then
    echo "$output"
    fail "$name expected output to contain: $expected"
  fi

  echo "ok - $name"
}

make_dir() {
  local name="$1"
  mkdir -p "$tmp_root/$name"
  echo "$tmp_root/$name"
}

write_sarif() {
  local file="$1"
  local rule_level="$2"
  local result_level="${3:-}"
  local result_level_property=""

  if [ -n "$result_level" ]; then
    result_level_property=$'\n          "level": "'"$result_level"$'",'
  fi

  cat > "$file" <<EOF
{
  "\$schema": "https://json.schemastore.org/sarif-2.1.0.json",
  "version": "2.1.0",
  "runs": [
    {
      "tool": {
        "driver": {
          "name": "CodeQL",
          "rules": [
            {
              "id": "test/rule",
              "name": "test/rule",
              "defaultConfiguration": { "level": "$rule_level" }
            }
          ]
        }
      },
      "results": [
        {
          "ruleId": "test/rule",
          "ruleIndex": 0,${result_level_property}
          "message": { "text": "test result" },
          "locations": [
            {
              "physicalLocation": {
                "artifactLocation": { "uri": "src/Test.cs" },
                "region": { "startLine": 1 }
              }
            }
          ]
        }
      ]
    }
  ]
}
EOF
}

missing_dir="$tmp_root/missing"
empty_dir="$(make_dir empty)"
notes_dir="$(make_dir notes)"
alerts_dir="$(make_dir alerts)"
json_ext_dir="$(make_dir json-ext)"
override_dir="$(make_dir override)"
malformed_dir="$(make_dir malformed)"

write_sarif "$notes_dir/note.sarif" "note"
write_sarif "$alerts_dir/warning.sarif" "warning"
write_sarif "$json_ext_dir/error.sarif.json" "error"
write_sarif "$override_dir/result-note.sarif" "warning" "note"
printf '{bad json' > "$malformed_dir/bad.sarif"

assert_failure_contains "missing SARIF directory fails closed" "CodeQL SARIF directory not found" bash "$verifier" "$missing_dir"
assert_failure_contains "empty SARIF directory fails closed" "No CodeQL SARIF files found" bash "$verifier" "$empty_dir"
assert_success "note-only SARIF passes" bash "$verifier" "$notes_dir"
assert_failure_contains "warning SARIF fails" "CodeQL analysis detected 1 blocking result(s)" bash "$verifier" "$alerts_dir"
assert_failure_contains "sarif.json extension is verified" "CodeQL analysis detected 1 blocking result(s)" bash "$verifier" "$json_ext_dir"
assert_success "result-level note overrides warning rule default" bash "$verifier" "$override_dir"
assert_failure_contains "malformed SARIF fails closed" "Failed to parse CodeQL SARIF results" bash "$verifier" "$malformed_dir"
