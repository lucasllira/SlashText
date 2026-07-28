# SlashText

Aplicativo portátil para Windows que expande atalhos iniciados por `/` ou `:` em Outlook,
Teams, navegadores e outros campos de texto.

## Recursos

- criação, edição, pesquisa e exclusão pela interface;
- categorias recolhíveis na navegação lateral;
- armazenamento legível em `snippets.md`;
- texto simples ou formatado com negrito, itálico, sublinhado, cor e hiperlink;
- campos preenchíveis antes da expansão, como `{{nome}}`;
- datas abreviadas ou extensas, mês e dia da semana abreviados ou por extenso;
- cálculos como `{{data:-7d}}`, `{{data:+1m}}` e `{{data:+1y}}`;
- `{{tab}}` para preencher campos em sequência, como assunto e corpo do e-mail;
- preview em tempo real e catálogo de variáveis clicáveis com descrições;
- blocos de código com linguagem e ação de copiar no preview;
- imagens incorporadas em aplicativos que aceitam conteúdo HTML;
- sugestões flutuantes ao digitar `/` ou `:`;
- Acento Rápido opcional para teclados sem teclas acentuadas;
- estatísticas locais e atalhos mais usados;
- temas claro, escuro profundo e automático;
- minimização para a bandeja e inicialização opcional com o Windows;
- backup diário consolidado com retenção dos últimos sete snapshots;
- publicação self-contained e single-file para Windows x64.

## Variáveis

| Variável | Resultado |
|---|---|
| `{{nome}}` | Solicita um valor antes de inserir |
| `{{campo\|padrão}}` | Campo preenchível com valor sugerido |
| `{{data}}` | Data atual |
| `{{data_curta}}` | Data abreviada |
| `{{data_extensa}}` | Data por extenso |
| `{{hora}}` | Hora atual |
| `{{datahora}}` ou `{{agora}}` | Data e hora |
| `{{dia}}`, `{{mes}}`, `{{ano}}` | Partes numéricas da data |
| `{{mes_nome}}`, `{{mes_curto}}` | Mês extenso ou abreviado |
| `{{dia_semana}}`, `{{dia_semana_curto}}` | Dia extenso ou abreviado |
| `{{ano_curto}}`, `{{semana}}` | Ano com 2 dígitos e semana |
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

## Arquivos portáteis

- `SlashText.exe`: aplicativo single-file;
- `snippets.md`: atalhos e formatação legível;
- `assets/`: imagens adicionadas aos atalhos;
- `settings.json`: preferências locais;
- `usage.json`: contadores anônimos de uso;
- `backups/`: no máximo sete snapshots diários do Markdown.

Os arquivos JSON são dados pequenos criados durante o uso, não dependências do app.
DLLs permanecem incorporadas ao executável publicado.

## Licença

Distribuído sob a licença MIT.
