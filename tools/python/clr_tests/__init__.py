"""Package initializer for Python CLR integration tests.

This module runs early when pytest imports test modules as the `tests` package.
We use it to import pythonnet (clr) early and to ensure the native .NET
probes (WPF bin folder) are on PATH so subsequent `from System import ...`
imports succeed during test collection.
"""

from __future__ import annotations

import os
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[3]
PYTHON_ROOT = REPO_ROOT / "tools" / "python"
WPF_BIN_DIR = REPO_ROOT / "bin" / "Debug" / "net9.0-windows"

# Ensure the WPF bin folder is discoverable by the CLR/runtime for native
# probing. Do this early so imports like `from System import Array` succeed.
existing_path = os.environ.get("PATH", "")
probe = str(WPF_BIN_DIR)
if probe and probe not in existing_path:
    os.environ["PATH"] = f"{probe};{existing_path}" if existing_path else probe

try:
    # Import pythonnet early so CLR import hooks are registered before test
    # modules are imported. If it isn't installed, we don't raise here — the
    # tests or conftest will skip appropriately.
    import clr  # type: ignore
except Exception:
    # leave it silent; conftest contains guards that will skip tests when
    # pythonnet is unavailable.
    pass

else:
    # Try to pre-load commonly-used framework assemblies so module-level
    # imports like `from System.Net.Http import ...` succeed during pytest
    # collection. These are best-effort and will not raise if unavailable.
    try:
        # core framework assemblies
        for asm in ("System", "System.Core", "System.Runtime", "System.Net.Http"):
            try:
                clr.AddReference(asm)
            except Exception:
                # ignore if runtime cannot resolve by name
                continue

        # If the repo supplies any third-party DLLs (e.g., Prism) into the
        # tests/assemblies folder, try to add them by path so imports like
        # `from Prism.Regions import Region` succeed.
        _assemblies_dir = Path(__file__).parent / "assemblies"
        if _assemblies_dir.exists():
            for dll in _assemblies_dir.glob("*.dll"):
                try:
                    clr.AddReference(str(dll))
                except Exception:
                    # Best-effort only; don't fail collection here.
                    continue
    except Exception:
        # Keep __init__ robust; don't let AddReference attempts break test
        # collection in environments that differ from the developer machine.
        pass
