"""Minimal SemVer helpers (MAJOR.MINOR.PATCH)."""

from __future__ import annotations

import re
from dataclasses import dataclass

_SEMVER = re.compile(r"^(\d+)\.(\d+)\.(\d+)$")


@dataclass(frozen=True)
class Version:
    major: int
    minor: int
    patch: int

    def __str__(self) -> str:
        return f"{self.major}.{self.minor}.{self.patch}"

    def bump(self, kind: str) -> "Version":
        kind = kind.lower()
        if kind == "major":
            return Version(self.major + 1, 0, 0)
        if kind == "minor":
            return Version(self.major, self.minor + 1, 0)
        if kind == "patch":
            return Version(self.major, self.minor, self.patch + 1)
        raise ValueError(f"bump inválido: {kind} (use major|minor|patch)")


def parse(text: str) -> Version:
    m = _SEMVER.match(text.strip())
    if not m:
        raise ValueError(f"versão SemVer inválida: {text!r}")
    return Version(int(m.group(1)), int(m.group(2)), int(m.group(3)))


# Conventional Commits → bump kind
_BREAKING = re.compile(r"^(\w+)(\(.+\))?!:|^BREAKING CHANGE:", re.MULTILINE | re.IGNORECASE)
_FEAT = re.compile(r"^feat(\(.+\))?:", re.MULTILINE | re.IGNORECASE)
_FIX = re.compile(r"^(fix|perf|refactor)(\(.+\))?:", re.MULTILINE | re.IGNORECASE)


def bump_from_commits(messages: list[str]) -> str:
    """Return major|minor|patch from conventional commit messages."""
    joined = "\n".join(messages)
    if any(_BREAKING.search(m) for m in messages) or _BREAKING.search(joined):
        return "major"
    if any(_FEAT.search(m) for m in messages):
        return "minor"
    if any(_FIX.search(m) for m in messages):
        return "patch"
    return "patch"
