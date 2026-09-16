"""Tests for VERSION.yml helpers (stdlib unittest)."""

from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from tools.versionamento.files import sync_package_versions
from tools.versionamento.semver import parse


class SyncPackageVersionsTest(unittest.TestCase):
    def test_syncs_ruby_csharp_and_node(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            ruby = root / "packages/ruby/agenda_cobranca/lib/agenda_cobranca/version.rb"
            csharp = root / "packages/csharp/AgendaCobranca.Sdk/src/AgendaCobranca.Sdk/AgendaCobranca.Sdk.csproj"
            node = root / "packages/nodejs/agenda-cobranca/package.json"
            ruby.parent.mkdir(parents=True)
            csharp.parent.mkdir(parents=True)
            node.parent.mkdir(parents=True)
            ruby.write_text('module AgendaCobranca\n  VERSION = "0.1.0"\nend\n', encoding="utf-8")
            csharp.write_text("<Project><Version>0.1.0</Version></Project>\n", encoding="utf-8")
            node.write_text(json.dumps({"name": "agenda-cobranca", "version": "0.1.0"}, indent=2) + "\n", encoding="utf-8")

            updated = sync_package_versions(root, parse("0.2.0"))
            self.assertEqual(len(updated), 3)
            self.assertIn('VERSION = "0.2.0"', ruby.read_text(encoding="utf-8"))
            self.assertIn("<Version>0.2.0</Version>", csharp.read_text(encoding="utf-8"))
            self.assertEqual(json.loads(node.read_text(encoding="utf-8"))["version"], "0.2.0")


if __name__ == "__main__":
    unittest.main()
