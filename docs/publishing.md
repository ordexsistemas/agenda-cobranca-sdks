# Publicação dos SDKs — GitHub Packages

Os pacotes compartilham a versão de `VERSION.yml` (hoje **0.2.0**). O CLI `./bin/versionamento release` atualiza `VERSION.yml`, `CHANGELOG.md` e os metadados Ruby/C#/Node, cria a tag anotada `sdk/vX.Y.Z` e, no Actions, publica no **GitHub Packages** da org `ordexsistemas`.

Não usamos nuget.org, npmjs.com nem RubyGems.org.

Workflows:

- [`.github/workflows/versionamento.yml`](../.github/workflows/versionamento.yml) — bump SemVer + tag
- [`.github/workflows/publish.yml`](../.github/workflows/publish.yml) — publica no GitHub Packages (também em `workflow_dispatch` e em push da tag `sdk/v*`)
- [`.github/workflows/test.yml`](../.github/workflows/test.yml) — testes em PR/push

## Permissões do Actions

O publish usa `GITHUB_TOKEN` do próprio repositório. Não é necessário `NPM_TOKEN`, `NUGET_API_KEY` nem `RUBYGEMS_API_KEY`.

```yaml
permissions:
  contents: write   # tag Go de subdiretório + checkout
  packages: write   # npm / NuGet / RubyGems no GitHub Packages
```

Se um job de publish falhar, o workflow falha (não há skip silencioso por secret ausente).

## Visibilidade

Os pacotes no GitHub Packages **herdam a visibilidade do repositório**.  
Se o repo for privado, o consumidor precisa de um PAT com `read:packages` (e `repo` se o pacote estiver ligado a um repositório privado). Em repo público, a leitura costuma ser possível sem PAT, mas o GitHub ainda pode exigir autenticação em alguns clientes.

## npm — `@ordexsistemas/agenda-cobranca`

Registry: `https://npm.pkg.github.com`  
O scope **precisa** ser `@ordexsistemas` (mesmo nome da org).

### Instalar (consumidor)

`.npmrc` no projeto:

```ini
@ordexsistemas:registry=https://npm.pkg.github.com
//npm.pkg.github.com/:_authToken=SEU_PAT
```

```bash
npm i @ordexsistemas/agenda-cobranca
```

O PAT precisa de `read:packages` quando o pacote/repositório não for publicamente legível.

### Publicar (já feito no Actions)

```bash
cd packages/nodejs/agenda-cobranca
npm ci
npm test
npm run build
NODE_AUTH_TOKEN="$GITHUB_TOKEN" npm publish
```

`package.json` declara `publishConfig.registry: https://npm.pkg.github.com`.

## NuGet — `AgendaCobranca.Sdk`

Source: `https://nuget.pkg.github.com/ordexsistemas/index.json`

### Instalar (consumidor)

```bash
dotnet nuget add source https://nuget.pkg.github.com/ordexsistemas/index.json \
  --name github \
  --username SEU_USUARIO \
  --password SEU_PAT \
  --store-password-in-clear-text

dotnet add package AgendaCobranca.Sdk
```

O PAT precisa de `read:packages` (e `repo` se o repositório for privado).

### Publicar (já feito no Actions)

```bash
dotnet pack ... -p:PackageVersion=0.2.0
dotnet nuget push nupkgs/*.nupkg \
  --api-key "$GITHUB_TOKEN" \
  --source https://nuget.pkg.github.com/ordexsistemas/index.json
```

## RubyGems — `agenda_cobranca`

Host: `https://rubygems.pkg.github.com/ordexsistemas`

### Instalar (consumidor)

`~/.gem/credentials` (chmod 0600):

```yaml
:github: Bearer SEU_PAT
```

Gemfile:

```ruby
source "https://rubygems.pkg.github.com/ordexsistemas" do
  gem "agenda_cobranca"
end
```

O PAT precisa de `read:packages`.

### Publicar (já feito no Actions)

```bash
printf '%s\n' ":github: Bearer ${GITHUB_TOKEN}" > ~/.gem/credentials
chmod 0600 ~/.gem/credentials
gem build agenda_cobranca.gemspec
gem push --key github --host https://rubygems.pkg.github.com/ordexsistemas agenda_cobranca-*.gem
```

## Go — `agendacobranca.dev/sdk/go`

Go **não** é publicado como card do GitHub Packages. O consumidor busca o módulo via VCS + [proxy.golang.org](https://proxy.golang.org).

- Caminho do módulo (import): `agendacobranca.dev/sdk/go`
- Diretório no monorepo: `packages/go/agenda-cobranca-go`
- Tag de release do produto: `sdk/vX.Y.Z` (versionamento)
- Tag do módulo (convenção de subdiretório): `packages/go/agenda-cobranca-go/vX.Y.Z` — criada pelo workflow de publish no mesmo commit da tag `sdk/v*`

### Com vanity DNS (`agendacobranca.dev`)

O host deve responder `?go-get=1` com:

```html
<meta name="go-import" content="agendacobranca.dev/sdk git https://github.com/ordexsistemas/agenda-cobranca-sdks">
```

Instalação pretendida depois do vanity:

```bash
go get agendacobranca.dev/sdk/go@v0.2.0
```

### Sem vanity (GitHub)

```go
require agendacobranca.dev/sdk/go v0.2.0

replace agendacobranca.dev/sdk/go => github.com/ordexsistemas/agenda-cobranca-sdks/packages/go/agenda-cobranca-go v0.2.0
```

```bash
go get github.com/ordexsistemas/agenda-cobranca-sdks/packages/go/agenda-cobranca-go@v0.2.0
```

O segundo comando só resolve se o proxy encontrar o tag `packages/go/agenda-cobranca-go/v0.2.0`.

O job Go ainda dispara um fetch em `https://proxy.golang.org/agendacobranca.dev/sdk/go/@v/vX.Y.Z.info` (best-effort; falha sem vanity DNS não quebra o workflow).

## Publicar uma versão

1. Rode **Versionamento SDK** (`workflow_dispatch`) ou `./bin/versionamento release --kind patch --push`.
2. Confira a run de **Publish SDKs** e os pacotes em [github.com/orgs/ordexsistemas/packages](https://github.com/orgs/ordexsistemas/packages).
3. Consumo: npm / NuGet / gem conforme as seções acima; Go via `go get …@vX.Y.Z`.
