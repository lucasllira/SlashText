# Inventário funcional — confiabilidade (base do PR #25)

Base remota: `agent/slashdesk-shortcuts-v2-splitters` em `5839cadda97c780b00338ac3e3011b99282d0934`.
Branch funcional: `agent/slashdesk-functional-reliability`.

## Fluxos e responsáveis antes das alterações

| Fluxo | Arquivos e serviços | Comportamento inicial | Cobertura inicial |
| --- | --- | --- | --- |
| Seleção e anotação de região | `Views/RegionCaptureWindow.cs`, `Services/CaptureService.cs` | A barra é centralizada na seleção e limitada pela janela que cobre o desktop virtual. Não consulta a área útil do monitor da seleção. | Smoke estrutural; sem casos de cantos, taskbar, DPI ou coordenadas negativas. |
| Detecção de atalhos | `Services/KeyboardHookService.cs` | Mantém buffer por janela e reconhece confirmação por Enter, Tab e Space. | Tradução ABNT e persistência básica de `/` e `:`. |
| Expansão | `Services/TextExpansionService.cs`, `Views/VariableInputWindow.cs`, `MainWindow.xaml.cs` | Recupera foco uma vez, apaga o tamanho do gatilho cadastrado, cola segmentos e envia Tab com atrasos fixos. Não há single-flight nem validação contínua do alvo. | Template `{{tab}}`; sem SendInput, foco, clipboard ou concorrência. |
| Sugestões | `Views/SuggestionWindow.cs`, `Services/KeyboardHookService.cs`, `MainWindow.xaml.cs` | Janela somente informativa, sem seleção, navegação ou clique. | Nenhuma cobertura comportamental. |
| Validação de gatilhos | `SnippetMarkdownRepository.cs`, `SnippetImportService.cs`, editor em `MainWindow.xaml.cs` | Regex independentes aceitam conjuntos diferentes do monitor. | Casos básicos; sem limites, conflitos por prefixo ou regra compartilhada. |
| Atalhos de captura | `Services/GlobalCaptureShortcutService.cs` | Hotkeys sem `MOD_NOREPEAT`; mouse sem debounce. | Parse e formatação somente. |
| Coordenação de captura | `MainWindow.xaml.cs`, `Services/CaptureService.cs` | Botões, hotkeys e mouse podem iniciar fluxos concorrentes. O modo hotkey não aplica a ocultação configurada. | Nenhuma cobertura de concorrência ou restauração. |

## Handlers e serviços afetados

- `KeyboardHook_OnSuggestionsChanged`
- `KeyboardHook_OnExpansionRequested`
- `CaptureShortcuts_OnTriggered`
- `CaptureActiveMonitor_OnClick`
- `CaptureRegion_OnClick`
- `CaptureWindow_OnClick`
- `CaptureScrolling_OnClick`
- `RunCaptureAsync`
- `RegionCaptureWindow.PositionToolbar`
- `KeyboardHookService`
- `TextExpansionService`
- `GlobalCaptureShortcutService`
- `SuggestionWindow`

O piloto visual mantém 64 handlers reconhecidos pelo inventário do PR #25. Esta entrega não remove handlers, bindings ou controles visuais do piloto.

## Lacunas que continuam exigindo ambiente real

- Salesforce em Edge e Chrome, incluindo páginas pesadas e `contenteditable`.
- Hook global de teclado e mouse em sessão interativa.
- Clipboard com formatos pertencentes a outros aplicativos.
- Múltiplos monitores reais, coordenadas negativas e DPI misto.
- Verificação visual da barra em taskbars nas quatro bordas.

