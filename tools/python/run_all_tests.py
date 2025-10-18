from __future__ import annotations

import os
from pathlib import Path

import pytest


def main() -> int:
    project_dir = Path(__file__).resolve().parent
    os.chdir(project_dir)
    return pytest.main(["--cov", "-v"])


if __name__ == "__main__":
    raise SystemExit(main())
