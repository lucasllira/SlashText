# Responsabilidades da janela principal e dos smokes

Mapa da organização introduzida na atividade 2 da #159. O aplicativo continua em WPF, com os mesmos serviços e arquivos parciais de `MainWindow`. A separação é por responsabilidade; não adiciona uma camada de arquitetura ou altera contratos públicos.

## Aplicativo

Todos os caminhos abaixo são relativos a `src/SlashText/`.

| Arquivo | Responsabilidade |
|---|---|
| `MainWindow.xaml` | Controles, namescope e ligações de eventos. |
| `MainWindow.xaml.cs` | Composição dos serviços, campos compartilhados, construtor, carregamento inicial e encerramento/descarte. Os inicializadores de campos foram mantidos no lugar. |
| `MainWindow.Shell.cs` | Navegação entre telas, cabeçalho, estado da janela, layout responsivo e divisores de Atalhos. |
| `MainWindow.Input.cs` | Coordenação dos eventos de expansão, sugestões e inserção pelo Acento Rápido. |
| `MainWindow.Shortcuts.Editor.cs` | Busca/categorias, seleção, salvamento/exclusão, formatação e prévia do editor. Complementa `MainWindow.Shortcuts*.cs`, que já contém rascunho, lista virtualizada e extras. |
| `MainWindow.Settings.Transfers.cs` | Preferências gerais/tema, importação de atalhos, backup/restauração e análise de imagens. Complementa `MainWindow.Settings.cs` e os partials de armazenamento/recuperação. |
| `MainWindow.Capture.Settings.cs` | Carregamento/leitura dos controles de Captura, presets e configuração dos atalhos globais. A persistência com rollback continua em `MainWindow.CapturePreferences.cs`. |
| `MainWindow.Capture.Launcher.cs` | Seleção de mídia/alvo, resumo, ajuda e encaminhamento da ação escolhida. |
| `MainWindow.Capture.Execution.cs` | Execução de captura de imagem, rolagem, MP4 e GIF. A reserva e o encerramento da operação continuam em `MainWindow.RecordingOperation.cs` e `MainWindow.RecordingRecovery.cs`. |
| `MainWindow.Capture.Workbench.cs` | Sessão de edição, ferramentas, zoom, expansão, cópia e salvamento da imagem. |
| `MainWindow.Capture.History.cs` | Filtros, cartões/miniaturas, navegação e ações do histórico. A proteção dos dados continua em `MainWindow.CaptureHistoryTrust.cs`. |
| `MainWindow.Updates.cs` | Consulta manual/periódica, oferta, download e aplicação de atualização. |
| `MainWindow.Tray.cs` | Inicialização, menu, notificações e estado das ações da bandeja. |
| `MainWindow.QuickAccent.cs` | Controles, conjuntos, prévia e estado do Acento Rápido. |

Os partials de Sobre, Estatísticas, idiomas e auxiliares continuam com suas responsabilidades existentes. Serviços em `Services/` executam as regras e operações; os arquivos acima coordenam a interface. Métodos privados podem ser chamados por outros partials da mesma classe.

Ao alterar um handler, procure seu nome em todos os `MainWindow.*.cs` e no XAML. Não duplique um método no arquivo principal apenas para satisfazer uma busca textual. Os contratos de interface/inventário inspecionam o conjunto de partials. Helpers de produção/fixture em `Design/` não foram movidos.

## Testes

Todos os caminhos abaixo são relativos a `tests/SlashText.SmokeTests/`.

- `Program.cs` somente encaminha os argumentos e, quando nenhum modo específico corresponde, executa a suíte padrão.
- `SmokeCommandLine.cs` conserva os modos, condições de argumentos, workers e retornos antecipados existentes. Um argumento desconhecido continua seguindo o comportamento anterior: execução da suíte padrão.
- `SmokeSuite.cs` mostra a ordem completa dos domínios, as inicializações de `AppPaths` e o `try/finally` de limpeza da fixture principal. A execução continua sequencial; não paralelizar domínios que compartilham `AppPaths`.
- `SmokeSuite.InputAndTemplates.cs` e `SmokeSuite.Layout.cs` cobrem templates, teclado/Acento e cálculos de posicionamento.
- `SmokeSuite.CaptureEditor.cs`, `SmokeSuite.CaptureSettings.cs`, `SmokeSuite.Rendering.cs`, `SmokeSuite.Recording.cs` e `SmokeSuite.Gif.cs` cobrem editor/renderização, preferências e backends fictícios. O teste de parada não confirmada permanece no fim da sequência, após atualização.
- `SmokeSuite.RepositoriesAndBackups.cs`, `SmokeSuite.Shortcuts.cs`, `SmokeSuite.Storage.cs`, `SmokeSuite.Updates.cs` e `SmokeSuite.Assets.cs` agrupam persistência/restauração, formatação, migração/escrita atômica, atualização e manutenção de imagens.
- `SmokeSuite.Assertions.cs` contém assertions compartilhadas. `SmokeSuite.Updates.Fixtures.cs`, `FakeUpdateHttpHandlers.cs` e `FakeRecorderBackend.cs` contêm dados/transportes/backends fictícios; não iniciam gravação nativa.
- Os arquivos `*Checks.cs` existentes continuam sendo chamados na mesma ordem e pelos mesmos modos. A thread STA do workbench e o worker isolado de clipboard continuam nas fronteiras anteriores.

O engine e a data fixa usados em mais de um domínio são parâmetros explícitos. Variáveis exclusivas de um domínio permanecem locais; não foram transformadas em estado estático compartilhado. Recursos `using` locais são liberados ao concluir seu domínio, antes de seguir ao próximo. A limpeza externa e a ordem das operações permanecem no orquestrador.

## Validação e limites

Use os perfis e critérios de resultado de [Execução de testes](testing.md). Para esta separação, os contratos rápidos conferem inventário/handlers e recursos, e o perfil Full exercita domínio, fixtures WPF e OCR offline. Indisponibilidade da estação privada de clipboard continua sendo `UNAVAILABLE`, sem acesso alternativo ao clipboard pessoal.

Antes de integrar uma mudança desse tipo, conferir métodos/assinaturas e inicializadores, sequência dos cenários, argumentos dos workers, descarte e resultados reais. Manter as proteções de publicação e os testes de pacote nos workflows próprios. Esta reorganização tem novos bytes de aplicativo e exige novo candidato na #70 antes da versão estável; a RC1 anterior não contém a refatoração.
