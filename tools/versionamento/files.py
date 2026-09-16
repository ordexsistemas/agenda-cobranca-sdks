"""Read/write VERSION.yml, CHANGELOG.md, and package versions."""

from __future__ import annotations

import json
import re
from datetime import date
from pathlib import Path

from .semver import Version, parse

# Keep language packages aligned with VERSION.yml on bump/release.
RUBY_VERSION_RBS = (
    Path("packages/ruby/agenda_cobranca/lib/agenda_cobranca/version.rb"),
    Path("packages/ruby/whatsapp/lib/ordex_whatsapp/version.rb"),
)
RUBY_GEMFILE_LOCKS = (
    Path("packages/ruby/agenda_cobranca/Gemfile.lock"),
    Path("packages/ruby/whatsapp/Gemfile.lock"),
)
CSHARP_CSPROJS = (
    Path("packages/csharp/AgendaCobranca.Sdk/src/AgendaCobranca.Sdk/AgendaCobranca.Sdk.csproj"),
    Path("packages/csharp/Ordex.WhatsApp.Sdk/src/Ordex.WhatsApp.Sdk/Ordex.WhatsApp.Sdk.csproj"),
)
NODE_PACKAGE_JSONS = (
    Path("packages/nodejs/agenda-cobranca/package.json"),
    Path("packages/nodejs/whatsapp-sdk/package.json"),
)
RUBY_LOCK_GEM_NAMES = ("agenda_cobranca", "ordex_whatsapp")


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


def sync_package_versions(root: Path, version: Version) -> list[Path]:
    """Write the monorepo version into Ruby/C#/Node package metadata.

    Go modules are identified by git tags (see root README), not a
    version field in go.mod. Returns paths that were updated.
    """
    ver = str(version)
    updated: list[Path] = []

    for rel in RUBY_VERSION_RBS:
        ruby = root / rel
        if ruby.exists():
            text = ruby.read_text(encoding="utf-8")
            new_text, n = re.subn(r'VERSION\s*=\s*"[^"]+"', f'VERSION = "{ver}"', text, count=1)
            if n:
                ruby.write_text(new_text, encoding="utf-8")
                updated.append(ruby)

    for rel, gem_name in zip(RUBY_GEMFILE_LOCKS, RUBY_LOCK_GEM_NAMES):
        lock = root / rel
        if lock.exists():
            text = lock.read_text(encoding="utf-8")
            new_text, n = re.subn(
                rf"{re.escape(gem_name)} \([0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?\)",
                f"{gem_name} ({ver})",
                text,
            )
            if n:
                lock.write_text(new_text, encoding="utf-8")
                updated.append(lock)

    for rel in CSHARP_CSPROJS:
        csproj = root / rel
        if csproj.exists():
            text = csproj.read_text(encoding="utf-8")
            new_text, n = re.subn(
                r"<Version>[^<]*</Version>",
                f"<Version>{ver}</Version>",
                text,
                count=1,
            )
            if n:
                csproj.write_text(new_text, encoding="utf-8")
                updated.append(csproj)

    for rel in NODE_PACKAGE_JSONS:
        package_json = root / rel
        if package_json.exists():
            data = json.loads(package_json.read_text(encoding="utf-8"))
            if data.get("version") != ver:
                data["version"] = ver
                package_json.write_text(
                    json.dumps(data, indent=2, ensure_ascii=False) + "\n",
                    encoding="utf-8",
                )
                updated.append(package_json)

    return updated

