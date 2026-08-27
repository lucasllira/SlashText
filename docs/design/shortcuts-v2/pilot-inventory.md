# SlashDesk — piloto Shell + Atalhos V2

## Base e escopo

- Base: `main` em `7b705bbec71be43877e1bb758a6d5c114de1bc61`.
- Branch desta correção: `agent/slashdesk-shortcuts-v2-splitters`.
- Versão preservada: `3.0.0`.
- Escopo visual: shell principal, title bar, header, navegação horizontal e tela Atalhos.
- Fora do escopo: Acento Rápido, Captura, Estatísticas, Configurações, Sobre, overlays, diálogos e bandeja.

## Inventário antes/depois

| Item | Antes | Depois | Diferença |
|---|---:|---:|---:|
| Controles nomeados no `MainWindow` | 139 | 140 | +1 |
| Handlers XAML únicos | 63 | 64 | +1 |
| Controles removidos | — | 0 | 0 |
| Handlers removidos | — | 0 | 0 |

Controle acrescentado nesta correção: `ShortcutEditorColumn`, necessário para aplicar o limite responsivo do editor sem alterar os painéis funcionais.

Handler acrescentado ao inventário XAML: ciclo compartilhado dos divisores de coluna. Os métodos `ShortcutSplitter_OnPreviewKeyDown`, `ShortcutSplitter_OnMouseDoubleClick`, `ShortcutSplitter_OnDragStarted` e `ShortcutSplitter_OnDragCompleted` são reutilizados pelos dois divisores.

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
| Gaps 16 px | colunas de 16 px com `GridSplitter` central de 8 px |
| Espaços 4/6/8/12/16/24/32 | `Space.*` em `Foundation.xaml` |
| Raios 8/12 px | `Radius.Control` e `Radius.Large` |
| Segoe UI Variable | `FontFamily.Body` e `FontFamily.Display` |
| Cascadia Mono/Consolas | `FontFamily.Mono` |
| Cores claro/escuro | recursos semânticos aplicados por `ThemeService` |

## Responsividade

- A partir de 1180 px, restaura 280 / flexível / 384 px.
- Entre 1051 e 1179 px, usa 260 / flexível / 280 px e editor mínimo de 440 px.
- Até 1050 px, usa 240 / flexível / 240 px e editor mínimo de 420 px.
- O mínimo de janela continua em 980 × 680, com as três colunas simultaneamente visíveis.
- Os limites são 220–460 px à esquerda e 240–480 px à direita; o algoritmo recalcula os máximos ao redimensionar a janela e reduz os painéis laterais antes do editor.
- Mouse move as divisórias ao vivo. Teclas ←/→ usam passos de 16 px; `Home` e clique duplo restauram o padrão responsivo.
- As larguras não são persistidas: permanecem apenas durante a sessão para não introduzir alteração no formato de configurações.

## Validação do protótipo atualizado

- busca por nome, conteúdo ou gatilho: validada;
- categorias e contadores: validados como estrutura independente;
- filtro `Mais utilizados`: validado sem substituir categorias ou lista;
- seleção atualizando o editor: validada;
- tema claro/escuro: validado;
- divisores por teclado, passos de 16 px e restauração: validados.

