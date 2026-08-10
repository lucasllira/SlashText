# SlashDesk — Especificação visual V2

## Objetivo e limite

Esta referência cobre somente o shell principal e a tela Atalhos. Não define mudanças visuais para Acento Rápido, Captura, Estatísticas, Configurações, Sobre, diálogos, overlays, onboarding, editor de captura ou bandeja.

O protótipo é uma fonte mensurável, não uma especificação de arquitetura. A implementação deve evoluir o design system já existente em `Styles/Foundation.xaml`, recursos semânticos e `ThemeService`.

## Geometria de referência

| Elemento | Medida |
| --- | ---: |
| Viewport principal | 1440 × 900 px |
| Tamanho mínimo a preservar | 980 × 680 px |
| Title bar | 34 px |
| Header | 66 px |
| Navegação horizontal | 52 px |
| Status bar | 28 px |
| Margem lateral principal | 32 px |
| Margem vertical do workspace | 20 px |
| Coluna esquerda | 280 px |
| Editor central | flexível; 680 px na referência |
| Coluna direita | 384 px |
| Gap entre colunas | 16 px |
| Raio dos painéis | 12 px |
| Raio dos campos/botões | 7–9 px |

Escala de espaçamento: `4, 6, 8, 12, 16, 20, 24, 32` px. Os valores 6 e 20 são composições locais; os tokens estruturais principais continuam 4, 8, 12, 16, 24 e 32.

## Grid WPF sugerido

```text
Window
├─ Row 34: TitleBar
├─ Row 66: Header
├─ Row 52: TopNavigation
├─ Row *: Workspace
│  └─ Grid columns: 280 | 16 gap | minmax(520, *) | 16 gap | 384
└─ Row 28: StatusBar
```

Em 980 × 680, reduza as margens laterais para 20 px, os gaps para 12 px e permita que a coluna direita use aproximadamente 280 px. Não esconda ações nem estruturas. Use rolagem vertical dentro de listas/painéis; não introduza corte horizontal oculto.

## Tipografia

- Família WPF: `Segoe UI Variable`, fallback `Segoe UI`.
- Título de tela: 21 px, Semibold.
- Título de painel/editor: 16 px, Semibold.
- Texto de corpo: 11–12 px.
- Rótulo de campo: 10 px, Semibold.
- Eyebrow/seção: 9–10 px, Bold, tracking de 0,7–0,9 px.
- Código/gatilho/variável: `Cascadia Mono`, fallback `Consolas`, 9–10 px.

## Tokens semânticos

### Tema claro

| Token | Valor |
| --- | --- |
| Accent | `#089BB2` |
| AccentStrong | `#087F95` |
| AccentSoft | `#E7F8FB` |
| AccentBorder | `#9BDBE4` |
| WindowBackground | `#F8FAFB` |
| Surface | `#FFFFFF` |
| SurfaceSubtle | `#F4F7F8` |
| SurfaceHover | `#EDF4F6` |
| TextPrimary | `#14232B` |
| TextSecondary | `#64747C` |
| TextTertiary | `#8A989F` |
| Border | `#D7E0E4` |
| BorderStrong | `#C4D1D6` |
| Success | `#19A66A` |
| Warning | `#D89024` |
| Danger | `#C84E5B` |

### Tema escuro

| Token | Valor |
| --- | --- |
| Accent | `#30C8DF` |
| AccentStrong | `#72D9E8` |
| AccentSoft | `#0C343D` |
| AccentBorder | `#1D6070` |
| WindowBackground | `#10191E` |
| Surface | `#152127` |
| SurfaceSubtle | `#111C21` |
| SurfaceHover | `#1B2B32` |
| TextPrimary | `#EDF6F8` |
| TextSecondary | `#9BABB2` |
| TextTertiary | `#708087` |
| Border | `#293940` |
| BorderStrong | `#3A4B52` |
| Success | `#42D894` |
| Warning | `#F0AD4F` |
| Danger | `#FF7F8D` |

## Estrutura obrigatória da coluna esquerda

As quatro estruturas abaixo são independentes e devem permanecer simultaneamente visíveis:

1. Busca geral por nome, conteúdo, gatilho e categoria suportada.
2. Categorias com “Todos”, categorias persistidas, contadores e estado selecionado.
3. Filtro de exibição com “Todos” e “Mais utilizados”.
4. Lista completa de atalhos filtrada, com total, rolagem, seleção, nome, gatilho e categoria.

“Mais utilizados” nunca substitui categorias ou a lista. Os seis itens do protótipo são apenas dados demonstrativos.

## Estados de componentes

| Estado | Regra |
| --- | --- |
| Default | Superfície neutra e borda semântica |
| Hover | `SurfaceHover`; não alterar geometria |
| Selected | `AccentSoft` + `AccentBorder`; lista usa barra interna de 3 px |
| Focus | `Accent` + halo externo de 3 px com 13–15% de opacidade |
| Pressed | AccentStrong ou superfície um nível mais escura |
| Disabled | 45% de opacidade; cursor padrão; sem hover |
| Error | Danger em borda/rótulo; mensagem textual próxima ao campo |
| Unsaved | Ponto + texto Warning no cabeçalho do editor |

## Rolagem e responsividade

- A lista de atalhos possui rolagem própria.
- A coluna de variáveis pode rolar verticalmente.
- Editor mantém toolbar, conteúdo, preview e ações acessíveis; conteúdo recebe a maior parcela flexível.
- No tamanho mínimo, preserve títulos e ações; reduza espaçamentos antes de reduzir tipografia.
- Não empilhe as colunas sem confirmar que a arquitetura WPF atual permite isso.
- Ações primárias não podem desaparecer fora da área visível.

## Critérios visuais de aprovação

- Navegação horizontal e seis abas na ordem atual.
- Layout em três colunas, sem menu lateral global.
- Categorias, exibição e atalhos claramente separados.
- Tema escuro sem áreas brancas e com cursor/foco visíveis.
- Tema claro sem sombras pesadas.
- Item e categoria selecionados evidentes sem depender apenas de cor.
- Toolbar integrada ao editor.
- Rodapé de ações alinhado e estável.
- Nenhum texto cortado, sobreposição ou node equivalente “solto”.
