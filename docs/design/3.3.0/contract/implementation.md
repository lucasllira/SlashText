# Migração com fidelidade verificável

## Decisão recomendada
Manter WPF e os serviços existentes; reproduzir o contrato em ResourceDictionary, ControlTemplate e Storyboard. Antes de migrar tudo, construir uma fatia vertical: shell + aba Captura + overlay nativo, com os serviços reais. Aprovar fidelidade e comportamento no Windows. Se a fidelidade exigida não for atingida, avaliar um piloto WebView2 para as telas de gerenciamento, mantendo captura/overlay/atalhos nativos.

O Lab já é React/TypeScript + CSS, renderizado como HTML. Exportar apenas HTML remove estado e comportamentos; não gera XAML. WebView2 reaproveita HTML/CSS/JS, mas exige runtime, ponte de mensagens validada, hospedagem local dos assets e revisão de implantação/segurança. Não carregar a URL remota do Lab no app.

## Mapa de implementação
| Referência | WPF | Preservar |
|---|---|---|
| .s-window / .s-titlebar / .s-appnav | WindowChrome, Grid, controles de navegação com templates | resize/minimize/maximize, comandos e ordem das abas |
| .s-button / .s-field / .s-toggle | Button, TextBox/ComboBox, ToggleButton com ControlTemplate | foco, hover, pressed, disabled, validação |
| .s-mode-grid / .s-commandbar | Grid/WrapPanel adaptativos + ICommand | cinco modos, atalhos de teclado/mouse, atrasos e exclusão mútua de captura |
| .s-artboard / .cs-selection | overlay nativo existente + Canvas/Adorner | coordenadas virtuais, DPI por monitor, cantos e seleção |
| .cs-toolbar / .cs-properties | toolbar flutuante e Popup | todos os instrumentos reais, bounds por monitor e teclado |
| .s-snippet-workspace | Grid + GridSplitter + editor atual | RichText real, imagens, variáveis e dirty-state |
| .s-dialog | diálogos WPF tematizados | validação, cancelamento, restauração, backup e atualizador reais |
| .s-tray | renderer do menu nativo existente | contraste recursivo em todos os submenus |

## Estados obrigatórios
Cada controle: normal, hover, pressed, focus-visible, disabled; campos também inválido. Telas: preenchida, vazia, carregando, erro e alterações não salvas. Captura: preparar, selecionar, redimensionar, anotar, cancelar, concluir; gravação: ativa, pausada, finalizando, prévia. Atualizações e backup mantêm os contratos da 3.2.0.

## Movimento
Usar RenderTransform e Opacity com durações e curvas do tokens.json. Cubic-bezier CSS pode ser aproximado diretamente por KeySpline nas animações de keyframes; considerar os valores inicial/final de cada propriedade. Não animar geometria de captura enquanto ela estiver sendo medida. Respeitar SystemParameters.ClientAreaAnimation e preferência do usuário. Manter uma instância de transform por controle; não substituir dados ou handlers para obter efeitos.

## Gate de fidelidade
1. Fixar este pacote e a revisão do Lab na issue principal; mudanças posteriores atualizam o contrato e a revisão juntos.
2. Criar uma galeria WPF de controles reais e seus estados em Claro/Preto.
3. Usar os mesmos fixtures e conteúdo em Lab/WPF, tamanho cliente equivalente e escala 100%, 125%, 150% e 200%; comparar medidas em DIP e screenshots em pixels já normalizados.
4. Capturar 1440×900 e a janela mínima suportada. Não aprovar controles cortados ou funções escondidas sem acesso alternativo.
5. Comparar lado a lado e por sobreposição. Revisar antialiasing e fontes separadamente das diferenças de layout; não usar um percentual arbitrário como único gate.
6. Validar movimento separadamente: durações, easing, início/fim, interrupção e redução de movimento. Screenshot parada não valida animação.
7. Rodar build Release x64, smokes existentes, handlers/bindings e testes manuais de teclado, DPI misto e dados portáteis. Aprovação visual do usuário é necessária para considerar uma tela pronta.

## Organização
Uma issue principal + fundação visual + uma issue por tela + overlay/menus/diálogos + validação final. PRs pequenos. Começar por uma fatia real de Captura para reduzir a maior incerteza de fidelidade; depois reutilizar componentes aprovados nas demais telas. Não prometer equivalência pixel a pixel antes do piloto Windows.

## Fontes primárias
- https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/styles-templates-overview
- https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/storyboards-overview
- https://www.designtokens.org/TR/2025.10/
- https://learn.microsoft.com/en-us/microsoft-edge/webview2/
- https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution
- https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/security
- https://playwright.dev/docs/test-snapshots
