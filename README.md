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
- menu horizontal compacto, foco visível e estados claros de seleção;
- layout responsivo a partir do tamanho mínimo de 980 × 680;
- onboarding e editor de captura usando os mesmos componentes visuais.

### Atalhos de texto

- atalhos iniciados por `/` ou `:` em Outlook, Teams, navegadores e outros apps;
- texto simples ou formatado com fonte, tamanho, cores, marca-texto, listas,
  alinhamento, tabelas, imagens e hiperlinks;
- variáveis preenchíveis, datas automáticas, cálculos de data e `{{tab}}`;
- sugestões flutuantes, preview e estatísticas locais.
- importação de `snippets.md`, exportações JSON do Text Blaze e arquivos YAML
  do Espanso, com conversão das variáveis compatíveis e proteção contra conflitos.

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
- edição durante a própria seleção de região, sem abrir outra janela, com seta,
  marca-texto, retângulo, círculo, lápis, texto, numeração, cores, espessura,
  desfazer e refazer;
- ações de copiar, salvar ou concluir usando a regra ativa;
- salvamento automático, clipboard e histórico local das últimas capturas;
- estatísticas integradas de atalhos, acentos e capturas por tipo;
- sem upload, conta ou compartilhamento externo.

## Primeira inicialização e atualizações

Na primeira abertura, o SlashDesk apresenta as funções principais e explica onde
os dados permanecem. A verificação em background consulta as Releases oficiais
de `lucasllira/SlashText`, ignora drafts e prereleases no canal estável e pode ser
desativada em **Configurações**. Ela não envia atalhos, capturas, estatísticas ou
identificadores pessoais.

## Arquivos portáteis

O ZIP portátil contém apenas:

```text
SlashDesk.exe
```

O runtime self-contained e os componentes nativos são incorporados ao executável.
Quando necessário, o .NET os extrai no cache interno de bundle em `%TEMP%\.net`.
Na edição portátil, os dados permanentes ficam ao lado do executável:

```text
SlashDesk.exe
SlashDeskData/
```

Na edição instalada, permanecem em `%LocalAppData%\SlashDesk`. Em ambos os modos,
a origem contém os mesmos nomes e formatos:

- `settings.json`: preferências;
- `usage.json`: contadores locais;
- `capture-history.json`: tipo, horário, tamanho e caminho das capturas recentes;
- `assets/`: imagens usadas nos atalhos;
- `Backups/`: um ZIP por dia ou sob demanda, com restauração e retenção das sete
  cópias mais recentes.
- `Logs/`: diagnósticos locais sem conteúdo de snippets ou capturas;
- `Updates/`: temporários controlados da atualização portátil.

A primeira execução portátil prioriza um `SlashDeskData` válido já existente. Se
ele ainda não existir, os dados legados de `%LocalAppData%\SlashDesk` são copiados
para staging, validados, respaldados e só então ativados. A origem antiga não é
apagada. Se as duas origens existirem, a origem portátil prevalece e a outra é
preservada em backup, sem mesclagem destrutiva.

A publicação instalada usa o perfil `Installed` e gera a pasta self-contained
que será consumida por um instalador futuro. A publicação portátil usa o perfil
`Portable` e gera um único executável self-contained `win-x64`. Para atualizar o
portátil, o SlashDesk valida o ZIP e o SHA-256, encerra o processo principal e usa
uma cópia temporária do próprio executável para substituir atomicamente somente
`SlashDesk.exe`. Se a nova versão não confirmar a inicialização, o executável
anterior é restaurado. `SlashDeskData` nunca é incluído nem substituído.

A compilação instalada ainda não oferece atualização automática: enquanto não
existir um instalador transacional validado, ela abre a Release oficial para
atualização manual e mantém `%LocalAppData%\SlashDesk` fora do staging.

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

O SlashDesk 2.9.1 estabiliza a gravação local de MP4 e GIF. A próxima versão
fica reservada para captura com rolagem, ações de captura/gravação no menu da
bandeja e revisão visual, além da assinatura digital para distribuição corporativa.

## Licença

Distribuído sob a licença MIT.
