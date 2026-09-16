# Changelog

Todas as mudanças notáveis deste projeto serão documentadas neste arquivo.

O formato segue [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/),
e este projeto adere ao [Semantic Versioning](https://semver.org/lang/pt-BR/).

## [Unreleased]

### Added
- SDK Node.js/TypeScript `@ordexsistemas/whatsapp-sdk` (add-on SaaS WhatsApp Cloud API, medição por categoria e quantidade de cobranças)
- SDKs WhatsApp irmãos em C# (`Ordex.WhatsApp.Sdk`), Go (`agendacobranca.dev/sdk/whatsapp`) e Ruby (`ordex_whatsapp`, com initializer Rails)

### Changed
- Pasta `docs/` deixou de ser versionada (instruções de GitHub Packages no README da raiz)

## [0.2.1] - 2026-09-16

### Added
- SDK Node.js/TypeScript `@ordexsistemas/agenda-cobranca` (`packages/nodejs/agenda-cobranca`)
- Pipeline de publicação no GitHub Packages (npm, NuGet, RubyGems) e tag do módulo Go

### Changed
- Scope npm `@ordexsistemas/agenda-cobranca` (org GitHub) e registries apontando para GitHub Packages, não nuget.org / npmjs / RubyGems.org

## [0.2.0] - 2026-09-16

### Changed
- Merge pull request #1 from ordexsistemas/feat/api-key-only
- chore: add SemVer versionamento tooling and GitHub Actions
- feat: api_key-only auth for Ruby, Go, and C# SDKs
- chore: resolve LICENSE merge (MIT Ordex Sistemas)
- chore: add MIT License (Ordex Sistemas)
- Initial commit
- feat: scaffold dos SDKs Ruby, Go e C# da Agenda Cobranca API
- Initialize project

## [0.1.0] - 2026-09-15

### Added
- Entrega inicial dos thin clients Ruby, Go e C# (cobranças + HMAC + webhooks locais)
