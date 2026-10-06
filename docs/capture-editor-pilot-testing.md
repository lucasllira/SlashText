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
