"""Read/write VERSION.yml and CHANGELOG.md."""

from __future__ import annotations

import re
from datetime import date
from pathlib import Path

from .semver import Version, parse


def load_version_yml(path: Path) -> tuple[dict[str, str], Version]:
    raw = path.read_text(encoding="utf-8")
    data: dict[str, str] = {}
    for line in raw.splitlines():
        line = line.strip()
        if not line or line.startswith("#") or ":" not in line:
            continue
        key, _, val = line.partition(":")
        data[key.strip()] = val.strip().strip('"').strip("'")
    if "version" not in data:
        raise ValueError(f"{path} sem campo version")
    return data, parse(data["version"])


def write_version_yml(path: Path, data: dict[str, str], version: Version) -> None:
    component = data.get("component", "sdk")
    product = data.get("product", "Ordex Pay SDKs")
    content = (
        f"# {product} — monorepo version (component: {component})\n"
        f"component: {component}\n"
        f'product: "{product}"\n'
        f"version: {version}\n"
    )
    path.write_text(content, encoding="utf-8")


def update_changelog(path: Path, version: Version, notes: list[str], today: date | None = None) -> None:
    today = today or date.today()
    text = path.read_text(encoding="utf-8") if path.exists() else (
        "# Changelog\n\n"
        "Todas as mudanças notáveis deste projeto serão documentadas neste arquivo.\n\n"
        "## [Unreleased]\n\n"
    )

    bullet_block = ""
    if notes:
        bullet_block = "\n".join(f"- {n}" for n in notes) + "\n"
    else:
        bullet_block = "- Release automatizado\n"

    new_section = (
        f"## [{version}] - {today.isoformat()}\n\n"
        f"### Changed\n"
        f"{bullet_block}\n"
    )

    unreleased = re.search(r"## \[Unreleased\]\s*\n", text, re.IGNORECASE)
    if unreleased:
        # Insert new version section right after Unreleased block header + keep Unreleased empty-ish
        insert_at = unreleased.end()
        # Find next ## [ after Unreleased to splice
        rest = text[insert_at:]
        next_ver = re.search(r"\n## \[", rest)
        if next_ver:
            # Replace content under Unreleased with blank + new section before previous versions
            before = text[:insert_at]
            after_unreleased = rest[next_ver.start():]
            text = before + "\n" + new_section + after_unreleased.lstrip("\n")
        else:
            text = text[:insert_at] + "\n" + new_section + rest
    else:
        text = text.rstrip() + "\n\n" + new_section

    # Ensure Unreleased section exists at top-ish
    if "## [Unreleased]" not in text:
        text = "# Changelog\n\n## [Unreleased]\n\n" + text

    path.write_text(text, encoding="utf-8")
