# Conteúdo para o front (página Integração)

| Arquivo | Uso |
| --- | --- |
| [`pagina-integracao.md`](pagina-integracao.md) | Texto principal dos **SDKs** na tela Integração / API externa |

## Como o front pode carregar

1. **Estático no `agenda_cobranca_api` / Ordex Pay**  
   Copie `pagina-integracao.md` para o asset/CMS do painel (ou importe no build).

2. **Fetch do GitHub (raw)**  
   ```text
   https://raw.githubusercontent.com/ordexsistemas/agenda-cobranca-sdks/main/docs/portal/pagina-integracao.md
   ```
   Renderize com o markdown viewer do front (ex.: `react-markdown`).

3. **Endpoint interno**  
   Sirva o MD (ou HTML gerado) via API do próprio SaaS para não depender do GitHub em produção.

O guia técnico completo (HMAC opcional, arquitetura) continua em [`../integracao-ordex-pay.md`](../integracao-ordex-pay.md).
