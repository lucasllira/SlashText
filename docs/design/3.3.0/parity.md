# Matriz de paridade — base 3.2.0 → Visual Lab 11

Base auditada: `406022943ab13d0d78399b2a797210ae5106b0e3`. Inventário mecânico em `ui-inventory.json`: nomes, eventos XAML, bindings, views auxiliares, métodos e locais de eventos dinâmicos. Linhas dos eventos dinâmicos são localizadores da base, não um contrato estável. A tabela abaixo complementa o inventário com a destinação funcional. Cada dono deve revisar os callbacks de suas fábricas de controles, não somente o XAML.

| Recurso real / entrada principal | Correspondência no Lab | Destino WPF / responsabilidade | Preservação e estados |
|---|---|---|---|
| `ShowView`, seis `*Tab_OnClick`, `MainWindow_OnSizeChanged`, `NormalizeShortcutColumns` | `.s-window`, `.s-appnav`, `.s-page` | Shell existente, #63; splitters #64 | Atalhos, Captura, Acento Rápido, Estatísticas, Configurações, Sobre. Janela atual mín. 980×680; testar menor área cliente efetiva e conteúdo com scroll. |
| `TitleBar_OnMouseLeftButtonDown`, minimizar/maximizar/fechar, `MainWindow_OnClosing` | `.s-titlebar` | WindowChrome/handlers existentes, #63 | Arrastar, duplo clique, maximizar, fechar para bandeja, estado maximizado. Não simular botões do Lab. |
| `SearchBox_OnTextChanged`, `CreateCategoryButton`, `CreateSnippetButton`, filtros/mais usados | `.s-snippet-sidebar`, categorias e lista | `ShortcutsView`, #64 | Busca, categorias, contagens, seleção, filtros, vazio e dados numerosos. |
| `BeginNewSnippet`, `SaveSnippet_OnClick`, `DeleteSnippet_OnClick`, `EditorField_OnTextChanged` | Editor de Atalhos | Editor nativo, #64 | Novo/editar/excluir, validação, categoria, dirty-state, salvar/cancelar. Não perder legados com `:`. |
| `ContentEditor`, `FormatBox`, negrito/itálico/sublinhado, fonte/tamanho, cores, marca-texto, listas, alinhamento, tabela/link/imagem | `.s-richbar` simplificada | RichTextBox e conversores atuais, #64 | Manter todas as funções existentes mesmo sem equivalente completo no Lab. Preservar imagens/assets e Markdown. |
| `VariableChip_OnClick`, `TemplateEngine`, `PreviewViewer`, `VariableInputWindow` | `.s-variables` / prévia ilustrativa | Editor #64 e diálogo #69 | Campos, defaults, datas, cálculos e tabulação reais. Não copiar sintaxe demonstrativa incompatível. |
| `KeyboardHookService`, `TextExpansionService`, `TriggerRule`, `SuggestionWindow` | Fora da página simulada | Serviços preservados; barra #69 | Somente `/`, `?` e `:` não disparam; atalhos legados preservados inativos. Expansão global, seleção, cancelamento, Unicode, clipboard e foco. |
| `QuickAccentSettings_OnChanged`, `QuickAccentDelaySlider_OnValueChanged`, conjuntos/preview | `.s-accent-preview`, `.s-character-bar`, cards | `QuickAccentView`, #65 | Timer 100/200 ms, sem depender do auto-repeat, conjuntos e exclusões; layout US/PT-BR, Shift/Caps; campos internos. Barra flutuante #69. |
| `CaptureActiveMonitor_OnClick`, `CaptureRegion_OnClick`, `CaptureWindow_OnClick`, `CaptureScrolling_OnClick` | `.s-mode-grid` | `CaptureView`, #63 | Monitor/Região/Janela/Longa independentes, botões + teclado/roda/mouse, atraso e exclusão mútua. |
| `RunCaptureAsync`, `RunScrollingCaptureAsync`, `CaptureService` | Simulação estática de captura | Serviços reais mantidos, #63 | Direta/Editor, cursor, ocultação, detecção de fim/duplicação e limites da longa, cancelamento. Não copiar defaults do Lab. |
| `BrowseCaptureDestination_OnClick`, `SaveCaptureSettings_OnClick`, `CapturePathResolver` | Campos de destino/nome/formato | `CaptureView`, #63 | Pasta nativa, variáveis de arquivo, formato/qualidade, salvar/copiar, validação e persistência. |
| `RegionCaptureWindow`, `RegionSelectionWindow`, `CaptureEditorWindow`, `CaptureToolbarState` | `.cs-stage`, `.cs-toolbar`, `.cs-properties` | Overlay/barra reais, #63 | Seleção/resize, seta, formas, texto, numeração, emoji Noto, lápis, marca-texto, cores/preenchimento/espessura/opacidade, undo/redo. DPI misto, coordenadas negativas/cantos. |
| `StartGifRecording_OnClick`, `GifRecordingService`, `GifPreviewWindow` | GIF demonstrativo | Captura #63; prévia #69 | Região, 10/20/30 FPS, paleta adaptativa, qualidade real, preview/cancelar/salvar. |
| `StartMp4Recording_OnClick`, `ScreenRecordingService`, `RecordingControlWindow` | Gravação e prévia simuladas | Gravação #63; controles #69 | Alvo monitor/janela/região, presets/cursor, pausar/retomar/finalizar, falha e backend nativo. MP4 continua acessível. |
| `RefreshCaptureHistory`, filtros e abrir/copiar/editar/excluir/limpar/abrir pasta | Histórico com dados fictícios | Histórico real, #63 | Tipo, filtros, metadados, links corretos, confirmação, arquivos ausentes, retenção e vazio. |
| `ShowTrayBalloon`, `OpenPendingCaptureNotification`, `InitializeTray`, `ApplyTrayItemsTheme` | `.s-tray`, `.s-toast` | WinForms/bandeja e notificações, #69 | Grupos Capturas/Gravações/Atalhos/Aplicativo, submenus recursivos, clique abre imagem; toast Windows tem limites de aparência. |
| `RefreshStatistics`, `UsageService` | `.s-metrics` e gráfico de exemplo | `StatisticsView`, #66 | Apenas dados existentes; zero/erro/módulo indisponível. Não inventar série temporal, exportação ou histórico que não existe. |
| `Settings_OnClick`, `ThemeBox_OnSelectionChanged`, `StartupService` | `.s-settings-grid`, `.s-theme-cards` | `SettingsView`, #67 | Preferências atuais, início com Windows, tema Claro/Preto/Sistema, validação. Nada de localStorage. |
| `ImportSnippets_OnClick`, `SnippetImportService` | Diálogo de importação simplificado | Importação real, #67 | Markdown SlashDesk, JSON Text Blaze, YAML Espanso, conflitos/conversão/avisos, cancelamento. |
| `CreateBackup_OnClick`, `RestoreBackup_OnClick`, `AnalyzeAssets_OnClick`, `BackupService` | Backups e análise simulados | Controles #67; diálogos #69 | Manifesto/tamanho/SHA, backups legados, staging/rollback, confirmação, imagens órfãs e backup obrigatório antes de remover. |
| `StartupModuleCoordinator`, proteção de snippets, `EnsureSnippetStorageAvailable` | Não representado | Avisos e modos reduzidos #67/#69 | Erros de leitura não apagam dados; restauração ZIP disponível; módulos opcionais falham sem derrubar app. |
| `CheckUpdates_OnClick`, `StartUpdateMonitor`, `OfferUpdateAsync`, `PortableUpdateService` | Atualização simulada | Preferências #67; diálogos #69 | Manual após Lembrar depois, monitor periódico, consentimento/cancelamento/progresso/erro, SHA, rollback. #37 permanece independente até teste real. |
| `ProductVersion`, `OpenGitHub_OnClick`, `ReleaseNotes_OnClick` | `.s-about-hero`, links | `AboutView`, #68 | Versão obtida do assembly e URLs reais. Não hardcode 3.3.0 nem texto fictício. |
| `PromptDialog`, `OnboardingWindow`, `UpdateAvailableWindow`, `UpdateProgressWindow`, `VariableInputWindow` | `.s-dialog` apenas parcialmente | Diálogos nativos, #69 | Owner/modalidade, Escape, foco retornado, validação, cancelamento e contratos atuais. |

## Componentes e conflitos resolvidos nesta fundação

| Existente | Novo componente explícito | Decisão |
|---|---|---|
| `Styles/Foundation.xaml`: `FontSize.*`, `Radius.*`, `Motion.*` | `Styles/VisualLab/Tokens.xaml`: `Lab.*` | Convivem. Não reconfigurar estilos legados por chave global. |
| `App.xaml` estilos implícitos de Button/TextBox/ComboBox/TextBlock | `Lab.Button`, `Lab.PrimaryButton`, `Lab.Field`, `Lab.Combo`, `Lab.Text` | Somente consumidor explícito recebe o novo template. Galeria possui alias local de TextBlock; nenhum alias global novo. |
| `Styles/Components.xaml`: `ToggleSwitch` | `Lab.Switch` | Switch WPF CheckBox, mesma semântica de estado; 39×21 DIP e deslocamento 18 DIP do CSS. |
| `AppNavigationButton` / `NavButton` | `Lab.Tab` | RadioButton com seleção/teclado nativos. Integração com ShowView pertence à #63. |
| `SectionCard`, `SettingsCard` | `Lab.Card`, `Lab.Popup` | Composição Border; Popup nativo preserva owner/teclado. |
| `ThemeService.Apply` | `LabPalette.Apply` | Atualiza apenas chaves Lab.* além da paleta antiga. Galeria tem escopo local e monitora o tema Windows; integração completa do shell/sistema fica #63/#67. |
| Ícones próprios em `Styles/Icons.xaml` e emoji Noto | `LabIcon` + `VisualLab/Icons.xaml` | 54 vetores Lucide da publicação, viewport 24×24, licença embutida. Não substituir os emojis ou instrumentos existentes por ausência no Lab. |

## Cobertura e limites

Todo controle nomeado/evento XAML está vinculado à view/issue no JSON. Métodos compartilhados, fábricas de botões, eventos dinâmicos e janelas C# são entradas de revisão obrigatórias: o inventário estático não substitui teste de comportamento. A ausência de um controle no Lab é uma lacuna do protótipo, nunca autorização para removê-lo.
