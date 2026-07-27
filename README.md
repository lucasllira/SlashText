# SlashText

Aplicativo portátil para Windows que expande atalhos iniciados por `/` em Outlook,
Teams, navegadores e outros campos de texto.

## Recursos

- criação, edição, pesquisa e exclusão pela interface;
- categorias recolhíveis na navegação lateral;
- armazenamento legível em `snippets.md`;
- texto simples ou formatado com negrito, itálico, sublinhado, cor e hiperlink;
- campos preenchíveis antes da expansão, como `{{nome}}`;
- data e hora automáticas, mês, ano, semana e dia da semana;
- cálculos como `{{data:-7d}}`, `{{data:+1m}}` e `{{data:+1y}}`;
- `{{tab}}` para preencher campos em sequência, como assunto e corpo do e-mail;
- sugestões flutuantes ao digitar `/`;
- estatísticas locais e atalhos mais usados;
- minimização para a bandeja e inicialização opcional com o Windows;
- backup diário consolidado com retenção dos últimos sete snapshots;
- publicação self-contained e single-file para Windows x64.

## Variáveis

| Variável | Resultado |
|---|---|
| `{{nome}}` | Solicita um valor antes de inserir |
| `{{campo\|padrão}}` | Campo preenchível com valor sugerido |
| `{{data}}` | Data atual |
| `{{hora}}` | Hora atual |
| `{{datahora}}` ou `{{agora}}` | Data e hora |
| `{{dia}}`, `{{mes}}`, `{{mes_nome}}`, `{{ano}}` | Partes da data |
| `{{semana}}`, `{{dia_semana}}` | Semana e dia por extenso |
| `{{usuario}}` | Usuário atual do Windows |
| `{{data:+7d}}` | Data calculada; aceita `d`, `m` e `y` |
| `{{tab}}` | Pressiona Tab e continua no próximo campo |

Os formatos podem ser personalizados, por exemplo:
`{{data:+7d|dddd, dd 'de' MMMM}}`.

## Executar e publicar

Em um Windows com o SDK do .NET 10:

```powershell
dotnet restore
dotnet run --project .\src\SlashText\SlashText.csproj
```

Publicação portátil:

```powershell
dotnet publish .\src\SlashText\SlashText.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -o .\publish
```

## Privacidade

O SlashText não envia dados para a internet. O detector global mantém apenas um
buffer curto iniciado por `/`. As estatísticas registram o identificador do atalho,
a contagem, a data do último uso e a quantidade estimada de caracteres poupados.
