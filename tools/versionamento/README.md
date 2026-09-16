# tools/versionamento

CLI portátil (Python stdlib) inspirada no `clic_versionamento`:

- Lê/atualiza `VERSION.yml` (component `sdk`, product "Ordex Pay SDKs")
- Atualiza `CHANGELOG.md` (Keep a Changelog)
- Cria tag anotada `sdk/vX.Y.Z`
- Opcionalmente faz push e `gh release create`

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
