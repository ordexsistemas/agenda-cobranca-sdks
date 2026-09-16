# Publicação dos SDKs

Os pacotes compartilham a versão de `VERSION.yml` (hoje **0.2.0**). O CLI `./bin/versionamento release` atualiza `VERSION.yml`, `CHANGELOG.md` e os metadados Ruby/C#/Node, cria a tag anotada `sdk/vX.Y.Z` e, no Actions, dispara a publicação.

Workflows:

- [`.github/workflows/versionamento.yml`](../.github/workflows/versionamento.yml) — bump SemVer + tag
- [`.github/workflows/publish.yml`](../.github/workflows/publish.yml) — publica nos registries (também em `workflow_dispatch` e em push da tag `sdk/v*`)
- [`.github/workflows/test.yml`](../.github/workflows/test.yml) — testes em PR/push

## Secrets do GitHub Actions

Configure em **Settings → Secrets and variables → Actions** (não commite tokens):

| Secret | Obrigatório para | Uso |
| --- | --- | --- |
| `RUBYGEMS_API_KEY` | RubyGems | `gem push` da gem `agenda_cobranca`. Alternativa: `GEM_HOST_API_KEY` (mesmo valor; a API do RubyGems espera este nome no ambiente). |
| `NUGET_API_KEY` | NuGet.org | `dotnet nuget push` de `AgendaCobranca.Sdk` |
| `NPM_TOKEN` | npm | `npm publish` de `@ordex/agenda-cobranca` (automação com permissão **Publish**; `--access public`) |

O job Go **não** precisa de secret de registry. Jobs cujo secret estiver vazio são **pulados** com warning (o restante do workflow segue).

## RubyGems — `agenda_cobranca`

```bash
cd packages/ruby/agenda_cobranca
gem build agenda_cobranca.gemspec
GEM_HOST_API_KEY=... gem push agenda_cobranca-*.gem
```

## NuGet — `AgendaCobranca.Sdk`

```bash
cd packages/csharp/AgendaCobranca.Sdk
dotnet pack src/AgendaCobranca.Sdk/AgendaCobranca.Sdk.csproj -c Release -p:PackageVersion=0.2.0
dotnet nuget push nupkgs/*.nupkg -k "$NUGET_API_KEY" -s https://api.nuget.org/v3/index.json --skip-duplicate
```

## npm — `@ordex/agenda-cobranca`

Pacote **scoped** (`@ordex/...`). Requer Node 18+ e publish com `--access public`.

```bash
cd packages/nodejs/agenda-cobranca
npm ci
npm test
npm run build
NODE_AUTH_TOKEN=... npm publish --access public
```

## Go — `agendacobranca.dev/sdk/go`

Go não usa um registry separado: o consumidor busca o módulo via VCS + [proxy.golang.org](https://proxy.golang.org).

- Caminho do módulo (import): `agendacobranca.dev/sdk/go`
- Diretório no monorepo: `packages/go/agenda-cobranca-go`
- Tag de release do produto: `sdk/vX.Y.Z` (versionamento)
- Tag do módulo (convenção de subdiretório): `packages/go/agenda-cobranca-go/vX.Y.Z` — criada pelo workflow de publish no mesmo commit da tag `sdk/v*`

### Com vanity DNS (`agendacobranca.dev`)

O host deve responder `?go-get=1` com:

```html
<meta name="go-import" content="agendacobranca.dev/sdk git https://github.com/ordexsistemas/agenda-cobranca-sdks">
```

Neste layout o path `go` após o prefixo `agendacobranca.dev/sdk` não coincide com `packages/go/agenda-cobranca-go`. Prefira um prefixo que aponte o repositório inteiro e um `replace`, **ou** sirva o meta com o repositório GitHub e use o tag de subdiretório abaixo.

Instalação pretendida depois do vanity:

```bash
go get agendacobranca.dev/sdk/go@v0.2.0
```

### Sem vanity (GitHub, funciona após o tag de subdiretório)

O `go.mod` declara `module agendacobranca.dev/sdk/go`. Para baixar pelo GitHub sem o domínio vanity, o consumidor usa `replace` apontando o caminho real do repositório (o `go.mod` da substituição continua com o path vanity):

```go
require agendacobranca.dev/sdk/go v0.2.0

replace agendacobranca.dev/sdk/go => github.com/ordexsistemas/agenda-cobranca-sdks/packages/go/agenda-cobranca-go v0.2.0
```

```bash
go get github.com/ordexsistemas/agenda-cobranca-sdks/packages/go/agenda-cobranca-go@v0.2.0
```

O segundo comando só resolve se o proxy encontrar o tag `packages/go/agenda-cobranca-go/v0.2.0`. Esse tag é criado automaticamente no publish.

O job Go ainda dispara um fetch em `https://proxy.golang.org/agendacobranca.dev/sdk/go/@v/vX.Y.Z.info` (best-effort; falha sem vanity não quebra o workflow).

## Publicar uma versão

1. Configure os secrets acima.
2. Rode **Versionamento SDK** (`workflow_dispatch`) ou `./bin/versionamento release --kind patch --push`.
3. Confira a run de **Publish SDKs** e as páginas npm / NuGet / RubyGems.
4. Consumo Go: `go get …@vX.Y.Z` conforme a seção anterior.
