# tools/versionamento

CLI portátil (Python stdlib) inspirada no `clic_versionamento`:

- Lê/atualiza `VERSION.yml` (component `sdk`, product "Ordex Pay SDKs")
- Atualiza `CHANGELOG.md` (Keep a Changelog)
- Sincroniza a versão nos pacotes Ruby (`agenda_cobranca` e `ordex_whatsapp`), C# (`AgendaCobranca.Sdk` e `Ordex.WhatsApp.Sdk`) e Node (`agenda-cobranca` e `whatsapp-sdk` `package.json`)
- Cria tag anotada `sdk/vX.Y.Z`
- Opcionalmente faz push e `gh release create`

O módulo Go (`agendacobranca.dev/sdk/go` e `agendacobranca.dev/sdk/whatsapp`) não tem campo de versão no `go.mod`; o workflow de publish cria as tags de subdiretório `packages/go/agenda-cobranca-go/vX.Y.Z` e `packages/go/whatsapp-go/vX.Y.Z`. Ver README na raiz (GitHub Packages).

## Uso local

```bash
./bin/versionamento show
./bin/versionamento bump --kind patch --dry-run
./bin/versionamento release --kind auto --dry-run
```

## Conventional Commits → bump (`--kind auto`)

| Commits desde a última tag `sdk/v*` | Bump |
| --- | --- |
| `feat!:` / `BREAKING CHANGE:` | major |
| `feat:` | minor |
| `fix:` / `perf:` / `refactor:` (ou nenhum convencional) | patch |

## Actions

Workflow: `.github/workflows/versionamento.yml` (`workflow_dispatch`).
Após o tag, chama [`.github/workflows/publish.yml`](../../.github/workflows/publish.yml) (GitHub Packages: npm agenda-cobranca + whatsapp-sdk, NuGet Agenda + WhatsApp, RubyGems agenda_cobranca + ordex_whatsapp + tags Go).
