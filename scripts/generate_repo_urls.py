#!/usr/bin/env python3
"""
AI Fetchable Repository Manifest Generator

This script generates a comprehensive manifest of all tracked files in the repository,
including URLs, metadata, and context information to enhance visibility and accessibility
for Large Language Models (LLMs) and AI systems.

The generated manifest includes:
- File URLs (both blob and raw for GitHub)
- File metadata (size, timestamps, extensions)
- Language detection
- Repository information
- Structured data for AI consumption

Best practices incorporated:
- Clear documentation and structure
- Comprehensive metadata for context
- Standardized URL formats
- Extensible design for additional context
"""

import datetime
import hashlib
import json
import mimetypes
import subprocess
from pathlib import Path
from typing import Any


class RepoManifestGenerator:
    """Generates AI-fetchable repository manifests with comprehensive file metadata."""

    # Language mappings for common extensions
    LANGUAGE_MAP = {
        '.py': 'Python',
        '.cs': 'C#',
        '.xaml': 'XAML',
        '.json': 'JSON',
        '.md': 'Markdown',
        '.txt': 'Text',
        '.xml': 'XML',
        '.yml': 'YAML',
        '.yaml': 'YAML',
        '.ps1': 'PowerShell',
        '.sh': 'Shell',
        '.js': 'JavaScript',
        '.ts': 'TypeScript',
        '.html': 'HTML',
        '.css': 'CSS',
        '.sql': 'SQL',
        '.config': 'Configuration',
        '.sln': 'Visual Studio Solution',
        '.csproj': 'C# Project',
        '.vb': 'Visual Basic',
        '.cpp': 'C++',
        '.c': 'C',
        '.h': 'C/C++ Header',
        '.java': 'Java',
        '.php': 'PHP',
        '.rb': 'Ruby',
        '.go': 'Go',
        '.rs': 'Rust',
        '.swift': 'Swift',
        '.kt': 'Kotlin',
        '.scala': 'Scala',
        '.clj': 'Clojure',
        '.fs': 'F#',
        '.r': 'R',
        '.m': 'MATLAB',
        '.ipynb': 'Jupyter Notebook',
        '.dockerfile': 'Docker',
        '.tf': 'Terraform',
        '.lock': 'Lock File',
        '.toml': 'TOML',
        '.ini': 'INI',
        '.bat': 'Batch',
        '.cmd': 'Command',
        '.gitignore': 'Git Ignore',
        '.gitattributes': 'Git Attributes',
    }

    def __init__(self, repo_path: str = "."):
        self.repo_path = Path(repo_path).resolve()
        self.repo_info = self._get_repo_info()
        self.tracked_files = self._get_tracked_files()

    def _run_git_command(self, command: list[str]) -> str:
        """Run a git command and return the output."""
        try:
            result = subprocess.run(
                ["git"] + command,
                cwd=self.repo_path,
                capture_output=True,
                text=True,
                check=True
            )
            return result.stdout.strip()
        except subprocess.CalledProcessError as e:
            print(f"Git command failed: {' '.join(command)}")
            print(f"Error: {e}")
            return ""

    def _get_repo_info(self) -> dict[str, Any]:
        """Get repository information."""
        remote_url = self._run_git_command(["config", "--get", "remote.origin.url"])
        branch = self._run_git_command(["branch", "--show-current"])
        commit_hash = self._run_git_command(["rev-parse", "HEAD"])
        is_dirty = bool(self._run_git_command(["status", "--porcelain"]))

        # Extract owner/repo from URL
        owner_repo = ""
        if remote_url:
            if "github.com" in remote_url:
                # Handle various GitHub URL formats
                if remote_url.endswith(".git"):
                    remote_url = remote_url[:-4]
                parts = remote_url.split("/")
                if len(parts) >= 2:
                    owner_repo = f"{parts[-2]}/{parts[-1]}"

        return {
            "remote_url": remote_url,
            "owner_repo": owner_repo,
            "branch": branch or "main",
            "commit_hash": commit_hash,
            "is_dirty": is_dirty,
            "generated_at": datetime.datetime.now().isoformat()
        }

    def _get_tracked_files(self) -> list[str]:
        """Get list of tracked files."""
        output = self._run_git_command(["ls-files"])
        return output.split("\n") if output else []

    def _get_file_metadata(self, file_path: str) -> dict[str, Any]:
        """Get comprehensive metadata for a file."""
        full_path = self.repo_path / file_path

        metadata = {
            "path": file_path,
            "exists": full_path.exists(),
            "size": 0,
            "last_modified": None,
            "extension": full_path.suffix.lower(),
            "language": self.LANGUAGE_MAP.get(full_path.suffix.lower(), "Unknown"),
            "mime_type": mimetypes.guess_type(str(full_path))[0],
            "line_count": 0,
            "encoding": "utf-8",  # Assume UTF-8
            "is_binary": False,
            "sha256": ""
        }

        if full_path.exists():
            stat = full_path.stat()
            metadata["size"] = stat.st_size
            metadata["last_modified"] = datetime.datetime.fromtimestamp(stat.st_mtime).isoformat()

            # Check if binary
            try:
                with open(full_path, 'rb') as f:
                    chunk = f.read(1024)
                    metadata["is_binary"] = b'\x00' in chunk
                    if not metadata["is_binary"]:
                        # Count lines for text files
                        content = chunk.decode('utf-8', errors='ignore')
                        metadata["line_count"] = len(content.splitlines())
                        # Calculate SHA256
                        f.seek(0)
                        metadata["sha256"] = hashlib.sha256(f.read()).hexdigest()
            except Exception:
                metadata["is_binary"] = True

        return metadata

    def _generate_file_urls(self, file_path: str) -> dict[str, str]:
        """Generate URLs for the file."""
        urls = {}

        if self.repo_info["owner_repo"]:
            base_url = f"https://github.com/{self.repo_info['owner_repo']}"
            urls["blob_url"] = f"{base_url}/blob/{self.repo_info['branch']}/{file_path}"
            urls["raw_url"] = f"{base_url}/raw/{self.repo_info['branch']}/{file_path}"
            urls["history_url"] = f"{base_url}/commits/{self.repo_info['branch']}/{file_path}"

        return urls

    def _get_file_context(self, file_path: str, metadata: dict[str, Any]) -> dict[str, Any]:
        """Get additional context for the file."""
        context = {
            "category": "unknown",
            "importance": "normal",
            "description": "",
            "dependencies": [],
            "related_files": []
        }

        # Categorize files
        if file_path.startswith("src/") or file_path.startswith("WileyWidget."):
            context["category"] = "source_code"
            context["importance"] = "high"
        elif file_path.startswith("tests/") or "test" in file_path.lower():
            context["category"] = "test"
            context["importance"] = "medium"
        elif file_path in ["README.md", "CONTRIBUTING.md", "LICENSE"]:
            context["category"] = "documentation"
            context["importance"] = "high"
        elif file_path.startswith("docs/"):
            context["category"] = "documentation"
            context["importance"] = "medium"
        elif file_path.startswith("scripts/"):
            context["category"] = "automation"
            context["importance"] = "medium"
        elif any(file_path.endswith(ext) for ext in [".csproj", ".sln", ".json", ".config"]):
            context["category"] = "configuration"
            context["importance"] = "high"

        # Add descriptions for common files
        descriptions = {
            "README.md": "Main project documentation with overview, installation, and usage instructions",
            "CONTRIBUTING.md": "Guidelines for contributing to the project",
            "LICENSE": "Project license information",
            ".gitignore": "Git ignore patterns for excluding files from version control",
            "package.json": "Node.js package configuration and dependencies",
            "requirements.txt": "Python package dependencies",
            "pyproject.toml": "Python project configuration",
            "WileyWidget.csproj": "C# project file with build configuration",
            "WileyWidget.sln": "Visual Studio solution file",
        }

        context["description"] = descriptions.get(file_path, f"{metadata['language']} file containing project code/data")

        # Extract dependencies for certain files
        if file_path == "requirements.txt" and not metadata["is_binary"]:
            try:
                with open(self.repo_path / file_path, encoding='utf-8') as f:
                    deps = [line.strip() for line in f if line.strip() and not line.startswith('#')]
                    context["dependencies"] = deps[:20]  # Limit to first 20
            except Exception:
                pass

        return context

    def generate_manifest(self) -> dict[str, Any]:
        """Generate the complete repository manifest."""
        print(f"Generating manifest for {len(self.tracked_files)} tracked files...")

        files_data = []
        for file_path in self.tracked_files:
            if not file_path.strip():
                continue

            metadata = self._get_file_metadata(file_path)
            urls = self._generate_file_urls(file_path)
            context = self._get_file_context(file_path, metadata)

            file_entry = {
                "metadata": metadata,
                "urls": urls,
                "context": context
            }

            files_data.append(file_entry)

        # Sort files by path for consistent output
        files_data.sort(key=lambda x: x["metadata"]["path"])

        manifest = {
            "repository": self.repo_info,
            "summary": {
                "total_files": len(files_data),
                "total_size": sum(f["metadata"]["size"] for f in files_data),
                "categories": {},
                "languages": {}
            },
            "files": files_data
        }

        # Calculate summary statistics
        for file_entry in files_data:
            cat = file_entry["context"]["category"]
            lang = file_entry["metadata"]["language"]

            manifest["summary"]["categories"][cat] = manifest["summary"]["categories"].get(cat, 0) + 1
            manifest["summary"]["languages"][lang] = manifest["summary"]["languages"].get(lang, 0) + 1

        return manifest

    def save_manifest(self, output_path: str | None = None) -> str:
        """Generate and save the manifest to a file."""
        if output_path is None:
            output_path = str(self.repo_path / "ai-fetchable-manifest.json")

        manifest = self.generate_manifest()

        with open(output_path, 'w', encoding='utf-8') as f:
            json.dump(manifest, f, indent=2, ensure_ascii=False)

        print(f"Manifest saved to: {output_path}")
        print(f"Total files: {manifest['summary']['total_files']}")
        print(f"Total size: {manifest['summary']['total_size']} bytes")

        return output_path

def main():
    """Main entry point."""
    import argparse

    parser = argparse.ArgumentParser(
        description="Generate AI-fetchable repository manifest with file URLs and metadata"
    )
    parser.add_argument(
        "--output", "-o",
        help="Output file path (default: ai-fetchable-manifest.json)"
    )
    parser.add_argument(
        "--repo-path", "-p",
        default=".",
        help="Repository path (default: current directory)"
    )

    args = parser.parse_args()

    generator = RepoManifestGenerator(args.repo_path)
    output_path = generator.save_manifest(args.output)

    print("\nManifest generation complete!")
    print(f"File: {output_path}")
    print("\nThis manifest provides:")
    print("- URLs for all tracked files (blob and raw)")
    print("- Comprehensive metadata for AI context")
    print("- File categorization and importance levels")
    print("- Language detection and statistics")
    print("- Structured data for LLM consumption")

if __name__ == "__main__":
    main()
