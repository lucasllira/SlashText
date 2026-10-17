<p align="center">
  <img src="src/SlashText/Assets/SlashDesk.png" width="96" alt="Ícone do SlashDesk">
</p>

<h1 align="center">SlashDesk</h1>

<p align="center">
  Expansão de textos, Acento Rápido, capturas e gravações em um único aplicativo portátil para Windows.
</p>

<p align="center">
  <a href="https://github.com/lucasllira/SlashText/releases/latest"><img alt="Última versão" src="https://img.shields.io/github/v/release/lucasllira/SlashText?display_name=tag&sort=semver"></a>
  <a href="https://github.com/lucasllira/SlashText/actions/workflows/build.yml"><img alt="Build Windows" src="https://github.com/lucasllira/SlashText/actions/workflows/build.yml/badge.svg"></a>
  <a href="LICENSE"><img alt="Licença MIT" src="https://img.shields.io/badge/licença-MIT-089BB2"></a>
  <img alt="Windows x64" src="https://img.shields.io/badge/Windows-x64-0078D4">
</p>

<p align="center">
  <a href="https://github.com/lucasllira/SlashText/releases/latest"><strong>Baixar a versão mais recente</strong></a>
  ·
  <a href="https://github.com/lucasllira/SlashText/issues">Relatar problema</a>
  ·
  <a href="#desenvolvimento">Executar o projeto</a>
</p>

> O projeto permanece no repositório `SlashText` para preservar links e histórico. Desde a versão 2.0, o produto e o executável se chamam **SlashDesk**.

## Sobre

O SlashDesk é um utilitário local para Windows que reúne tarefas normalmente distribuídas entre vários aplicativos: atalhos de texto, caracteres acentuados, captura de tela e gravação.

Ele foi pensado para uso pessoal ou corporativo com três prioridades:

- funcionamento local, sem conta e sem upload obrigatório;
- distribuição portátil em um único executável;
- preservação dos dados do usuário durante atualizações.

A versão estável disponível está na [página de releases](https://github.com/lucasllira/SlashText/releases/latest).

A **3.3.0** reúne a nova interface completa, áudio nas gravações e idiomas
português, inglês e espanhol. Consulte as [notas da 3.3.0](docs/releases/3.3.0.md)
e o [registro de validação e publicação](https://github.com/lucasllira/SlashText/issues/70).
Os links de download acima apontam para a versão estável publicada.

## Principais recursos

| Área | O que oferece |
|---|---|
| **Atalhos de texto** | Expansões iniciadas por `/`, conteúdo formatado, variáveis, categorias, sugestões e estatísticas |
| **Acento Rápido** | Seleção de caracteres por tecla, conjuntos configuráveis, suporte a Caps Lock, Shift e layouts diferentes |
| **Captura** | Monitor, janela, região e captura longa adaptativa, com modos Direta/Editor, atalhos globais, histórico e salvamento configurável |
| **Gravação** | GIF e MP4 locais, seleção de alvo, presets, cursor, pausa, retomada, prévia e histórico |
| **Experiência** | Temas Claro, Preto e Seguir o Windows, bandeja, inicialização com o sistema e atualização automática segura |

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

A implementação da interface completa do Visual Lab faz parte da **3.3.0, em validação**.
A versão estável 3.2.0 mantém a fundação Fluent anterior. A nova interface preserva
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
4. Configure os atalhos globais na tela **Configurações**.
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

As próximas releases incluem proveniência assinada do GitHub para o ZIP e o
executável. O roteiro de [verificação de proveniência](docs/release-provenance.md)
confere repositório, workflow, tag e commit esperados. O atualizador do app mantém
as verificações de integridade; não verifica essa assinatura internamente.
Releases antigas sem atestado continuam identificadas como legadas.

Cada Release publica o ZIP e seu arquivo `.sha256`. No PowerShell:

~~~powershell
Get-FileHash .\SlashDesk-3.2.0-portable-win-x64.zip -Algorithm SHA256
~~~

O resultado deve ser igual ao hash informado na página da Release.

## Desenvolvimento

### Requisitos

- Windows x64;
- SDK .NET definido em [`global.json`](global.json), com a política de servicing documentada;
- PowerShell 7.2 ou superior; Python 3 para inventário e validações de segurança.

Consulte o [ambiente de desenvolvimento e build](docs/development.md) para versões,
recursos Noto/OCR, Visual C++ x64 e validação dos pacotes Portable/Installed.

### Executar

~~~powershell
dotnet restore SlashText.sln
dotnet run --project .\src\SlashText\SlashText.csproj
~~~

### Validar

O [fluxo de testes](docs/testing.md) oferece os perfis `Quick`, `Full` e
`Hardware`, com resumo estruturado, cancelamento e limites de isolamento.
Hardware e áudio exigem opt-in explícito; indisponibilidade não conta como aprovação.

~~~powershell
dotnet build SlashText.sln --configuration Release -p:Platform=x64
dotnet run --project .\tests\SlashText.SmokeTests\SlashText.SmokeTests.csproj --configuration Release -p:Platform=x64
.\scripts\compare-ui-inventory.ps1
.\scripts\ui-integrity-smoke.ps1
.\scripts\release-workflow-smoke.ps1
.\scripts\capture-toolbar-visual-contract-smoke.ps1
~~~

### Publicar o portátil

~~~powershell
dotnet publish .\src\SlashText\SlashText.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  -p:Platform=x64 `
  -p:PublishProfile=Portable `
  --output .\publish-portable
~~~

Os workflows do GitHub repetem build, testes, validações de UI, publicação self-contained e inicialização do executável em pasta limpa antes de gerar uma Release.

## Documentação técnica

- [Armazenamento e atualizações](docs/storage-and-updates.md)
- [Design system](docs/design-system.md)
- [Inventário funcional da interface 3.0.0](docs/ui-inventory-3.0.0.md)
- [Inventário de confiabilidade](docs/reliability-functional-inventory.md)
- [Contrato visual da barra de captura](docs/capture-toolbar-visual-contract.md)
- [Integração da barra de captura](docs/capture-toolbar-visual-integration.md)

## Versões

### 3.3.0 — em validação

- seis telas e superfícies auxiliares com visual consistente, temas e navegação por teclado;
- interface em português, inglês e espanhol, aplicada ao reabrir o aplicativo;
- gravação MP4 com áudio do computador e microfone selecionados explicitamente;
- Acento Rápido com linhas compactas, estado claro e controle pela bandeja;
- estatísticas locais, lista de atalhos virtualizada e melhorias na duração dos GIFs;
- reforços de importação, privacidade do clipboard, backup e recuperação de dados;
- avisos de terceiros offline e fluxo de proveniência para as distribuições oficiais.

Veja as [notas da versão 3.3.0](docs/releases/3.3.0.md). A validação do candidato e
a aprovação de publicação permanecem na [issue #70](https://github.com/lucasllira/SlashText/issues/70).

### 3.2.0

- captura longa adaptativa, modos Direta/Editor e ações completas na bandeja;
- melhor qualidade de GIF com paleta adaptativa;
- refinamentos de atalhos e Acento Rápido;
- atualização manual corrigida após **Lembrar depois** e monitor periódico;
- persistência atômica e preservação de arquivos corrompidos;
- backups verificáveis, restauração transacional e rollback;
- inicialização resiliente e limpeza manual segura de imagens órfãs;
- preservação da pasta `SlashDeskData` durante a atualização.

Veja as [notas completas da versão 3.2.0](https://github.com/lucasllira/SlashText/releases/tag/v3.2.0).

### 3.1.0

- captura corrigida para múltiplos monitores, DPI misto, coordenadas negativas e áreas úteis;
- nova barra Fluent de anotação;
- catálogo local de emojis Noto;
- maior confiabilidade na expansão de textos e coordenação das capturas;
- preservação de gatilhos legados incompatíveis;
- workspace de atalhos redimensionável;
- tema preto verdadeiro e fundação visual reutilizável;
- atualização validada a partir da 3.0.0 sem perda de dados.

Veja as [notas completas da versão 3.1.0](https://github.com/lucasllira/SlashText/releases/tag/v3.1.0).

### 3.0.0

- redesign funcional baseado na linha 2.9.1;
- navegação horizontal e paridade entre temas;
- preservação de atalhos, histórico e dados portáteis;
- atualização transacional com rollback.

## Próximos passos

- migrar as demais telas para o contrato visual completo do Visual Lab;
- refinar animações, acessibilidade e responsividade sem remover funções existentes.

## Licença

Distribuído sob a [licença MIT](LICENSE).

Os [avisos e licenças de terceiros](THIRD-PARTY-NOTICES.md) acompanham o aplicativo
e podem ser consultados offline em **Sobre → Avisos de terceiros**, nos três
idiomas da interface. Os textos originais das licenças são preservados.
