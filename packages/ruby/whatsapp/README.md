# ordex_whatsapp

Add-on **SaaS Ordex Pay** para a API principal do WhatsApp (Meta Cloud API), em Ruby.

Empresas no plano que inclui o extra WhatsApp usam esta gem para:

1. Enviar templates e mensagens de sessão pela Graph API.
2. Taguear cada envio em `auth` | `utility` | `service` | `marketing`.
3. Conferir entitlement do add-on (plugável contra Ordex Pay).
4. Medir o volume contra as cotas mensais do Cenário Base (~100k transações/mês).
5. Controlar a quantidade de cobranças na [Agenda de Cobranças](../agenda_cobranca) a partir desse uso.

A sincronização da agenda **compõe** a gem `agenda_cobranca` (`AgendaSdkAdapter`) — HMAC e headers `chave_api` / `X-Api-Key` ficam no cliente da Agenda.

## Instalação

Publicado no **GitHub Packages**. Host e PAT: README na raiz do monorepo.

```ruby
# Gemfile
source "https://rubygems.pkg.github.com/ordexsistemas" do
  gem "ordex_whatsapp"
end
```

No monorepo:

```ruby
gem "ordex_whatsapp", path: "../../packages/ruby/whatsapp"
```

Não commite tokens reais — use placeholders de ambiente.

## Uso

```ruby
client = OrdexWhatsApp::Client.new(
  access_token: ENV.fetch("WHATSAPP_ACCESS_TOKEN"),
  phone_number_id: ENV.fetch("WHATSAPP_PHONE_NUMBER_ID"),
  waba_id: ENV["WHATSAPP_WABA_ID"],
  tenant_id: ENV.fetch("ORDEX_PAY_TENANT_ID"),
  ordex_api_key: ENV["ORDEX_PAY_API_KEY"],
  agenda_pagador: { documento: "12345678901", nome: "Empresa SaaS" },
  entitlement: OrdexWhatsApp::StaticEntitlementChecker.new(true) # demo
)

client.send_template(
  to: "5511999999999",
  category: "utility",
  template: { name: "pix_recebido", language: "pt_BR" }
)

preview = client.preview_cobranca_plan
sync = client.sync_cobrancas # create / keep / cancel
```

Initializer Rails: `examples/rails/ordex_whatsapp.rb`.

## Cotas do Cenário Base (configuráveis)

| Categoria | Volume / mês | Unitário USD | Total USD | Total BRL (câmbio 5,5) |
| --- | ---: | ---: | ---: | ---: |
| `auth` | 40.000 | 0,0315 | 1.260 | 6.930 |
| `utility` | 60.000 | 0,0350 | 2.100 | 11.550 |
| `service` | 5.000 (**1.000 free**) | 0,0300 | 120 | 660 |
| `marketing` | 10.000 | 0,0625 | 625 | 3.437,50 |

`CobrancaQuantity.compute` converte uso em quantidade na Agenda (`per-category` | `by-sessions` | `by-value`).  
`external_reference` = `wa:{tenant}:{period}:{categoria}:{indice}`.

## Testes

```bash
cd packages/ruby/whatsapp
bundle install
bundle exec rspec
```
