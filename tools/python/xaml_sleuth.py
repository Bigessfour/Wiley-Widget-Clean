"""WPF Whisperer: combined static XAML and runtime UI inspection helper.

This module provides the ``XamlSleuth`` class as a lightweight alternative to
Snoop for two common debugging workflows that pop up while working with WPF:

1. Static analysis of XAML files to detect binding mistakes, missing
   namespaces, and other subtle issues before running the application.
2. Runtime inspection of a live WPF window using UI Automation to highlight
   suspicious controls (for example empty text blocks that appear to be bound).

The script can be executed directly. Use ``python xaml_sleuth.py --help`` for
usage details.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections.abc import Iterable, Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import TYPE_CHECKING, Any, cast

try:
    from lxml import etree as _etree  # type: ignore[import]
except ImportError:  # pragma: no cover - optional dependency may be absent.
    _etree = None  # type: ignore[assignment]

if TYPE_CHECKING:
    from lxml import etree as _etree_module  # type: ignore[import]
else:
    _etree_module = _etree

etree = cast(Any, _etree_module)

try:
    import uiautomation as automation
except ImportError:  # pragma: no cover - runtime dependency may be absent.
    automation = None  # type: ignore[assignment]

BINDING_PATTERN = re.compile(r"\{\s*Binding(?P<body>[^}]*)\}", re.IGNORECASE)
PATH_PATTERN = re.compile(r"Path\s*=\s*(?P<path>[^,]+)")
SIMPLE_PATH_PATTERN = re.compile(r"^\s*(?P<path>[^,]+?)\s*(,|$)")

DEFAULT_MOCK_DATA: dict[str, Any] = {
    "ProcessName": "python.exe",
    "ProcesName": None,
    "WindowTitle": "Wiley Widget",
    "UserName": "wpf-whisperer",
}

TEXTUAL_CONTROL_TYPES = {"TextControl", "EditControl", "DocumentControl"}


@dataclass
class Issue:
    """Represents a potential problem discovered during a run."""

    location: str
    message: str
    severity: str = "warning"

    def format(self) -> str:
        emoji = {
            "error": "💥",
            "warning": "⚠️",
            "info": "ℹ️",
        }.get(self.severity.lower(), "⚠️")
        return f"{emoji} {self.location}: {self.message}"


class XamlSleuth:
    """Encapsulates static and runtime inspection helpers for WPF projects."""

    def __init__(
        self,
        *,
        xaml_path: Path | None = None,
        runtime_target: Path | None = None,
        mock_data: dict[str, Any] | None = None,
        report_path: Path | None = None,
        verbose: bool = False,
    ) -> None:
        self.xaml_path = xaml_path
        self.runtime_target = runtime_target
        self.report_path = report_path
        self.verbose = verbose
        self.mock_data = {**DEFAULT_MOCK_DATA, **(mock_data or {})}
        self._xml_parser = None
        if etree is not None:
            self._xml_parser = etree.XMLParser(
                remove_blank_text=True, resolve_entities=False, recover=True
            )

    def _default_window_title(self):
        """Generate a default window title based on the runtime target."""
        if hasattr(self, 'runtime_target') and self.runtime_target:
            return self.runtime_target.stem
        return ""

    # ------------------------------------------------------------------
    # Static analysis
    # ------------------------------------------------------------------
    def run_static_analysis(self) -> list[Issue]:
        if etree is None:
            raise RuntimeError(
                "Static analysis requires the 'lxml' package. "
                "Install it via 'pip install lxml'."
            )

        if self.xaml_path is None:
            raise ValueError("Static analysis requires a XAML file path.")
        if not self.xaml_path.exists():
            raise FileNotFoundError(self.xaml_path)
        if etree is None:
            raise RuntimeError(
                "Static analysis requires the 'lxml' package. Install dependencies via "
                "pip install -r tools/python/requirements-xaml_sleuth.txt."
            )

        if self.verbose:
            print(f"🕵️ Parsing XAML: {self.xaml_path}")

        try:
            tree = etree.parse(str(self.xaml_path), self._xml_parser)
        except etree.XMLSyntaxError as exc:  # pragma: no cover - direct user feedback.
            return [
                Issue(
                    location=str(self.xaml_path),
                    message=f"XAML syntax error: {exc}",
                    severity="error",
                )
            ]

        root = tree.getroot()
        issues: list[Issue] = []
        issues.extend(self._validate_root_namespaces(root))
        issues.extend(self._walk_static_tree(root, path=[self._tag_name(root)]))
        return issues

    def _validate_root_namespaces(self, element: Any) -> list[Issue]:
        issues: list[Issue] = []
        nsmap = element.nsmap or {}
        default_namespace_present = any(key in (None, "") for key in nsmap)
        if not default_namespace_present:
            issues.append(
                Issue(
                    location=self._tag_name(element),
                    message=(
                        "Root element is missing the default WPF namespace "
                        "(xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\")."
                    ),
                    severity="warning",
                )
            )
        if "x" not in nsmap:
            issues.append(
                Issue(
                    location=self._tag_name(element),
                    message=(
                        "Root element is missing the x namespace "
                        "(xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\")."
                    ),
                    severity="warning",
                )
            )
        return issues

    def _walk_static_tree(
        self,
    element: Any,
        *,
        path: Sequence[str],
        depth: int = 0,
        issues: list[Issue] | None = None,
    ) -> list[Issue]:
        if issues is None:
            issues = []
        if not self._is_element_node(element):
            return issues or []

        element_path = " > ".join(path)

        for attr_name, attr_value in element.attrib.items():
            # Detect undefined namespace prefixes.
            if attr_name.startswith("{"):
                qname = etree.QName(attr_name)
                if qname.namespace is None:
                    issues.append(
                        Issue(
                            location=f"{element_path} [{attr_name}]",
                            message="Attribute uses an undefined namespace prefix.",
                        )
                    )
            if "Binding" in attr_value:
                issues.extend(self._inspect_binding(attr_value, element_path))

        text_payload = (element.text or "").strip()
        if text_payload.startswith("{") and "Binding" in text_payload:
            issues.extend(self._inspect_binding(text_payload, element_path))

        for idx, child in enumerate(element):
            child_tag = self._tag_name(child)
            child_path = [*path, f"{child_tag}[{idx}]"]
            self._walk_static_tree(child, path=child_path, depth=depth + 1, issues=issues)
        return issues

    def _inspect_binding(self, raw_binding: str, location: str) -> list[Issue]:
        matches: list[Issue] = []
        binding_match = BINDING_PATTERN.search(raw_binding)
        if not binding_match:
            return matches

        body = binding_match.group("body")
        path_value = self._extract_path(body)
        if path_value is None:
            matches.append(
                Issue(
                    location=location,
                    message=f"Binding expression '{raw_binding}' does not expose a Path."
                    " Consider adding Path=... for clarity.",
                    severity="info",
                )
            )
            return matches

        if path_value not in self.mock_data:
            matches.append(
                Issue(
                    location=location,
                    message=(
                        f"Binding path '{path_value}' not found in mock data. "
                        "Possible typo or missing property."
                    ),
                )
            )
        elif self.mock_data[path_value] is None:
            matches.append(
                Issue(
                    location=location,
                    message=(
                        f"Binding path '{path_value}' resolves to None in mock data. "
                        "Check data context initialization."
                    ),
                    severity="info",
                )
            )
        elif self.verbose:
            print(f"✅ Binding '{path_value}' resolved via mock data at {location}")
        return matches

    @staticmethod
    def _extract_path(binding_body: str) -> str | None:
        # First try explicit Path= syntax
        explicit_match = PATH_PATTERN.search(binding_body)
        if explicit_match:
            return explicit_match.group("path").strip()

        # For simple cases, check if it looks like a property path (not a binding parameter)
        # Property paths typically don't contain '=' and are not binding keywords
        binding_keywords = {'mode', 'converter', 'relativesource', 'elementname', 'source', 'xpath', 'stringformat', 'converterparameter', 'bindinggroupname', 'binding', 'multibinding', 'prioritybinding'}

        simple_match = SIMPLE_PATH_PATTERN.match(binding_body)
        if simple_match:
            candidate_path = simple_match.group("path").strip().lower()
            # Don't treat binding parameters as paths
            if '=' in candidate_path or candidate_path in binding_keywords:
                return None
            return simple_match.group("path").strip()
        return None

    # ------------------------------------------------------------------
    # Runtime inspection
    # ------------------------------------------------------------------
    def run_runtime_inspection(
        self,
        *,
        window_title: str | None,
        max_depth: int,
    ) -> list[Issue]:
        if automation is None:
            raise RuntimeError(
                "Runtime inspection requires the 'uiautomation' package. "
                "Install it via 'pip install uiautomation'."
            )
        if window_title is None:
            window_title = self._default_window_title()
        if not window_title:
            raise ValueError(
                "Unable to derive a window title. Provide --window-title explicitly, or ensure your executable name matches the window title."
            )

        if self.verbose:
            print(f"🔍 Attaching to window with title '{window_title}' (depth {max_depth}).")

        window_control = automation.WindowControl(searchDepth=2, Name=window_title)
        if not window_control.Exists(maxSearchSeconds=2):
            regex_title = rf".*{re.escape(window_title)}.*"
            window_control = automation.WindowControl(searchDepth=3, RegexName=regex_title)
            if not window_control.Exists(maxSearchSeconds=2):
                raise RuntimeError(
                    f"Could not find a window matching '{window_title}'. "
                    "Ensure the application is running."
                )

        issues: list[Issue] = []
        self._walk_runtime_tree(
            window_control,
            path=[self._runtime_node_label(window_control)],
            max_depth=max_depth,
            issues=issues,
        )
        return issues


    def _walk_runtime_tree(
        self,
        control: Any,
        *,
        path: Sequence[str],
        max_depth: int,
        depth: int = 0,
        issues: list[Issue] | None = None,
    ) -> list[Issue]:
        if issues is None:
            issues = []

        location = " > ".join(path)
        control_type = getattr(control, "ControlTypeName", "Control")
        name = getattr(control, "Name", "") or ""
        automation_id = getattr(control, "AutomationId", "") or ""

        if self.verbose:
            print(f"Inspecting {location} (type={control_type}, name={name!r})")

        # Flag empty text-like controls.
        if control_type in TEXTUAL_CONTROL_TYPES:
            value_text = self._get_control_value(control)
            if not (value_text or name):
                issues.append(
                    Issue(
                        location=location,
                        message="Text-based control appears empty. Possible binding failure.",
                    )
                )

        if not automation_id and not name:
            issues.append(
                Issue(
                    location=location,
                    message="Control lacks AutomationId and Name; consider naming for testing.",
                    severity="info",
                )
            )

        if depth >= max_depth:
            return issues

        try:
            children = control.GetChildren()
        except Exception as exc:  # pragma: no cover - UIA can be temperamental.
            issues.append(
                Issue(
                    location=location,
                    message=f"Failed to enumerate children: {exc}",
                    severity="info",
                )
            )
            return issues

        for idx, child in enumerate(children):
            child_label = self._runtime_node_label(child, fallback_index=idx)
            child_path = [*path, child_label]
            self._walk_runtime_tree(
                child,
                path=child_path,
                max_depth=max_depth,
                depth=depth + 1,
                issues=issues,
            )
        return issues

    @staticmethod
    def _runtime_node_label(control: Any, *, fallback_index: int | None = None) -> str:
        control_type = getattr(control, "ControlTypeName", "Control")
        name = getattr(control, "Name", "") or ""
        if name:
            return f"{control_type}('{name}')"
        if fallback_index is not None:
            return f"{control_type}[{fallback_index}]"
        return control_type

    @staticmethod
    def _get_control_value(control: Any) -> str:
        try:
            pattern = control.GetValuePattern()
        except Exception:  # pragma: no cover - depends on automation support.
            return ""
        value = getattr(pattern, "Value", "")
        if value is None:
            return ""
        return str(value)

    # ------------------------------------------------------------------
    # Reporting helpers
    # ------------------------------------------------------------------
    def emit_report(self, issues: Iterable[Issue], *, mode: str) -> None:
        issues_list = list(issues)
        header = f"📋 {mode.upper()} report: {len(issues_list)} finding(s)."
        print("\n" + header)
        if issues_list:
            for item in issues_list:
                print(item.format())
        else:
            print("✅ No gremlins detected.")

        if self.report_path is not None:
            report_text = "\n".join([header, *[issue.format() for issue in issues_list]])
            self.report_path.write_text(report_text, encoding="utf-8")
            if self.verbose:
                print(f"📝 Report written to {self.report_path}")

    @staticmethod
    def _tag_name(element: Any) -> str:
        tag = getattr(element, "tag", None)
        if not isinstance(tag, str):
            return "#comment"
        if etree is None:
            raise RuntimeError("lxml is required to derive element tag names.")
        qname = etree.QName(tag)
        return qname.localname

    @staticmethod
    def _is_element_node(node: Any) -> bool:
        tag = getattr(node, "tag", None)
        return isinstance(tag, str)


def _flatten_mock_data(
    payload: Any,
    *,
    prefix: str = "",
    accumulator: dict[str, Any] | None = None,
) -> dict[str, Any]:
    if accumulator is None:
        accumulator = {}

    def _store(key: str, value: Any) -> None:
        if key:
            accumulator[key] = value

    if isinstance(payload, dict):
        if prefix:
            _store(prefix, payload)
        for key, value in payload.items():
            next_prefix = f"{prefix}.{key}" if prefix else key
            _flatten_mock_data(value, prefix=next_prefix, accumulator=accumulator)
    elif isinstance(payload, list):
        _store(prefix, payload)
        _store(f"{prefix}.Count" if prefix else "Count", len(payload))
    else:
        _store(prefix, payload)
    return accumulator


def load_mock_data(path: Path | None) -> dict[str, Any]:
    if path is None:
        return {}
    if not path.exists():
        raise FileNotFoundError(path)
    content = path.read_text(encoding="utf-8")
    data = json.loads(content)
    if not isinstance(data, dict):
        raise ValueError("Mock data JSON must be an object at the top level.")
    return _flatten_mock_data(data)


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Static and runtime helper for triaging WPF binding problems. "
            "Supply a XAML file by default or --runtime for a live window."
        )
    )
    parser.add_argument(
        "target",
        type=Path,
        help="Path to the XAML file (static mode) or executable (runtime mode).",
    )
    parser.add_argument(
        "--runtime",
        action="store_true",
        help="Attach to a running WPF window instead of parsing XAML.",
    )
    parser.add_argument(
        "--mock-data",
        type=Path,
        help="Optional JSON file describing mock data context for static analysis.",
    )
    parser.add_argument(
        "--window-title",
        type=str,
        help="Title of the window to inspect in runtime mode. Defaults to exe name.",
    )
    parser.add_argument(
        "--max-depth",
        type=int,
        default=5,
        help="Maximum depth when traversing the runtime UI tree (default: 5).",
    )
    parser.add_argument(
        "--report",
        type=Path,
        help="Optional file to save a summary report (text).",
    )
    parser.add_argument(
        "--verbose",
        action="store_true",
        help="Enable verbose logging for both modes.",
    )
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(argv)

    try:
        mock_data = load_mock_data(args.mock_data)
    except Exception as exc:
        print(f"💥 Failed to load mock data: {exc}")
        return 1

    sleuth = XamlSleuth(
        xaml_path=args.target if not args.runtime else None,
        runtime_target=args.target if args.runtime else None,
        mock_data=mock_data,
        report_path=args.report,
        verbose=args.verbose,
    )

    try:
        if args.runtime:
            issues = sleuth.run_runtime_inspection(
                window_title=args.window_title,
                max_depth=args.max_depth,
            )
            sleuth.emit_report(issues, mode="runtime")
        else:
            issues = sleuth.run_static_analysis()
            sleuth.emit_report(issues, mode="static")
    except Exception as exc:
        print(f"💥 Execution failed: {exc}")
        return 1
    return 0


if __name__ == "__main__":  # pragma: no cover - CLI entry point.
    sys.exit(main())
