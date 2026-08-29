# Barra de marcações Fluent do SlashDesk

## Escopo

Este piloto altera somente a barra transitória da captura por região, seus
painéis contextuais, o modelo transitório das anotações e a renderização do
bitmap final. Não altera o shell, Atalhos, expansão de texto, persistência,
backup, GIF ou MP4.

## Base funcional preservada

A barra continua em uma janela flutuante independente do overlay. O monitor é
obtido pelo retângulo físico da seleção; a área útil vem de `MONITORINFO.rcWork`.
O posicionamento usa pixels físicos, DPI por monitor e `SetWindowPos`, inclusive
para monitores com coordenadas negativas. A barra e cada painel contextual são
medidos antes do cálculo e validados integralmente contra a área útil.

## Composição

- Barra principal escura, em uma linha, com raio de 11 DIPs.
- Botões de 36 x 36 DIPs e ícones vetoriais em `Geometry`.
- Capturar é um botão dividido: configuração atual, copiar ou salvar.
- Ferramentas: Seta, Marca-texto, Formas, Lápis, Texto, Número e Borracha.
- Ações: Desfazer, Refazer, Refazer seleção e Cancelar.
- Em menos de 620 DIPs disponíveis, comandos secundários migram para um menu
  de overflow visível. Capturar, ferramenta ativa, Desfazer, Refazer e Cancelar
  permanecem visíveis.
- Painéis contextuais possuem altura máxima monitor-aware e rolagem vertical
  visível; abrem acima quando não há espaço abaixo.

## Recursos semânticos

Os recursos `CaptureToolbarSurfaceBrush`, `Elevated`, `Border`, `Text`,
`SecondaryText`, `Hover`, `Pressed`, `Selected`, `Disabled`, `Accent`, `Danger`
e `Focus` ficam em `Styles/CaptureToolbar.xaml`. A interface de captura é sempre
escura para manter contraste previsível sobre a imagem.

## Anotações e paridade

O modelo transitório suporta contorno, preenchimento, opacidade, espessura,
tamanho, negrito e carimbo. Retângulos e elipses impedem preenchimento e
contorno simultaneamente ausentes. Emoticons usam Segoe UI Emoji no overlay e
são rasterizados por WPF em ARGB transparente antes de compor o bitmap final.
Limpar todas as marcações cria uma etapa de histórico e pode ser desfeito.

## Acessibilidade e movimento

Todos os comandos principais têm nome de automação, tooltip em português,
foco visível e área clicável de 36 x 36 DIPs. Escape fecha painéis e cancela a
captura quando nenhum painel está ativo. A abertura usa fade de 160 ms somente
quando as animações do Windows estão habilitadas.

## Validação manual

1. Abra a captura por região em escala 100%, 125% e 150%.
2. Posicione a seleção nos quatro cantos e confirme que barra e popovers cabem.
3. Repita em monitor secundário, incluindo monitor à esquerda do principal.
4. Acione Capturar, Copiar e Salvar e confirme que a opção pontual não altera a
   configuração persistida.
5. Teste cor, opacidade e espessura de Seta, Marca-texto e Lápis.
6. Teste Retângulo e Elipse com preenchimento, só contorno e ambos.
7. Insira emoticons em 24, 32, 48 e 64 px próximos às bordas da seleção.
8. Desfaça/refaça cada tipo e desfaça “Apagar todas as marcações”.
9. Compare overlay, clipboard e PNG salvo em posição, escala, cor e opacidade.
10. Em resolução estreita, confirme o overflow e a ausência de controles cortados.
