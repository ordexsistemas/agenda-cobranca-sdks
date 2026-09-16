"""Git helpers via subprocess."""

from __future__ import annotations

import subprocess
from pathlib import Path


def run(args: list[str], cwd: Path, check: bool = True) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        args,
        cwd=cwd,
        check=check,
        text=True,
        capture_output=True,
    )


def commits_since_tag(cwd: Path, tag: str | None) -> list[str]:
    if tag:
        rng = f"{tag}..HEAD"
        proc = run(["git", "log", "--pretty=%s", rng], cwd, check=False)
        if proc.returncode != 0:
            proc = run(["git", "log", "--pretty=%s", "-50"], cwd)
    else:
        proc = run(["git", "log", "--pretty=%s", "-50"], cwd)
    lines = [ln.strip() for ln in proc.stdout.splitlines() if ln.strip()]
    return lines


def latest_component_tag(cwd: Path, component: str) -> str | None:
    prefix = f"{component}/v"
    proc = run(["git", "tag", "--list", f"{prefix}*", "--sort=-v:refname"], cwd, check=False)
    tags = [ln.strip() for ln in proc.stdout.splitlines() if ln.strip()]
    return tags[0] if tags else None


def configure_user(cwd: Path, name: str = "github-actions[bot]", email: str = "github-actions[bot]@users.noreply.github.com") -> None:
    run(["git", "config", "user.name", name], cwd)
    run(["git", "config", "user.email", email], cwd)


def commit_all(cwd: Path, message: str) -> None:
    run(["git", "add", "-A"], cwd)
    # Allow empty? No — release should change files
    run(["git", "commit", "-m", message], cwd)


def annotated_tag(cwd: Path, tag: str, message: str) -> None:
    run(["git", "tag", "-a", tag, "-m", message], cwd)


def push_all(cwd: Path, remote: str = "origin") -> None:
    run(["git", "push", remote, "HEAD"], cwd)
    run(["git", "push", remote, "--tags"], cwd)
