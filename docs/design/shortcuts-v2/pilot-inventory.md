# SlashDesk — piloto Shell + Atalhos V2

## Base e escopo

- Base: `main` em `7b705bbec71be43877e1bb758a6d5c114de1bc61`.
- Branch: `agent/slashdesk-shell-shortcuts-v2`.
- Versão preservada: `3.0.0`.
- Escopo visual: shell principal, title bar, header, navegação horizontal e tela Atalhos.
- Fora do escopo: Acento Rápido, Captura, Estatísticas, Configurações, Sobre, overlays, diálogos e bandeja.

## Inventário antes/depois

| Item | Antes | Depois | Diferença |
|---|---:|---:|---:|
| Controles nomeados no `MainWindow` | 129 | 139 | +10 |
| Handlers XAML únicos | 56 | 63 | +7 |
| Controles removidos | — | 0 | 0 |
| Handlers removidos | — | 0 | 0 |

Controles acrescentados: `DisplayAllButton`, `DisplayMostUsedButton`, `ShellCollectionStatusText`, `ShellPageDescription`, `ShellPageTitle`, `ShellStatusText`, `ShortcutHeaderActions`, `SnippetCountText`, `SnippetListPanel` e `WorkspaceHost`.

Handlers acrescentados: `CloseWindow_OnClick`, `DisplayAll_OnClick`, `DisplayMostUsed_OnClick`, `EditorField_OnTextChanged`, `MaximizeWindow_OnClick`, `MinimizeWindow_OnClick` e `TitleBar_OnMouseLeftButtonDown`.

## Estrutura funcional preservada

| Área | Implementação real preservada |
|---|---|
| Dados | `SnippetMarkdownRepository` e `snippets.md` |
| Uso | `UsageService`, sem duplicar ou alterar estatísticas |
| Importação | `SnippetImportService`: SlashDesk, Text Blaze e Espanso |
| Conteúdo | `RichTextMarkdownConverter`, texto simples/formatado, hyperlinks, imagens, listas, tabelas e alinhamento |
| Variáveis | `TemplateEngine`, variáveis automáticas, preenchíveis e `{tab}` |
| Expansão | `KeyboardHookService` e `TextExpansionService` |
| Persistência | mesma coleção, IDs, categorias e formato existentes |

## Diferenças entre handoff e código real

- O modelo atual não possui persistência de “aplicativos por snippet”. Nenhum campo fictício foi criado; o fluxo real de escopo/expansão foi preservado.
- “Mais utilizados” deixou de ser uma lista paralela limitada a três itens e passou a ser filtro da lista completa, usando os contadores reais de `UsageService`.
- A busca existente por nome, gatilho e categoria foi ampliada para conteúdo; os resultados continuam usando a coleção real.
- Categorias persistidas, filtro de exibição e lista de atalhos agora são estruturas independentes e simultaneamente visíveis.

## Medidas e tokens WPF

| Handoff | Recurso/estrutura WPF |
|---|---|
| Title bar 34 px | `WindowChrome` + primeira linha do shell |
| Header 66 px | `AppShellHeader` |
| Navegação 52 px | `AppNavigationBar` |
| Margem 32 px | `AppWorkspace`; reduzida responsivamente antes da tipografia |
| 280 / flex / 384 px | colunas nomeadas de `ShortcutsView` |
| Gaps 16 px | colunas divisoras transparentes |
| Espaços 4/6/8/12/16/24/32 | `Space.*` em `Foundation.xaml` |
| Raios 8/12 px | `Radius.Control` e `Radius.Large` |
| Segoe UI Variable | `FontFamily.Body` e `FontFamily.Display` |
| Cascadia Mono/Consolas | `FontFamily.Mono` |
| Cores claro/escuro | recursos semânticos aplicados por `ThemeService` |

## Responsividade

- A partir de 1180 px, aplica as larguras de referência.
- Entre 1080 e 1179 px, reduz margens, gaps e colunas laterais.
- Abaixo de 1080 px, reutiliza o comportamento responsivo já existente: editor rolável acima e navegação/variáveis abaixo, sem ocultar ações.
- O mínimo de janela continua em 980 × 680.
