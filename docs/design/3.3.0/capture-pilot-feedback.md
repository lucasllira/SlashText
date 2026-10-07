# Captura 3.3.0 — revisão do teste de 06/10/2026

Escopo: issue #63 / PR #72. Não encerrar a issue ou avançar para outra tela antes da validação do candidato e do overlay nativo previsto na issue.

## Três divergências reportadas

| Item | Causa | Correção |
| --- | --- | --- |
| Novo | Paleta inicial diferente das imagens atuais do Lab; TextBlock interno recebia a cor implícita de texto em vez da cor do botão. | Token opt-in `Lab.capture-primary`: Claro `#337c8f`, Preto `#74d1e5`, extraídos do preenchimento nas imagens enviadas. Texto e ícone usam o Foreground do botão (branco no claro, escuro no preto). Windows acompanha a paleta do sistema. |
| Zoom | LayoutTransform externo era compensado pelo ajuste automático do Viewbox. | Viewport de altura estável, com rolagem; dimensões explícitas de prévia = ajuste ao viewport × 75/100/125%. Coordenadas de anotação e bitmap original permanecem independentes do zoom. |
| Copiar dos Recentes | Clipboard recebia somente FileDrop, que não permite colar a captura como imagem em vários destinos. | Payload com imagem decodificada e FileDrop original; carregamento OnLoad libera o arquivo. PNG/JPEG/GIF podem ser colados como imagem; GIF mantém também o arquivo animado e MP4 mantém cópia de arquivo. |

O contrato congelado em `contract/` permanece inalterado. A nova cor é uma exceção documentada e limitada ao botão Novo, sem repintar todos os controles ou telas. O site não pôde ser lido nesta revisão; as imagens de comparação enviadas pelo usuário são a referência dessa correção de cor.

## Verificações automáticas

- Smoke estrutural: vínculo explícito de cor no Novo, tokens por tema, chamada do zoom interno e ausência de LayoutTransform externo.
- Smoke WPF em STA: dimensões da prévia nos três níveis de zoom e tamanho/pixels da exportação inalterados.
- Payload do clipboard: PNG, JPEG e GIF oferecem imagem e arquivo; MP4 oferece arquivo; arquivo ausente falha sem alterar o clipboard; arquivo de origem não fica bloqueado.
- Build Windows e smokes existentes continuam obrigatórios. O smoke do payload não escreve no clipboard real do runner; colar em aplicativos externos exige teste manual.

## Reteste manual do candidato

1. Comparar Novo com as imagens do Lab em Claro e Preto; alternar Windows e verificar contraste de texto e ícone. Repetir em Imagem, Vídeo e GIF.
2. Abrir uma imagem e alternar 75%, 100%, 125%: o tamanho visível deve mudar, com rolagem quando necessário, sem redimensionar o editor inteiro. Desenhar e exportar; tamanho original e posicionamento devem permanecer corretos. Repetir com imagem alta de Captura longa e ao redimensionar a janela.
3. Clicar Copiar em uma captura PNG/JPEG dos Recentes e colar no Paint ou outro editor. Copiar GIF: testar a imagem estática num editor e o arquivo animado no Explorer. Copiar MP4: colar como arquivo no Explorer.
4. Confirmar que selecionar o cartão e abrir/editar/excluir continuam funcionando, assim como o carrossel, os atalhos, as regras e as animações com redução de movimentos.

Não substituir dados existentes por dados de exemplo nem publicar release 3.3.0 com este candidato. A aprovação da tela não encerra automaticamente o restante da #63.

## Segunda revisão — edições salvas, emotes e ações principais

Feedback seguinte: copiar e zoom aprovados, mas Recentes continuava apontando para o original depois de Salvar uma cópia; emotes trocavam de aspecto/identidade; janela avançada e ações principais divergiam do piloto.

### Correções deste recorte

- Salvar (simples ou avançado) passa por `SaveEditedImageAsync`: grava pixels em arquivo temporário, substitui o destino escolhido e registra esse arquivo exato no histórico. Destino novo tem ID próprio; destino já registrado atualiza o mesmo ID e as dimensões, sem duplicação. A cópia não altera o arquivo de origem. O editor passa a selecionar a cópia salva, e as miniaturas são reconstruídas sem cache de arquivo.
- Concluir uma imagem do histórico atualiza arquivo/registro/miniatura; imagens externas e quadros de GIF usam diálogo quando o salvamento está ativo, sem sobrescrever gravação animada com PNG. O original de um GIF é preservado.
- Ambos os editores usam o catálogo Noto de 36 itens na seleção, prévia e exportação. Antes, a prévia simples era um glifo Segoe UI e oferecia símbolos sem asset; o fallback de exportação os trocava por coração. Agora o picker mostra somente assets disponíveis e um valor desconhecido é rejeitado, não substituído silenciosamente.
- `Lab.Pilot.PrimaryButton` compartilha a cor refinada de Novo com Concluir, Salvar regra, Salvar atalhos e os diálogos de inserção. Os TextBlocks compostos usam explicitamente o Foreground do botão.
- A janela avançada usa tipografia, paleta, superfícies, ferramentas selecionadas, campos, botões e movimento do piloto. Preserva seta, marca-texto, retângulo, elipse, lápis, texto, número, desfoque, pixelização, recorte, redimensionamento e undo/redo; adiciona o mesmo seletor de emoji. Não se promete identidade final de geometria: a janela ainda é separada.
- Edição iniciada pelos Recentes libera o arquivo de origem antes de substituir e diferencia Copiar, Salvar cópia e Concluir. A prévia ativa é recarregada depois de uma edição salva.

### Avaliação: um único editor é viável e preferível

Recomendação: **um editor reutilizável, embutido na Captura, com modo de trabalho expandido**, não dois editores com capacidades/estado duplicados. O formato WPF atual permite composição em UserControl e reaproveitamento em hosts diferentes; não exige HTML/WebView nem troca de serviços.

Não remover a janela antes de migrar seus recursos. Integração completa é uma etapa adicional da #63, dependente de aprovação do usuário para essa mudança de fluxo:

1. Extrair sessão/documento único: bitmap base, anotações, undo/redo, seleção, zoom/pan, estado alterado e comandos de saída.
2. Compartilhar o mesmo controle visual entre a aba e um host expandido; expandir não deve rasterizar camadas nem apagar undo/redo.
3. Migrar elipse, número, desfoque, pixelização, recorte e redimensionamento; prévia deve mostrar o resultado real (não só caixas indicativas de privacidade).
4. Colocar ferramentas menos frequentes em Mais ferramentas/painel contextual, mantendo a barra principal compacta e navegável por teclado.
5. Separar saída explícita: Copiar não grava; Salvar cópia registra destino novo; Concluir aplica regra/registro conforme o contexto; cancelar alterações não modifica original nem histórico.
6. Só substituir o botão Editor avançado por Expandir editor e retirar o fluxo antigo após testes equivalentes nas duas apresentações, temas/DPI/movimento reduzido e aprovação manual. O overlay de captura permanece um host nativo distinto, compartilhando sessão/renderer quando apropriado.

Esta revisão alinha a janela existente, mas **não implementa nem aprova a remoção do editor avançado**.

### Reteste adicional

1. Em uma captura, desenhar e Salvar com novo nome: a cópia deve ser o primeiro Recente, com miniatura editada; Copiar/colar essa entrada deve incluir as anotações. O original continua intacto. Editar a cópia e Concluir deve atualizar essa entrada sem duplicá-la. Reiniciar e repetir Copiar.
2. Inserir positivo, sorriso, estrela e coração; comparar prévia simples, janela avançada, PNG salvo e clipboard. A identidade/cor dos emotes deve permanecer igual.
3. Conferir os três botões principais em Claro/Preto/Windows e abrir ferramentas avançadas, testar recorte/redimensionamento, Copiar, Salvar cópia, Concluir e cancelar.
4. Evidências automáticas novas: snapshots dos diálogos reais e do editor avançado em Claro/Preto, com asserts de preenchimento/contraste; smoke de persistência das edições, original preservado, atualização sem duplicatas e reload do histórico.
