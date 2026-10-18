# Guia de uso do SlashDesk

Navegação: [README do produto](../README.md) · [suporte](../SUPPORT.md) · [índice técnico](README.md).

Este guia detalha os recursos do checkout atual. A interface completa e os idiomas estão disponíveis na estável 3.3.0; escolha o pacote pela [página de releases](https://github.com/lucasllira/SlashText/releases). A versão estável e o pré-lançamento anterior são identificados no README.

## Principais recursos

| Área | O que oferece |
|---|---|
| **Atalhos de texto** | Expansões iniciadas por `/`, conteúdo formatado, variáveis, categorias, sugestões e estatísticas |
| **Acento Rápido** | Seleção de caracteres por tecla, conjuntos configuráveis, suporte a Caps Lock, Shift e layouts diferentes |
| **Captura** | Monitor, janela, região e captura longa adaptativa, com modos Direta/Editor, atalhos globais, histórico e salvamento configurável |
| **Gravação** | GIF e MP4 locais, seleção de alvo, presets, cursor, pausa, retomada, prévia e histórico |
| **Experiência** | Temas Claro, Preto e Seguir o Windows, bandeja, inicialização com o sistema e atualização com validação de integridade |

### Atalhos de texto

- gatilhos com `/` em Outlook, Teams, navegadores e outros aplicativos;
- texto simples ou formatado com fonte, tamanho, cores, marca-texto, listas, alinhamento, tabelas, imagens e hyperlinks;
- variáveis preenchíveis, datas automáticas, cálculos de data e navegação com `{{tab}}`;
- busca, categorias, filtros, favoritos por uso e sugestões flutuantes;
- importação do próprio `snippets.md`, de exportações JSON do Text Blaze e de arquivos YAML do Espanso;
- preservação de gatilhos legados incompatíveis, que permanecem nos dados sem serem ativados silenciosamente;
- estatísticas locais de uso e caracteres economizados.

### Acento Rápido

- conjunto Português (Brasil) e outros conjuntos configuráveis;
- escolha da tecla de ativação, posição da barra e atraso;
- ordenação por uso e exibição opcional do código Unicode;
- suporte a Caps Lock, Shift, ABNT e layouts diferentes;
- lista de aplicativos excluídos;
- temporizador independente da repetição do teclado e funcionamento nos campos editáveis do SlashDesk.

### Capturas e editor

- captura do monitor ativo, janela sob o cursor ou região livre;
- captura longa adaptativa para páginas compatíveis, com card, atalho e ação próprios;
- atraso configurável e inclusão opcional do cursor;
- atalhos independentes com teclado, `Print Screen`, roda e botões laterais do mouse;
- barra de anotação integrada à seleção de região;
- seta, marca-texto, retângulo, círculo, lápis, texto e numeração;
- cores, espessura, desfazer, refazer e edição inline;
- PNG ou JPEG com qualidade configurável;
- cópia para o clipboard, salvamento automático e histórico local;
- destino e nome de arquivo personalizáveis com variáveis de data, tipo e aplicativo;
- posicionamento correto em múltiplos monitores, coordenadas negativas, DPI misto e taskbars em diferentes bordas.

### GIF e MP4

- gravação de monitor, janela ou região;
- presets de FPS e qualidade;
- inclusão opcional do cursor;
- pausa, retomada, contador e finalização explícita;
- prévia de GIF antes de manter o arquivo no histórico;
- MP4 local em H.264 por recursos do Windows, sem exigir FFmpeg;
- controles e diagnósticos para finalização assíncrona.

### Interface

- temas **Claro**, **Preto** e **Seguir o Windows**;
- navegação horizontal e componentes reutilizáveis;
- workspace de atalhos com lista, editor e painel de variáveis;
- divisores redimensionáveis no workspace;
- foco visível, contraste semântico e suporte à escala do Windows;
- barra de captura Fluent e catálogo local de emojis Noto.

A implementação da interface completa do Visual Lab faz parte da **3.3.0 estável**.
A versão anterior 3.2.0 mantém a fundação Fluent anterior. A nova interface preserva
os dados, os atalhos e os formatos existentes.

## Instalação portátil

1. Abra a [Release mais recente](https://github.com/lucasllira/SlashText/releases/latest).
2. Baixe `SlashDesk-X.Y.Z-portable-win-x64.zip`.
3. Extraia o ZIP em uma pasta com permissão de escrita.
4. Execute `SlashDesk.exe`.

O pacote é self-contained para Windows x64: não é necessário instalar o .NET separadamente. Na primeira execução, o aplicativo cria `SlashDeskData` ao lado do executável.

Estrutura esperada:

~~~text
SlashDesk/
├── SlashDesk.exe
└── SlashDeskData/
~~~

Para manter o aplicativo realmente portátil, mova ou copie **a pasta inteira**, incluindo `SlashDeskData`.

## Primeiros passos

1. Conclua a apresentação inicial.
2. Abra **Atalhos** e crie um gatilho como `/teste`.
3. Digite o gatilho em outro aplicativo e confirme a expansão.
4. Na área de **Captura**, configure os atalhos globais.
5. Ajuste destino, nome, formato e comportamento das capturas.
6. Ative o Acento Rápido somente nos aplicativos em que pretende usá-lo.

Se um atalho global já estiver reservado pelo Windows ou por outro programa, o SlashDesk informa o conflito para que outra combinação seja escolhida.

## Variáveis de texto

| Variável | Resultado |
|---|---|
| `{{nome}}` | Solicita um valor antes de inserir |
| `{{campo\|padrão}}` | Campo preenchível com valor sugerido |
| `{{data}}`, `{{hora}}`, `{{agora}}` | Data e hora atuais |
| `{{data:+7d}}` | Data calculada; aceita `d`, `m` e `y` |
| `{{usuario}}` | Usuário atual do Windows |
| `{{tab}}` | Pressiona Tab e continua no próximo campo |

## Dados locais e backups

Na edição portátil, os dados permanentes ficam em `SlashDeskData`. Na edição instalada de desenvolvimento, ficam em `%LocalAppData%\SlashDesk`.

| Item | Conteúdo |
|---|---|
| `snippets.md` | Atalhos, categorias e conteúdo |
| `settings.json` | Preferências do aplicativo |
| `usage.json` | Estatísticas locais |
| `capture-history.json` | Metadados e caminhos das capturas e gravações |
| `assets/` | Imagens incorporadas aos atalhos |
| `Backups/` | Backups verificáveis com manifesto, tamanho, SHA-256 e retenção |
| `Logs/` | Diagnósticos sem conteúdo de snippets ou capturas |
| `update-state.json` | Estado da verificação de atualizações |
| `Updates/` | Arquivos temporários controlados da atualização |

As capturas, GIFs e MP4 ficam no destino escolhido pelo usuário. O histórico guarda metadados e caminhos, não uma segunda cópia do conteúdo.

Na primeira execução portátil, uma `SlashDeskData` válida ao lado do executável tem prioridade. Se ela não existir, dados legados de `%LocalAppData%\SlashDesk` podem ser copiados por staging, validados e ativados sem apagar a origem antiga.

A limpeza automática mantém os sete ZIPs mais recentes com nomes gerados pelo SlashDesk ou pelo antigo SlashText. Arquivos com nomes não reconhecidos ficam fora da limpeza. Os backups antigos `snippets-*.md` são preservados até remoção manual: o gravador atual não cria mais essas cópias, e elas podem conter versões únicas. Para recuperar uma versão, abra **Configurações → Importar atalhos → SlashDesk** e selecione o `.md` na pasta de backups. A importação permite revisar conflitos antes de confirmar.

A restauração valida o backup antes de tocar nos dados ativos, aplica tudo em staging e executa rollback completo em caso de falha. Em **Configurações**, a ação **Analisar imagens** localiza arquivos sem referência e só permite removê-los após revisão, confirmação e criação de um novo backup.

## Atualizações seguras

O SlashDesk consulta apenas as Releases estáveis oficiais deste repositório. Drafts e prereleases não são oferecidas no canal estável.

Na atualização portátil:

1. o ZIP e o arquivo `.sha256` oficiais são baixados;
2. nome, versão, arquitetura, conteúdo e SHA-256 são validados;
3. somente `SlashDesk.exe` é substituído;
4. a nova versão precisa confirmar a inicialização;
5. se houver falha, o executável anterior é restaurado.

A pasta `SlashDeskData` não é incluída no pacote e não é substituída pelo atualizador. A atualização real da 3.0.0 para a 3.1.0 foi validada preservando atalhos, categorias, configurações e histórico.

### Lembrar depois

Ao selecionar **Lembrar depois**, as verificações automáticas aguardam 24 horas antes de oferecer novamente a mesma versão. A partir da 3.2.0, o botão **Buscar atualizações** permite retomar a oferta manualmente durante esse período, sem remover `update-state.json` nem qualquer outro arquivo de `SlashDeskData`. A verificação automática também permanece ativa durante a sessão, respeitando cache, preferência e versões ignoradas.

## Privacidade

- atalhos, textos, imagens e gravações permanecem no computador;
- nenhuma conta é necessária;
- não há sincronização nem upload automático;
- a verificação de atualização consulta somente a API pública de Releases do GitHub;
- logs locais não incluem o conteúdo dos atalhos nem das capturas.

## Limitações do Windows

O Windows pode impedir capturas da área de trabalho segura, conteúdo protegido por DRM ou aplicativos executados com privilégios superiores aos do SlashDesk.

Captura com rolagem depende do comportamento do aplicativo de destino e pode repetir ou omitir trechos em páginas que não respondem corretamente à navegação automatizada.

## Verificar o download

A estável [v3.3.0](https://github.com/lucasllira/SlashText/releases/tag/v3.3.0)
inclui proveniência assinada do GitHub para o ZIP e o executável. A versão anterior 3.2.0
é anterior a esse fluxo. O roteiro de [verificação de proveniência](release-provenance.md)
confere repositório, workflow, tag e commit esperados. O atualizador do app mantém
as verificações de integridade; não verifica essa assinatura internamente.
Releases antigas sem atestado continuam identificadas como legadas.

Cada Release publica o ZIP e seu arquivo `.sha256`. No PowerShell:

~~~powershell
Get-FileHash .\SlashDesk-3.2.0-portable-win-x64.zip -Algorithm SHA256
~~~

O resultado deve ser igual ao hash informado na página da Release.

