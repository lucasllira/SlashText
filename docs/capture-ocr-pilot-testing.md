# Captura + OCR — piloto 3.3.0

Este piloto inclui as melhorias da Captura do PR #75 e acrescenta OCR local.
Extraia em uma pasta nova e abra `Abrir-Piloto-Captura-OCR.cmd`.
Os dados ficam em `SlashDeskPilotData`; a versão oficial 3.2.0 não é substituída.
Feche outros pilotos antes de testar atalhos globais. Não há release/tag neste trabalho.

## Extrair durante a seleção

1. Em Captura → Novo → Região, selecione uma área contendo texto.
2. Ajuste o recorte normalmente. Na barra, clique no ícone de leitura, **Extrair texto da região**.
3. Durante o reconhecimento, a área e sua prévia recebem uma camada suave azul/violeta.
   A animação não modifica pixels, anotações, recorte, histórico ou imagem exportada.
4. Revise o texto apresentado. É possível editar, selecionar um trecho, Copiar seleção ou Copiar tudo.
5. Fechar retorna à mesma seleção. OCR não finaliza/salva uma captura, não aplica regras de captura
   de imagem e não cria registro no histórico. A captura normal permanece disponível.

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

Tesseract/InteropDotNet/Leptonica e modelos best têm avisos em Assets/Ocr/NOTICE.md e LICENSE.txt,
também incluídos no pacote de teste. Modelos: tessdata_best, commit e12c65a915945e4c28e237a9b52bc4a8f39a0cec,
restaurados com verificação SHA-256 durante build e embutidos no EXE. Sem treinamento próprio.

## Validação

Automatizada: worker real no Windows, pt/en, acentos, números, URL/e-mail, letras pequenas,
código em fundo escuro, resultado vazio, cancelamento prévio/durante execução, limpeza temporária,
cópia de trecho/tudo, indicação estática, descarte de clocks e preservação de pixels/revisão do editor.
Evidências WPF nos temas Claro/Preto/Windows, painel compacto e renderização 100%/150%.

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
