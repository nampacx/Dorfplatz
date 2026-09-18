#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SKILL_ROOT="$(dirname "$SCRIPT_DIR")"
PYTHON_BIN="${PYTHON:-python3}"
VENV_PATH="${1:-$SKILL_ROOT/.venv}"

command -v "$PYTHON_BIN" >/dev/null 2>&1 || {
  echo "Python executable not found: $PYTHON_BIN" >&2
  exit 1
}

echo "Creating Python environment at $VENV_PATH"
"$PYTHON_BIN" -m venv "$VENV_PATH"
if [ -x "$VENV_PATH/bin/python" ]; then
  VENV_PYTHON="$VENV_PATH/bin/python"
elif [ -x "$VENV_PATH/Scripts/python.exe" ]; then
  VENV_PYTHON="$VENV_PATH/Scripts/python.exe"
else
  echo "Virtual-environment Python was not created in bin/ or Scripts/." >&2
  exit 1
fi

"$VENV_PYTHON" -m pip install --upgrade pip
"$VENV_PYTHON" -m pip install -r "$SCRIPT_DIR/requirements.txt"

echo "Environment ready."
printf "Run: %q %q\n" "$VENV_PYTHON" "$SCRIPT_DIR/analyze_reference_pdf.py"