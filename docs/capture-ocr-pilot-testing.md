# Captura + OCR — piloto 3.3.0

Este piloto inclui as melhorias da Captura do PR #75 e acrescenta OCR local.
Extraia em uma pasta nova e abra `Abrir-Piloto-Captura-OCR.cmd`.
Os dados ficam em `SlashDeskPilotData`; a versão oficial 3.2.0 não é substituída.
Feche outros pilotos antes de testar atalhos globais. Não há release/tag neste trabalho.

## Configurações de captura

O botão **Configurações de captura** reúne Imagem e destino, Atalhos, Vídeo e GIF e OCR.
O botão separado Personalizar atalhos e a ajuda duplicada ao lado de Expandir editor foram removidos.
A ajuda principal permanece no cabeçalho.

- Imagem e destino: regra direta/editor, cópia, salvamento, pasta, nome, PNG/JPEG, cursor, atraso e histórico.
- Atalhos: os mesmos quatro campos de gravação de combinações para monitor, região, janela e captura longa.
  Combinações inválidas ou repetidas impedem o salvamento. O status mostra conflitos com outros aplicativos.
- Vídeo e GIF: FPS, qualidade e cursor. Os controles rápidos da tela continuam disponíveis e sincronizados.
- OCR: **Best** (padrão, prioriza precisão) ou **Fast** (prioriza velocidade), português/inglês/ambos,
  organização automática/bloco/texto espalhado e melhoria de imagens difíceis.

Salvar aplica as quatro seções juntas; Cancelar, Esc ou clicar fora descarta as alterações do diálogo.
As preferências são persistidas e valem para OCR na região e no editor. Arquivos antigos usam Best e ambos os idiomas.
Fast e Best já estão incluídos no executável; alternar não exige rede, instalação ou download.

## Imagens difíceis

Quando a confiança do motor é baixa ou não há texto, a opção de melhoria permite tentativas limitadas
com outras segmentações, binarização Sauvola e contraste pelo canal de cor mais claro.
Apenas a cópia usada pelo OCR é tratada. Modelos/idiomas escolhidos são respeitados em todas as tentativas.
O resultado com maior confiança é escolhido; esse valor é uma heurística do motor, não garantia de acerto.
Leituras com confiança abaixo de 80% mostram orientação para revisão, sem descartar o texto editável.
Desativar a melhoria usa somente a organização escolhida, com menor trabalho de processamento.

Nos recortes das prévias fornecidas pelo usuário, o título branco/vermelho passou a ser reconhecido
corretamente e o parágrafo em inglês foi preservado. O título amarelo e o logotipo tiveram melhora parcial,
mas ainda requerem correção. Essas prévias são menores que as imagens originalmente capturadas;
não permitem comparar a precisão do exemplo da página escolar com a imagem de entrada original.
Não houve treinamento ou correção baseada nas frases esperadas. Fontes estilizadas, fotografia,
compressão e pouca definição continuam sendo limitações do Tesseract.

## Extrair durante a seleção

1. Em Captura → Novo → Região, selecione uma área contendo texto.
2. Ajuste o recorte normalmente. Na barra, clique no botão identificado **Extrair texto**, ao lado de Capturar.
3. Durante o reconhecimento, a área e sua prévia recebem uma camada suave azul/violeta.
   A animação não modifica pixels, anotações, recorte, histórico ou imagem exportada.
4. Revise o texto apresentado. É possível editar, selecionar um trecho, Copiar seleção ou Copiar tudo.
5. Fechar retorna à mesma seleção. OCR não finaliza/salva uma captura, não aplica regras de captura
   de imagem e não cria registro no histórico. A captura normal permanece disponível.

## Barra de captura e popup

A barra mantém os blocos **Área, Anotar, Privacidade, Histórico, OCR e Finalizar**, com títulos e separadores.
Área reúne mover e refazer seleção; Privacidade mantém Desfocar/Pixelizar visíveis, inclusive no modo compacto.
Extrair texto tem bloco próprio. As opções de cor, tamanho e intensidade aparecem ao escolher uma ferramenta.
O menu de três pontos mostra Limpar marcações e, em áreas pequenas, as ferramentas de anotação recolhidas.
Não repete mover, refazer seleção, privacidade ou as ferramentas que continuam visíveis. Blocos podem
passar para outra linha em uma área estreita, sem cortar comandos ou esconder OCR/Privacidade.

Configurações de captura fica centralizada na área do aplicativo, com o fundo cobrindo toda a janela.
O limite de tamanho da popup não limita mais o fundo semitransparente em janelas maiores/maximizadas.
Cancelar, Esc e clique fora continuam descartando alterações. Conferir janela normal/maximizada
e monitores com escalas diferentes; a correção do fundo é compartilhada pelos demais diálogos.
Na popup de configurações, a sombra agora é uma camada decorativa separada dos textos e controles.
O conteúdo usa alinhamento aos pixels, formatação de texto Display e indicação de ClearType sobre
o fundo opaco. Isso evita aplicar o efeito de sombra ao texto. Conferir nitidez nas escalas reais
do Windows; imagens renderizadas em 100/125/150% não substituem o teste de DPI em hardware.

## Extrair no editor

1. Abra uma captura ou importe uma imagem. Use **Extrair texto** acima da imagem.
2. No editor normal ou expandido, o reconhecimento usa a composição atual, com anotações e efeitos.
   Um recorte pendente precisa ser aplicado ou cancelado antes de extrair.
3. Confira o texto, copie e feche. O documento, checkpoint e undo/redo devem permanecer iguais.

## Cancelamento e estados

- Cancelar leitura, Esc ou fechar o painel interrompe o worker e não copia/salva.
- Se não houver texto, as ações de cópia ficam desabilitadas. Tentar novamente refaz a leitura.
- O clipboard só muda por ação de cópia do usuário. Copiar seleção usa apenas o trecho escolhido.
- Com movimentos reduzidos ou animações do Windows desligadas, a indicação é estática.
- O processamento é limitado a 45 segundos e regiões de até 24 megapixels. Imagens maiores
  devem ser recortadas em trechos. Falhas do motor ficam isoladas do processo do editor.
- Imagem e resultado usam arquivos temporários locais durante o worker; são removidos ao concluir
  ou cancelar. Não são enviados, adicionados ao histórico ou registrados nos logs. Apenas os modelos
  públicos ficam em cache temporário para as próximas leituras.

## Requisitos e dependências

Windows x64 compatível com a aplicação atual. Português e inglês já estão no executável;
o reconhecimento funciona sem internet, conta ou download de modelos no primeiro uso.

O wrapper Tesseract 5.2.0 utiliza DLLs compiladas com Visual Studio 2019. É necessário o runtime
Microsoft Visual C++ 2015–2022 x64 disponível no Windows. Ele não é instalado silenciosamente pelo piloto.
Se o computador não o tiver, o OCR pode falhar ao carregar componentes; a Captura continua funcionando.
[Requisito do wrapper](https://github.com/charlesw/tesseract#dependencies).
[Download oficial do runtime](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist?view=msvc-170).
Uma validação em Windows sem esse runtime ainda é necessária antes da distribuição oficial do OCR.

Tesseract/InteropDotNet/Leptonica e modelos Best/Fast têm avisos em Assets/Ocr/NOTICE.md e LICENSE.txt,
também incluídos no pacote de teste. Modelos: tessdata_best, commit e12c65a915945e4c28e237a9b52bc4a8f39a0cec,
restaurados com verificação SHA-256 durante build e embutidos no EXE. Sem treinamento próprio.
Fast: tessdata_fast, commit 87416418657359cb625c412a48b6e1d6d41c29bd, também verificado por SHA-256.

## Validação

Automatizada: worker real no Windows, pt/en, acentos, números, URL/e-mail, letras pequenas,
código em fundo escuro, resultado vazio, cancelamento prévio/durante execução, limpeza temporária,
cópia de trecho/tudo, indicação estática, descarte de clocks e preservação de pixels/revisão do editor.
Evidências WPF nos temas Claro/Preto/Windows, painel compacto e renderização 100%/150%.
As quatro abas de configurações são renderizadas nos três temas. Testes cobrem Cancelar sem mutação,
conflitos de atalhos, regra direta sem saída, preservação dos campos legados de gravação,
salvar/reabrir as preferências, defaults antigos, opções inválidas e uso real de Fast/Best offline.
Também são conferidos os seis títulos, OCR e Privacidade visíveis, acesso sem duplicação na barra/menu,
modos normal/compacto e ferramentas de privacidade ativas nos três temas. Textos ficam fora da árvore
do efeito de sombra, com renderizações em 100/125/150%. O fundo e a centralização
são verificados com janelas normal/maximizada, inclusive com limites menores na popup para reproduzir o defeito.

Comparação preliminar em seis imagens sintéticas: fast e best reconheceram todos os exemplos;
fast confundiu `}` com `3` no exemplo de código, best acertou os seis textos após normalizar espaços.
Na primeira medição local, leituras completas ficaram em aproximadamente 0,4–0,6 s;
o worker best atingiu cerca de 130 MiB, contra 104 MiB do fast. Esses números descrevem apenas
o computador e fixtures usados, não precisão/latência garantidas. Modelos best somam 23.560.540 bytes
antes da compressão do pacote. A comparação completa fica em benchmark.json das evidências.

Teste manual: prints reais de erros, navegador, terminal, telas claras/escuras e imagens comprimidas;
português/acentos, inglês, números, URL e código; cancelar pelo botão/Esc; copiar sem seleção;
editor normal/expandido; mover texto/emoji e conferir undo/redo depois do OCR; copiar/salvar imagem
sem qualquer tintura azul/violeta. Testar dois monitores com DPI diferente no hardware real.
Renderizações em escalas diferentes não substituem esse teste. Tabelas não são reconstruídas;
indentação de código e ordem de leitura podem precisar de correção. Não há seleção de palavras
diretamente sobre a imagem nesta versão; os trechos são selecionados no painel de resultado.

O PR permanece em rascunho até o teste manual. Não integrar ou publicar release automaticamente.
