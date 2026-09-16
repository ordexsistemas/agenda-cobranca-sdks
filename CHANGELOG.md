# Changelog

Todas as mudanças notáveis deste projeto serão documentadas neste arquivo.

O formato segue [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/),
e este projeto adere ao [Semantic Versioning](https://semver.org/lang/pt-BR/).

## [Unreleased]

### Added
- Autenticação por `api_key` apenas (headers `chave_api` + `X-Api-Key`)
- HMAC opcional (`signing_enabled` / `SigningEnabled`, OFF por padrão)
- Base URL padrão HML Ordex Pay (`/api/v2/externo`)
- Guia de integração: `docs/integracao-ordex-pay.md`
- Versionamento SemVer via `tools/versionamento` + workflow GitHub Actions

### Changed
- Ruby, Go e C#: `client_id` / `client_secret` não são mais obrigatórios no uso básico

## [0.1.0] - 2026-09-15

### Added
- Entrega inicial dos thin clients Ruby, Go e C# (cobranças + HMAC + webhooks locais)
