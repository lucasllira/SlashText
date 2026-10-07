# Captura 3.3.0 — piloto do editor unificado · issue #63

Este pacote é um **candidato de teste**, não uma atualização oficial. Não substituir o executável da versão 3.2.0 nem copiar os dados oficiais para este pacote.

## Como abrir sem alterar a instalação oficial

1. Extraia o ZIP completo para uma **pasta nova e gravável**.
2. Saia do SlashDesk oficial pelo menu da bandeja. O mutex continua compartilhado para impedir dois monitores de teclado/captura simultâneos.
3. Execute `Abrir-Piloto-Captura.cmd` ou `SlashDesk.exe`. A identificação de piloto está compilada no EXE; não depende do lançador.
4. Os dados são criados em `SlashDeskPilotData`, ao lado do EXE. O piloto não migra/importa dados de `%LocalAppData%\SlashDesk` nem de `SlashDeskData`. O destino inicial das novas capturas fica dentro da pasta do piloto; um destino escolhido manualmente pelo usuário é respeitado.
5. Consulta/aplicação de atualizações e registro de inicialização com Windows ficam bloqueados no piloto. Para voltar ao oficial, saia do piloto e abra sua instalação normal.

## Referências preservadas

- Lab original das telas: https://slashdesk-visual-lab.lucasllira.chatgpt.site/
- Experimento aprovado do editor: https://slashdesk-editor-unificado.lucasllira.chatgpt.site/
- Handoff: https://slashdesk-editor-unificado.lucasllira.chatgpt.site/editor-unificado-handoff.md
- Issue: https://github.com/lucasllira/SlashText/issues/63
- PR piloto, ainda em rascunho: https://github.com/lucasllira/SlashText/pull/72

Nenhum desses Labs é alterado por esta integração nativa.

## O que mudou neste candidato

- Um documento com fonte preservada, comandos imutáveis, checkpoint, desfazer/refazer de anotações, recorte e redimensionamento.
- Mesmo controle na Captura normal e expandida; expandir/voltar não reabre a imagem nem rasteriza o histórico. Esc volta ao painel (com foco na imagem); com seleção de recorte pendente, primeiro cancela a seleção.
- Barra contextual: fontes instaladas do Windows antes do campo de texto, tamanho independente da espessura, negrito, itálico e alinhamento; faixa horizontal dos 36 emotes Noto e busca sem acentos em Ver todos.
- A faixa rápida agora reserva altura para os botões completos, sem scrollbar cortando a base; tamanho em px tem campo mais largo. `Ver todos` oferece 3.972 emojis Unicode/Noto disponíveis no snapshot fixado, nove categorias, busca em português/shortcodes e páginas de 96 opções. Inclui tons de pele e bandeiras, funciona offline; não equivale aos emojis personalizados de organizações no Chat.
- No modo expandido, elipse, linha, número, desfoque, pixelização, recorte e redimensionamento aparecem diretamente na barra; os três pontos ficam somente no painel normal. Em janela menor as ferramentas continuam visíveis, com quebra de linha e ações de saída em outra linha.
- Mais ferramentas: elipse, linha, número, desfoque, pixelização, recorte confirmado e redimensionamento com proporção. Prévia, exportação e clipboard usam o mesmo renderer.
- Copiar não grava; Salvar cópia sugere um novo nome e registra o resultado; Concluir em um Recente estático salva no destino dessa entrada. Se escolher um arquivo existente no diálogo, o Windows solicita confirmação de sobrescrita.
- Salvar e atualizar/filtrar Recentes não zeram o documento nem undo/redo. Abrir outra imagem pede confirmação quando há alterações; Descartar retorna à **última versão salva nesta sessão**, diferença intencional em relação ao experimento web, que retornava à fonte inicial.
- Monitor/janela/captura longa com regra Editor encaminham para o mesmo editor embutido; Concluir aplica a regra, Cancelar captura não cria saída. O overlay nativo de seleção/região continua separado, com seu fluxo de captura existente.
- Vídeo/GIF continuam gravando; sua barra de anotações fica oculta. Alternar modos preserva a sessão da imagem; GIF animado não é aberto como imagem estática editável.
- Entrada de controles e menus, feedback de seleção e transição de altura do modo expandido usam motion nativo; redução de movimento é respeitada. Não há animação sobre coordenadas do documento.

Não existe reposicionamento/edição individual de objetos já inseridos nesta etapa (igual ao experimento aprovado): mudanças de fonte/cor afetam as próximas inserções. Os efeitos de privacidade não garantem remoção irrecuperável de informação; confira a saída antes de compartilhar.

## Testes manuais para aprovação

### Cores e desempenho — retorno de 06/10/2026

- O botão de cor abre uma faixa horizontal: principais cores primeiro, setas para os demais 30 tons e **RGB** no final. Os campos R/G/B (0–255) e hexadecimal ficam recolhidos até solicitados. Enter/Aplicar confirma a cor; entrada inválida mantém a anterior. Esc/clique fora fecha a faixa. Espessura de 1–24 px, amostra do traço e campo da barra ficam sincronizados. Propriedades afetam próximas anotações.
- Repetir Claro/Preto/Windows; verificar teclado, RGB inválido/255, hexadecimal, setas, cor branca/preta e tamanhos 1/7/24. Reduzir a janela/DPI: RGB pode quebrar linha, sem sobrepor controles.
- Repetir o caso relatado: imagem 2560×2317, pelo menos 61 emotes e desfoques sobrepostos. Comparar resposta ao inserir novas anotações, trocar ferramenta, expandir e desfazer. Salvar/copiar precisam preservar a mesma composição. Desfoques grandes e reconstrução após desfazer ainda dependem do tamanho/CPU; o benchmark do runner não aprova fluidez física.
- A prévia agora transfere pixels sem comprimir PNG. O documento mantém **um** bitmap de cache, aplica novos comandos incrementalmente e invalida o cache quando undo/descartar altera o prefixo. Fonte/undo/checkpoint não são achatados; exportação devolve cópia independente.

### Etapa da #63: overlay/barra de seleção

Pertence à #63. A implementação está no novo candidato descrito abaixo; os testes físicos e a aprovação permanecem pendentes. Antes de encerrar o piloto:
1. Inventariar os handlers de `RegionCaptureWindow` e a barra nativa, usando contrato fixado e o print de referência Snipping Tool.
2. Provar seleção por arraste, mover e **reduzir/ampliar a área pelas alças**, cancelar e finalizar. Coordenadas de imagem/desktop, DPI e monitores devem permanecer corretos.
3. Harmonizar barra flutuante, ícones, popups de propriedades e animações; permitir anotações rápidas **antes** de finalizar a captura real. Reutilizar componentes/renderer quando as coordenadas e lifecycle permitirem; não substituir por desktop fictício do Lab.
4. Validar posicionamento nos quatro cantos, monitores negativos/mistos, taskbar, saídas Direta/Editor e todos os instrumentos já existentes.
5. Entregar outro candidato isolado com evidências Claro/Preto e teste manual. Não avançar às demais telas nem fechar #63 sem essa validação.

Use imagens/capturas de teste, não arquivos únicos importantes. Repita em Claro, Preto e Windows.

1. **Imagem importada:** abrir PNG/JPEG; caneta, seta, retângulo, texto Georgia e Consolas, tamanhos distintos, negrito/itálico, emotes diferentes. Conferir prévia e arquivo/clipboard.
2. **Mesma sessão:** expandir, inserir elipse/número, voltar, desfazer/refazer. Nada deve sumir nem duplicar. Repetir com janela estreita e maximizada; ferramentas não podem ficar cortadas.
3. **Emotes:** percorrer a faixa horizontal; conferir que botões e tamanhos 32–128 px não estão cortados, também em janela estreita e escalas 125%/150%. Ver todos: navegar páginas, filtrar nove categorias, buscar `coracao`, `gato`, `:smile:`, buscar nome inexistente; inserir emote fora dos 36 rápidos (ex.: 👍🏽 e 🇧🇷). Ele deve aparecer selecionado na faixa e igual no arquivo/clipboard. Esc/Tab/Enter e contraste nos três temas. Repetir sem internet.
4. **Geometria:** recortar arrastando em ambos os sentidos. Cancelar seleção preserva imagem; aplicar muda dimensões. Enquanto houver seleção pendente, copiar/salvar/concluir ficam desabilitados. Desfazer/refazer restaura/reaplica o recorte.
5. **Tamanho:** redimensionar mantendo proporção e sem proporção. Conferir dimensões do PNG/JPEG; desfazer até antes do redimensionamento. Entradas inválidas/maiores que 64 MP não devem ser aplicadas.
6. **Zoom/pan:** 25–400%, Ctrl+roda, arrastar com Navegar. Zoom é relativo ao ajuste inicial: 100% significa ajustar, não um pixel da tela por pixel de imagem. Pixels exportados não mudam com o zoom.
7. **Saída/histórico:** salvar cópia, desenhar algo novo, copiar o Recente salvo. Ele deve representar a versão salva, não a nova anotação. Concluir essa versão editada, copiar o mesmo Recente e conferir atualização. Desfazer ainda deve funcionar após salvar.
8. **Proteção de alterações:** abrir outro Recente com alterações pendentes e responder Não. A imagem atual e seu histórico devem permanecer. Descartar só deve voltar ao checkpoint depois da confirmação. Fechar com edição pendente também pede confirmação.
9. **Captura real:** regra Editor, monitor/janela/captura longa → mesma sessão embutida → Concluir ou Cancelar captura. Regra Direta deve continuar sem exigir editor. Captura de região mantém o overlay funcional.
10. **Gravação sem edição:** com desenho pendente, Imagem → Vídeo → GIF → Imagem. Barra não aparece nos modos de gravação e volta com a mesma sessão. Gravar, pausar, finalizar e copiar/abrir os arquivos recentes sem achatar animação.
11. **Motion/acessibilidade:** movimentos ligados/desligados no Windows, Tab/Shift+Tab/Enter/Esc, menu Mais ferramentas e foco após voltar. Repetir em escala 100%, 125%, 150% e monitores distintos.
12. **Isolamento:** confirmar criação de `SlashDeskPilotData`, nenhuma importação dos atalhos/configurações oficiais, atualizador/inicialização com Windows bloqueados. Sair do piloto e conferir que o oficial mantém seus dados e funciona normalmente.

## Gates e próximos passos

1. Build Windows, contratos/inventário de UI e smoke tests precisam passar.
2. Evidências automatizadas de controles/contexto nos temas Claro/Preto não substituem teste real de DPI, captura/clipboard e vídeo no computador do usuário.
3. Registrar aprovação ou divergências nesta issue, com tamanho da janela, escala/tema, arquivo e sequência de ações.
4. Manter #63 aberta e PR #72 em rascunho enquanto houver gates de Captura/overlay pendentes. Não publicar 3.3.0 nem migrar outras telas antes da aprovação.
5. A janela legada fica como fallback interno/rollback nesta fase; sua retirada física só acontece depois da paridade aprovada. O fluxo principal da Captura usa o editor unificado.
6. Editor temporal de vídeo/GIF: outra etapa, explicitamente fora deste candidato.

## Meus emojis e barra nativa — etapa da issue #63

### Meus emojis
1. Na ferramenta Emotes, abra **Ver todos → Meus emojis → Adicionar imagem**. Importe PNG transparente e JPEG; o nome inicial vem do arquivo.
2. Selecione o item, ajuste o tamanho e insira na captura. Compare prévia, Copiar e Salvar.
3. Apague o arquivo de origem e reinicie o piloto: a coleção deve continuar disponível. Os dados ficam em `SlashDeskPilotData/capture-emotes`, separados dos assets de atalhos.
4. Remova um item da coleção com uma imagem editada ainda aberta. As marcações já inseridas, desfazer/refazer e a exportação devem permanecer iguais.
5. Cancele o seletor de arquivos e tente um arquivo inválido. Não deve surgir item nem operação na imagem. GIF animado não faz parte desta etapa.
6. A importação aceita até 16 MB/16 megapixels e normaliza uma cópia para no máximo 512 px no lado maior. Preserve a pasta inteira do piloto para manter a coleção.

### Barra de captura do piloto
1. Faça uma captura de **Região** por Novo, atalho e bandeja. O desktop deve permanecer congelado durante a seleção.
2. Use **Selecionar** para mover a área. Arraste as oito alças para reduzir/expandir a região. Nenhuma borda deve inverter ou sair da área virtual. Setas movem a seleção; Shift+setas acelera; foco em uma alça + setas redimensiona.
3. Troque para Caneta, Marca-texto, Formas, Seta, Texto, Número e Emotes. Formas oferece retângulo/elipse/linha/seta e opções de preenchimento/contorno. Mais ferramentas oferece Desfocar/Pixelizar e instrumentos que migram para o menu em monitores estreitos.
4. Cores/RGB usa a mesma faixa de cores da tela de Captura. Verifique texto/fonte, tamanho, opacidade, espessura, Sem cor, numeração, catálogo Noto e Meus emojis.
5. Alterar a região conserva as anotações na posição do conteúdo do desktop: reduzir a seleção recorta as marcas, e expandir novamente as revela. **R/refazer seleção** inicia outra seleção e limpa o histórico anterior, como antes.
6. Desfaça/refaça e use o menu Capturar → Copiar/Salvar ou Capturar conforme configuração. A composição final deve corresponder à prévia, inclusive transparência, desfoque e pixelização. Edição de vídeo/GIF permanece fora deste fluxo.
7. Esc fecha primeiro o popup; outro Esc cancela. Texto em edição deve aceitar suas próprias teclas. Cancelar não salva nem copia nada.
8. Teste em Claro/Preto/Windows, animações do Windows desativadas, cantos, taskbar nas bordas, monitores à esquerda/acima e DPI 100/150/200%. A barra deve se reposicionar sem piscar; popups não podem cortar controles. Em largura insuficiente, use Mais ferramentas.

### Evidências e limites
A galeria automatizada usa os componentes reais para registrar a barra normal/compacta e Meus emojis em Claro/Preto. Testes verificam importação, transparência, persistência, remoção sem invalidar o documento, clipboard e geometria das oito alças. O Windows físico ainda precisa validar mouse/teclado, foco dos popups, fluidez e monitores/DPI mistos; snapshots não substituem essa aprovação. A #63 permanece aberta até esse teste.
