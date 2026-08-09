# SlashDesk Design System

Versão do guia: 2.0
Aplicação inicial: SlashDesk 2.8.1
Redesign funcional validado: SlashDesk 3.0.0

Este documento é a referência visual e de implementação do SlashDesk. Novas telas
devem ser compostas com os tokens e estilos existentes. Um estilo local só deve ser
criado quando representar um estado ou componente realmente novo e reutilizável.

## 1. Princípios

1. **Local e direto:** a interface deve parecer uma ferramenta nativa do Windows,
   rápida e confiável.
2. **Compacto, não apertado:** priorizar informação útil e reduzir grandes áreas
   vazias, sem diminuir a área mínima de interação.
3. **Uma superfície por função:** evitar “card dentro de card”. Use bordas e
   superfícies apenas para separar grupos funcionais.
4. **Destaque com propósito:** o ciano identifica ação principal, foco, seleção e
   estado ativo. Ele não deve decorar superfícies sem função.
5. **Paridade de temas:** claro e escuro têm a mesma hierarquia e contraste; nenhum
   componente pode simplesmente forçar a aparência do outro tema.
6. **Função antes de animação:** movimentos devem explicar mudança de estado e não
   atrasar o fluxo.

## 2. Decisões fixas do produto

- navegação principal horizontal;
- identidade ciano;
- temas Claro, Escuro e Seguir o Windows;
- tipografia Segoe UI Variable, com fallback Segoe UI;
- Cascadia Mono/Consolas apenas para comandos e variáveis;
- shell compacto, conteúdo denso e poucos níveis de superfície;
- overlays de captura e gravação usam os mesmos tokens sem depender visualmente da
  janela principal;
- nenhuma alteração visual pode remover handlers, controles ou funções existentes;
- Atalhos é a primeira tela de referência para novos componentes;
- gravação de tela usa este sistema, não estilos paralelos;
- animações não essenciais permanecem desativadas para respeitar redução de movimento.

## 3. Cores semânticas

As telas usam somente chaves semânticas. Os valores são aplicados pelo
`ThemeService`.

| Token | Claro | Escuro | Uso |
|---|---:|---:|---|
| `CanvasBrush` | `#EEF2F4` | `#071018` | fundo da janela |
| `SurfaceBrush` | `#FCFDFC` | `#0C161F` | superfície principal |
| `ElevatedBrush` | `#F4F7F8` | `#111E28` | grupo secundário |
| `PanelBrush` | `#F8FAFA` | `#0F1A23` | barras e painéis |
| `ChromeBrush` | `#EDF2F4` | `#13212B` | áreas técnicas/preview |
| `InputBrush` | `#FFFFFF` | `#09131B` | campos editáveis |
| `InkBrush` | `#15212B` | `#F3F6F8` | texto principal |
| `MutedBrush` | `#65737E` | `#9AABB7` | texto secundário |
| `DividerBrush` | `#D5DEE3` | `#243642` | bordas e divisores |
| `AccentBrush` | `#079FB2` | `#26C6D8` | ação/foco/seleção |
| `AccentSubtleBrush` | `#E0F6F8` | `#10333D` | fundo de destaque |
| `HoverBrush` | `#E8EFF1` | `#172630` | hover |
| `SelectedBrush` | `#DDF5F8` | `#113944` | item selecionado |
| `DangerBrush` | `#C63C4A` | `#FF7D89` | ação destrutiva |
| `SuccessBrush` | `#188A62` | `#4FD7A5` | sucesso/monitor ativo |

Não usar valores hexadecimais diretamente em telas XAML. Exceções: arte vetorial
de marca e código de composição interna de bitmap, quando o valor não representa
um componente da interface.

## 4. Tipografia

| Papel | Token | Tamanho |
|---|---|---:|
| legenda | `FontSize.Caption` | 11 |
| corpo | `FontSize.Body` | 13 |
| corpo destacado | `FontSize.BodyLarge` | 14 |
| título de seção | `FontSize.Section` | 16 |
| título de página | `FontSize.Title` | 24 |

- títulos: `FontFamily.Display`, peso `SemiBold`;
- corpo e controles: `FontFamily.Body`;
- comandos/variáveis: `FontFamily.Mono`;
- não usar peso Bold em blocos extensos.

## 5. Espaçamento e geometria

A escala é múltipla de 4: `Space.1` 4, `Space.2` 8, `Space.3` 12,
`Space.4` 16, `Space.5` 20, `Space.6` 24 e `Space.8` 32.

| Elemento | Raio |
|---|---:|
| ícone/elemento pequeno | `Radius.Small` = 6 |
| botão/campo | `Radius.Control` = 8 |
| card/painel | `Radius.Card` = 10 |
| workspace/modal | `Radius.Large` = 12 |
| status/chip | `Radius.Pill` |

Controles interativos devem ter pelo menos 36 px de altura. A margem de página
padrão é 24 px; painéis densos podem usar 16 ou 20 px.

## 6. Estrutura das telas

1. `AppShellHeader`: marca à esquerda e estado do serviço à direita.
2. `AppNavigationBar`: menu horizontal central, com indicador inferior ciano.
3. Área de conteúdo em `CanvasBrush`.
4. `AppWorkspace` somente quando a tela exige uma área de trabalho contínua,
   como Atalhos. Telas de painel usam cards diretamente sobre o canvas.
5. Cabeçalho da tela com `PageHeading` e `PageSupportingText`.

### Tela Atalhos

- painel esquerdo: busca, categorias, mais usados e criação;
- centro: edição do atalho;
- painel direito: variáveis;
- divisores simples entre as três zonas;
- ação primária “Salvar alterações” alinhada à direita;
- ação destrutiva visualmente secundária;
- em largura reduzida, os painéis empilham sem perder controles.

## 7. Componentes e estados

Todos os componentes precisam prever:

- normal;
- hover;
- pressionado;
- foco por teclado;
- selecionado, quando aplicável;
- desabilitado;
- erro/sucesso, quando aplicável;
- claro e escuro.

Estilos de fundação disponíveis:

- `AppShellHeader`
- `AppNavigationBar`
- `AppNavigationButton`
- `AppWorkspace`
- `WorkspacePane`
- `WorkspaceSidebar`
- `PageHeading`
- `PageSupportingText`
- `FieldLabel`
- `SubtlePanel`
- `BrandMark`
- `CompactStatusPill`

Os estilos legados permanecem temporariamente para evitar regressões. Cada tela
migrada deve usar os estilos semânticos acima; ao final da migração, os estilos
sem uso serão removidos.

## 8. Captura e gravação

- toolbar, seletor, badge, editor inline e ajuda devem escolher a paleta clara ou
  escura a partir do tema efetivo;
- a imagem capturada não recebe tint do tema;
- ação principal fica à direita; cancelar/voltar permanecem neutros;
- a ferramenta selecionada usa `AccentBrush` e nunca depende apenas de cor:
  deve possuir borda, indicador ou rótulo;
- atalhos de teclado devem permanecer visíveis;
- gravação de tela deve reutilizar o seletor de região e os tokens do overlay.
- a barra flutuante de gravação usa `PanelBrush`, `DividerBrush`, `InkBrush`,
  `MutedBrush`, `DangerBrush` e a ação principal existente;
- configurações de MP4 e GIF ficam no painel Captura, não em uma tela ou padrão
  visual paralelo;
- MP4 usa H.264/Media Foundation localmente; o pacote não baixa, executa nem exige
  FFmpeg;
- a prévia do GIF acontece antes da gravação ser persistida no histórico;
- desfoque e pixelização sempre exibem indicação textual na prévia, além da cor;
- captura com rolagem é experimental e deve informar limitações em aplicativos que
  ignoram navegação por Page Down.

## 9. Responsividade WPF

- tamanho mínimo suportado da janela: 980 × 680;
- abaixo do breakpoint definido no `MainWindow`, painéis laterais são empilhados;
- não ocultar ações para acomodar largura;
- respeitar escala do Windows e múltiplos monitores;
- não fixar largura de texto sem `TextWrapping`.

## 10. Checklist por PR

- [ ] usa tokens semânticos, sem novas cores locais;
- [ ] funciona em Claro, Escuro e Seguir o Windows;
- [ ] possui hover, foco, seleção e desabilitado;
- [ ] mantém os handlers e funcionalidades existentes;
- [ ] não cria card dentro de card sem necessidade funcional;
- [ ] passa no `ui-integrity-smoke.ps1`;
- [ ] compila WPF sem warning;
- [ ] foi revisado em 100%, 125% e 150% de escala;
- [ ] foi conferido no tamanho mínimo e no tamanho padrão;
- [ ] overlays permanecem legíveis fora da janela principal.
