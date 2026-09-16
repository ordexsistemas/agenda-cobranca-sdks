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
            lock = root / "packages/ruby/agenda_cobranca/Gemfile.lock"
            ruby_wa = root / "packages/ruby/whatsapp/lib/ordex_whatsapp/version.rb"
            lock_wa = root / "packages/ruby/whatsapp/Gemfile.lock"
            csharp = root / "packages/csharp/AgendaCobranca.Sdk/src/AgendaCobranca.Sdk/AgendaCobranca.Sdk.csproj"
            csharp_wa = root / "packages/csharp/Ordex.WhatsApp.Sdk/src/Ordex.WhatsApp.Sdk/Ordex.WhatsApp.Sdk.csproj"
            node = root / "packages/nodejs/agenda-cobranca/package.json"
            whatsapp = root / "packages/nodejs/whatsapp-sdk/package.json"
            for path in (ruby, lock, ruby_wa, lock_wa, csharp, csharp_wa, node, whatsapp):
                path.parent.mkdir(parents=True, exist_ok=True)
            ruby.write_text('module AgendaCobranca\n  VERSION = "0.1.0"\nend\n', encoding="utf-8")
            lock.write_text("PATH\n  specs:\n    agenda_cobranca (0.1.0)\n\nCHECKSUMS\n  agenda_cobranca (0.1.0)\n", encoding="utf-8")
            ruby_wa.write_text('module OrdexWhatsApp\n  VERSION = "0.1.0"\nend\n', encoding="utf-8")
            lock_wa.write_text("PATH\n  specs:\n    ordex_whatsapp (0.1.0)\n\nCHECKSUMS\n  ordex_whatsapp (0.1.0)\n", encoding="utf-8")
            csharp.write_text("<Project><Version>0.1.0</Version></Project>\n", encoding="utf-8")
            csharp_wa.write_text("<Project><Version>0.1.0</Version></Project>\n", encoding="utf-8")
            node.write_text(json.dumps({"name": "@ordexsistemas/agenda-cobranca", "version": "0.1.0"}, indent=2) + "\n", encoding="utf-8")
            whatsapp.write_text(json.dumps({"name": "@ordexsistemas/whatsapp-sdk", "version": "0.1.0"}, indent=2) + "\n", encoding="utf-8")

            updated = sync_package_versions(root, parse("0.2.0"))
            self.assertEqual(len(updated), 8)
            self.assertIn('VERSION = "0.2.0"', ruby.read_text(encoding="utf-8"))
            self.assertIn('VERSION = "0.2.0"', ruby_wa.read_text(encoding="utf-8"))
            self.assertEqual(lock.read_text(encoding="utf-8").count("agenda_cobranca (0.2.0)"), 2)
            self.assertEqual(lock_wa.read_text(encoding="utf-8").count("ordex_whatsapp (0.2.0)"), 2)
            self.assertIn("<Version>0.2.0</Version>", csharp.read_text(encoding="utf-8"))
            self.assertIn("<Version>0.2.0</Version>", csharp_wa.read_text(encoding="utf-8"))
            self.assertEqual(json.loads(node.read_text(encoding="utf-8"))["version"], "0.2.0")
            self.assertEqual(json.loads(whatsapp.read_text(encoding="utf-8"))["version"], "0.2.0")


if __name__ == "__main__":
    unittest.main()
