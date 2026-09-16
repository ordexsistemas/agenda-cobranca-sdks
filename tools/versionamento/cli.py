#!/usr/bin/env python3
"""CLI: tools/versionamento — SemVer bump + CHANGELOG + tag sdk/vX.Y.Z

Commands:
  show                          Print current VERSION.yml
  bump --kind auto|patch|minor|major [--dry-run]
  release --kind auto|patch|minor|major [--push] [--github-release]

Inspired by clic_versionamento; stdlib only (no PyPI deps).
"""

from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
from pathlib import Path

from .files import load_version_yml, update_changelog, write_version_yml
from .gitutil import (
    annotated_tag,
    commit_all,
    commits_since_tag,
    configure_user,
    latest_component_tag,
    push_all,
)
from .semver import bump_from_commits


def repo_root() -> Path:
    env = os.environ.get("VERSIONAMENTO_ROOT")
    if env:
        return Path(env).resolve()
    # tools/versionamento/cli.py → parents[2] = repo root
    return Path(__file__).resolve().parents[2]


def cmd_show(args: argparse.Namespace) -> int:
    root = repo_root()
    data, ver = load_version_yml(root / "VERSION.yml")
    print(json.dumps({"component": data.get("component"), "product": data.get("product"), "version": str(ver)}, ensure_ascii=False, indent=2))
    return 0


def resolve_kind(root: Path, component: str, kind: str) -> tuple[str, list[str]]:
    tag = latest_component_tag(root, component)
    messages = commits_since_tag(root, tag)
    if kind == "auto":
        kind = bump_from_commits(messages)
    return kind, messages


def cmd_bump(args: argparse.Namespace) -> int:
    root = repo_root()
    version_path = root / "VERSION.yml"
    changelog_path = root / "CHANGELOG.md"
    data, current = load_version_yml(version_path)
    component = data.get("component", "sdk")
    kind, messages = resolve_kind(root, component, args.kind)
    new_ver = current.bump(kind)
    notes = messages[:20] if messages else ["Release automatizado"]

    print(f"bump {kind}: {current} → {new_ver}")
    if args.dry_run:
        print("(dry-run) sem alterações em disco")
        return 0

    write_version_yml(version_path, data, new_ver)
    update_changelog(changelog_path, new_ver, notes)
    print(f"atualizado VERSION.yml e CHANGELOG.md → {new_ver}")
    return 0


def cmd_release(args: argparse.Namespace) -> int:
    root = repo_root()
    version_path = root / "VERSION.yml"
    changelog_path = root / "CHANGELOG.md"
    data, current = load_version_yml(version_path)
    component = data.get("component", "sdk")
    product = data.get("product", "Ordex Pay SDKs")
    kind, messages = resolve_kind(root, component, args.kind)
    new_ver = current.bump(kind)
    notes = messages[:20] if messages else ["Release automatizado"]
    tag = f"{component}/v{new_ver}"

    print(f"release {kind}: {current} → {new_ver} (tag {tag})")
    if args.dry_run:
        print("(dry-run) sem commit/tag/push")
        return 0

    write_version_yml(version_path, data, new_ver)
    update_changelog(changelog_path, new_ver, notes)

    if os.environ.get("CI") or args.configure_git:
        configure_user(root)

    commit_all(root, f"chore(release): {component} v{new_ver}")
    annotated_tag(root, tag, f"{product} v{new_ver}")

    if args.push:
        push_all(root)

    if args.github_release:
        body = "\n".join(f"- {n}" for n in notes)
        title = f"{product} v{new_ver}"
        env = os.environ.copy()
        cmd = [
            "gh", "release", "create", tag,
            "--title", title,
            "--notes", body,
        ]
        subprocess.run(cmd, cwd=root, check=True, env=env)
        print(f"GitHub Release criado: {tag}")

    print(f"OK {tag}")
    return 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(prog="versionamento", description="SemVer + CHANGELOG + tag sdk/vX.Y.Z")
    sub = parser.add_subparsers(dest="command", required=True)

    p_show = sub.add_parser("show", help="Mostra VERSION.yml atual")
    p_show.set_defaults(func=cmd_show)

    p_bump = sub.add_parser("bump", help="Atualiza VERSION.yml + CHANGELOG (sem tag)")
    p_bump.add_argument("--kind", choices=["auto", "patch", "minor", "major"], default="auto")
    p_bump.add_argument("--dry-run", action="store_true")
    p_bump.set_defaults(func=cmd_bump)

    p_rel = sub.add_parser("release", help="Bump + commit + annotated tag (+ push/gh release opcional)")
    p_rel.add_argument("--kind", choices=["auto", "patch", "minor", "major"], default="auto")
    p_rel.add_argument("--dry-run", action="store_true")
    p_rel.add_argument("--push", action="store_true", help="git push HEAD + tags")
    p_rel.add_argument("--github-release", action="store_true", help="gh release create")
    p_rel.add_argument("--configure-git", action="store_true", help="configura user.name/email (CI)")
    p_rel.set_defaults(func=cmd_release)

    args = parser.parse_args(argv)
    return args.func(args)


if __name__ == "__main__":
    raise SystemExit(main())
