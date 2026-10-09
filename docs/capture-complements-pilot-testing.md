# Complementos da Captura — piloto 3.3.0 · issue #74

Base: issue #63 integrada, seguida da tela Atalhos #64 aprovada. Este pacote é um piloto de desenvolvimento, não uma release oficial. O Visual Lab permanece como referência; estes complementos foram solicitados para o app real.

## Abrir com dados separados

Extraia o ZIP em uma pasta própria e abra `Abrir-Piloto-Captura.cmd` ou `SlashDesk.exe`. O título identifica **Complementos da Captura · #74**. Os dados ficam em `SlashDeskPilotData` ao lado do executável. O piloto não migra os dados oficiais, não verifica atualizações e não registra início com o Windows. Feche outros pilotos antes de testar para evitar conflitos entre atalhos globais.

## Histórico e busca

1. Faça capturas com nomes diferentes e em mais de um tipo. Busque um pedaço do nome em **Buscar capturas**; combine com o filtro. Data aceita `dd/MM/aaaa`, `aaaa-MM-dd` e mês por extenso. A busca ignora caixa e acentos.
2. Clique **Ver todas**. O carrossel continua em uma linha, com até 40 resultados; a janela completa oferece todos os registros em páginas de 25, sem carregar miniaturas de todo o acervo.
3. Teste Anterior/Próxima, busca, limpar busca e filtro. Busca e filtro voltam à primeira página.
4. Teste Abrir, Copiar, Editar (imagem estática) e Excluir. As ações são as mesmas do carrossel. Excluir continua exigindo a confirmação existente. Arquivos ausentes ficam identificados; abrir/copiar/editar ficam indisponíveis, sem remover entradas automaticamente.
5. Feche por Fechar, Esc e clique no fundo. Fechar não altera nenhum registro.

## Editar anotações que já foram inseridas

1. Abra uma imagem e insira texto, emoji, retângulo, caneta e desfoque/pixelização.
2. Selecione a ferramenta de seleção/navegação e clique no objeto. A moldura identifica a anotação; o painel contextual apresenta suas propriedades.
3. Arraste para mover. A moldura acompanha o arraste e a composição é atualizada ao soltar (evita renderizar todos os efeitos a cada movimento). Setas movem 1 px; Shift+setas, 10 px. Ctrl+arraste navega pela imagem mesmo sobre uma anotação.
4. Altere texto, fonte, tamanho, cores, preenchimento, opacidade ou intensidade conforme o objeto e clique **Aplicar alterações**. Retângulo, elipse e áreas de privacidade também oferecem largura/altura em pixels.
5. Exclua uma anotação pelo botão contextual ou Delete. Teste desfazer/refazer de alterações, movimento e exclusão.
6. Faça um recorte e redimensione a imagem; selecione um objeto que continua visível e confirme movimento/alteração/desfazer. Zoom e expansão preservam a seleção e o documento.
7. Copiar/Salvar/Concluir devem produzir a composição atualizada. Após salvar na mesma sessão, os objetos continuam editáveis; descartar volta ao checkpoint salvo.

### Mover texto e emoji durante a seleção

1. Em Novo → Região, selecione uma área e insira texto e emojis, incluindo uma imagem de Meus emojis. Termine a digitação (Enter ou foco fora) antes de mover o texto.
2. Com Texto/Emoji ativo, arraste uma anotação existente do mesmo tipo. Com Selecionar, ambos os tipos podem ser movidos. Não deve aparecer uma duplicata nem se deslocar o retângulo da captura.
3. A moldura acompanha o mouse; a composição atualiza ao soltar. Um Ctrl+Z desfaz o arraste inteiro; Ctrl+Y refaz. Esc durante o arraste cancela o movimento; depois de selecionar, Esc primeiro limpa a seleção do objeto.
4. Setas movem o objeto selecionado em 1 pixel da imagem de saída; Shift aumenta para 10. Sem objeto selecionado, as setas continuam ajustando a área. Ctrl+arraste move a área mesmo quando o cursor está sobre texto/emoji; as alças continuam redimensionando a área.
5. Mova/redimensione a área depois de anotar, mova o texto/emoji que continua visível e confira Capturar, Copiar e Salvar. A posição usa o conteúdo congelado do desktop, inclusive com DPI diferente.
6. No editor normal e expandido, clique em texto/emoji existente com sua ferramenta ativa: o editor seleciona a anotação para mover e ajustar propriedades. Selecione novamente a ferramenta para inserir novas anotações. Ctrl+arraste continua sendo navegação.
7. Finalize a captura e volte à tela Captura: os mesmos textos/emojis inseridos na seleção devem continuar sendo objetos editáveis. Mova-os e teste desfazer, salvar/copiar e descartar para o checkpoint. A transferência preserva o recorte limpo e as anotações separadas; não reconstrói camadas a partir do PNG. Antes de iniciar outra captura de região, uma edição pendente pede confirmação para não ser substituída silenciosamente.

Formas e efeitos continuam editáveis como objetos no editor. Na seleção do desktop, esta melhoria permite mover **textos e emojis**. A seleção prioriza o objeto de cima; uma anotação totalmente coberta precisa ser desobstruída antes de selecionar.

**Limite do formato:** objetos são editáveis na sessão atual. PNG/JPEG exportados contêm a composição achatada. Fechar/reabrir um arquivo não recupera objetos, fontes nem áreas de desfoque como camadas. Um formato persistente de projeto é outra funcionalidade. Seleção usa a área delimitadora e prioriza o objeto de cima; não existe ainda um painel de camadas para objetos totalmente sobrepostos.

## Ajuda e aparência

Abra `?`, busque um recurso, leia os passos e confira as demonstrações. Há tópicos novos sobre edição de objetos e histórico completo. Clique dentro para continuar lendo, fora para fechar; confira também Esc e X. Fechar não deve acionar a tela por trás. Teste Claro, Preto e Windows, janela reduzida e zoom do Windows em 100%, 125%, 150% ou 200%, conforme disponíveis.

## OCR: avaliação, ainda não implementado

Fluxo proposto: a barra de seleção oferece **Extrair texto**, o usuário arrasta a região, o reconhecimento local trabalha sobre a imagem e apresenta texto para selecionar/copiar. Não exige um assistente generativo, conta ou envio da imagem.

O fluxo básico é de complexidade média. Seleção de palavras diretamente sobre a imagem acrescenta coordenadas, DPI, teclado e estados; tabelas estruturadas ficam fora da primeira versão. Para a distribuição portátil atual, avaliar Tesseract + modelos português/inglês. A API Windows.Media.Ocr documenta identidade de pacote para desktop; não migrar o produto para MSIX só por esse recurso. A API Windows AI OCR mais recente tem requisitos de hardware distintos e não é premissa deste plano.

Antes de autorizar implementação, comparar textos pequenos, tema claro/escuro, acentos, código, telas em DPI misto, imagens comprimidas e regiões grandes. Medir tempo, memória e erros; não há promessa de precisão ou desempenho sem esse ensaio. Cancelar não copia/salva e o processamento não bloqueia a interface. Sucesso não precisa criar um arquivo de captura.

Fontes técnicas: Microsoft Text Extractor (15/04/2025), Windows.Media.Ocr e Tesseract ImproveQuality. OCR ficou em avaliação nesta issue; nenhum motor foi adicionado ao pacote.

### Treinamento próprio: avaliação de viabilidade

O OCR não precisa começar cru: o Tesseract oferece modelos já treinados, inclusive português e inglês. `tessdata_fast` prioriza desempenho e `tessdata_best` prioriza qualidade nos testes do projeto (isso não garante a mesma diferença nos prints do SlashDesk). Somente a família `best` serve de base para ajuste fino. Um modelo local não precisa de API, conta ou envio da imagem; tecnicamente seu reconhecimento LSTM já usa aprendizado de máquina, mas não exige um assistente generativo nem serviço externo.

Adicionar imagens ao pacote não cria uma memória automática. O treinamento supervisionado exige recortes de linhas e transcrições exatas revisadas, por exemplo `linha.png` + `linha.gt.txt`. A saída é um modelo `.traineddata`: as imagens de treinamento e suas ferramentas não precisam acompanhar o app. O tamanho depende do modelo, idiomas e distribuição; não cresce necessariamente na proporção da quantidade de imagens. Medir o tamanho real do pacote e a memória de execução antes de escolher.

Recomendação, sem iniciar implementação de OCR nesta etapa:

1. Montar um conjunto de avaliação com prints representativos e textos corretos: português/acentos, inglês, números, URLs, código, fontes/tamanhos, temas claro/escuro e DPI. Usar exemplos com licença compatível, dados públicos ou conteúdo sintético.
2. Comparar os modelos prontos e variantes de preparação da imagem (recorte com margem, escala, contraste/inversão quando necessário e segmentação). Preservar imagem original. Medir erros de caracteres/palavras, tempo, memória e MB do pacote.
3. Separar os exemplos de ajuste dos testes finais, inclusive por origem/fonte/template, evitando testar cópias do material usado para treinar. Imagens sintéticas ajudam a variar fontes, mas não substituem prints reais na avaliação.
4. Só abrir uma etapa de treinamento se houver falha recorrente que a preparação e configuração não resolvam. A documentação alerta que retreinar costuma não ajudar em fontes/idiomas comuns. Fine-tuning de modelo pronto é preferível a treinar do zero; complexidade alta em dados, avaliação e manutenção.
5. Adotar um modelo próprio somente se superar o modelo base nos exemplos novos sem regressões relevantes e com custo aceitável. Versionar dataset, modelo, licença e resultados. Não aprender automaticamente com capturas pessoais.

Referências primárias:
- https://tesseract-ocr.github.io/tessdoc/Data-Files.html
- https://github.com/tesseract-ocr/tesstrain (ground truth, START_MODEL e avaliação)
- https://tesseract-ocr.github.io/tessdoc/ImproveQuality.html

## Encerramento

Registrar no PR o commit, resultado dos testes Windows e capturas das três opções de tema. Integrar/encerrar #74 somente depois da aprovação do piloto pelo usuário. Em seguida retomar a ordem do plano #61, com a tela Acento Rápido #65.
