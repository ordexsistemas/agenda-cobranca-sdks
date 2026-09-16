# tools/versionamento

CLI portátil (Python stdlib) inspirada no `clic_versionamento`:

- Lê/atualiza `VERSION.yml` (component `sdk`, product "Ordex Pay SDKs")
- Atualiza `CHANGELOG.md` (Keep a Changelog)
- Sincroniza a versão nos pacotes Ruby (`version.rb`), C# (`.csproj`) e Node (`package.json`)
- Cria tag anotada `sdk/vX.Y.Z`
- Opcionalmente faz push e `gh release create`

O módulo Go (`agendacobranca.dev/sdk/go`) não tem campo de versão no `go.mod`; o workflow de publish cria a tag de subdiretório `packages/go/agenda-cobranca-go/vX.Y.Z`. Ver [`docs/publishing.md`](../../docs/publishing.md).

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
Após o tag, chama [`.github/workflows/publish.yml`](../../.github/workflows/publish.yml) (RubyGems, NuGet, npm, tag Go). Secrets: [`docs/publishing.md`](../../docs/publishing.md).
