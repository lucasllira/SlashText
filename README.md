# SlashDesk

Utilitário portátil e local para Windows que reúne expansão de texto, Acento
Rápido e captura de tela. Foi pensado para ambientes pessoais e corporativos
onde instalar vários aplicativos ou enviar conteúdo para a nuvem não é uma opção.

> O projeto continua no repositório `SlashText` para preservar links e histórico.
> A partir da versão 2.0, o produto e o executável se chamam **SlashDesk**.

## Recursos

### Interface

- design system próprio com grafite, branco quente e ciano funcional;
- temas claro, escuro ou sincronizado com o Windows;
- navegação consistente, foco visível e estados claros de seleção;
- layout responsivo a partir do tamanho mínimo de 1080 × 800;
- onboarding e editor de captura usando os mesmos componentes visuais.

### Atalhos de texto

- atalhos iniciados por `/` ou `:` em Outlook, Teams, navegadores e outros apps;
- texto simples ou formatado com fonte, tamanho, cores, marca-texto, listas,
  alinhamento, tabelas, imagens e hiperlinks;
- variáveis preenchíveis, datas automáticas, cálculos de data e `{{tab}}`;
- sugestões flutuantes, preview e estatísticas locais.

### Acento Rápido

- conjuntos configuráveis, incluindo somente Português (Brasil);
- suporte a Caps Lock, Shift e layouts de teclado diferentes;
- avanço previsível de uma opção por toque na tecla de ativação, sem o
  auto-repeat do Windows pular caracteres;
- posição, atraso, ordenação e aplicativos excluídos configuráveis.

### Captura local

- monitor ativo;
- seleção livre de região;
- reconhecimento da janela sob o cursor;
- atalho global independente para cada ação, gravado ao pressionar a combinação;
- teclas de função, `Print Screen`, teclado, roda, botão central e botões
  laterais do mouse;
- combinações como `PrintScreen`, `F10`, `Ctrl+Shift+WheelUp` e `Alt+MouseX1`;
- pasta automática com variáveis `{year}`, `{month}`, `{month-name}` e `{day}`;
- nome com `{date}`, `{time}`, `{type}` e `{app}`;
- PNG ou JPEG com qualidade configurável;
- editor após captura de região com seta, marca-texto, retângulo, círculo,
  lápis, texto, numeração, cores, espessura, desfazer e refazer;
- ações de copiar, salvar ou concluir usando a regra ativa;
- salvamento automático, clipboard e histórico local das últimas capturas;
- estatísticas integradas de atalhos, acentos e capturas por tipo;
- sem upload, conta ou compartilhamento externo.

## Primeira inicialização e atualizações

Na primeira abertura, o SlashDesk apresenta as funções principais e explica onde
os dados permanecem. A verificação de atualização consulta somente o último
GitHub Release e pode ser desativada em **Configurações**. Ela não envia atalhos,
capturas, estatísticas ou identificadores pessoais.

## Arquivos portáteis

O pacote contém apenas:

```text
SlashDesk.exe
SlashDeskData/
└── snippets.md
```

Durante o uso, a pasta `SlashDeskData` também pode conter:

- `settings.json`: preferências;
- `usage.json`: contadores locais;
- `capture-history.json`: tipo, horário, tamanho e caminho das capturas recentes;
- `assets/`: imagens usadas nos atalhos;
- `backups/`: um ZIP por dia, com retenção de sete dias.

A atualização migra automaticamente `SlashTextData` para `SlashDeskData`, com
prioridade para os dados reais do usuário.

## Variáveis de texto

| Variável | Resultado |
|---|---|
| `{{nome}}` | Solicita um valor antes de inserir |
| `{{campo\|padrão}}` | Campo preenchível com valor sugerido |
| `{{data}}`, `{{hora}}`, `{{agora}}` | Data e hora |
| `{{data:+7d}}` | Data calculada; aceita `d`, `m` e `y` |
| `{{usuario}}` | Usuário atual do Windows |
| `{{tab}}` | Pressiona Tab e continua no próximo campo |

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

## Privacidade e limitações do Windows

O conteúdo digitado e as imagens capturadas não saem do computador. O histórico
de captura não armazena a imagem, apenas metadados locais necessários para a
lista de recentes.

O Windows pode bloquear capturas da área de trabalho segura, conteúdo com DRM ou
aplicativos elevados quando o SlashDesk não está no mesmo nível de permissão.
Atalhos já reservados pelo Windows ou por outro programa também podem ser
recusados; o aplicativo informa o conflito.

## Próximas etapas

- recorte, desfoque e pixelização no editor de captura;
- gravação local de MP4 e GIF, com região/janela/monitor, FPS e cursor;
- captura com atraso e captura com rolagem;
- assinatura digital para facilitar distribuição corporativa.

Gravação e GIF permanecem no roadmap até que o pipeline de mídia seja validado
de ponta a ponta; a interface não mostra ações que ainda não funcionem.

## Licença

Distribuído sob a licença MIT.
