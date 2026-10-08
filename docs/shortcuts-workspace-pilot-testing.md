# Piloto Atalhos 3.3.0 — issue #64

Este pacote abre diretamente em Atalhos e conserva a Captura aprovada na #63.
É um executável de desenvolvimento. A versão oficial publicada continua em 3.2.0.

## Abrir e preservar os dados

1. Extraia o ZIP em uma **pasta nova**, diferente da instalação oficial.
2. Saia do SlashDesk pelo menu da bandeja. O piloto usa o mesmo controle de instância.
3. Execute `Abrir-Piloto-Atalhos.cmd` ou o `SlashDesk.exe` deste pacote.
4. O título identifica `Piloto Atalhos 3.3.0 · #64`; os dados ficam em `SlashDeskPilotData`.
5. Para continuar os testes anteriores, copie a **pasta inteira** `SlashDeskPilotData` do piloto da Captura, com o app fechado. Preserve assets, emojis pessoais e demais arquivos. Trabalhe com uma cópia.

O piloto não migra a pasta oficial nem verifica/instala atualizações ou inicia com Windows.
O metadado de piloto está no próprio EXE: abrir sem o CMD também mantém o isolamento.
Capturas novas usam o destino do piloto quando ainda não há preferências. Se copiar preferências anteriores, confira o destino configurado antes de capturar.

## Roteiro principal

| Teste | Resultado esperado |
|---|---|
| Novo atalho | Nome, comando `/teste`, categoria e conteúdo podem ser preenchidos. Salvar cria um atalho real. |
| Texto simples | Quebras de linha e variáveis permanecem após salvar, sair e reabrir. |
| Texto formatado | Barra agrupada em fonte/tamanho, estilo/cores, parágrafo/listas e inserção. Fontes instaladas, B/I/U, cores, marca-texto, imagens, links, listas, tabelas e alinhamento (inclusive justificar) continuam disponíveis. Selecione um trecho: estilo e fonte acompanham a seleção; salvar/reabrir preserva o resultado. Desfazer/Refazer funcionam. |
| Prévia | Recolher fecha o olho; expandir abre o olho e conserva o conteúdo. |
| Ícones de categoria | Clique na pastinha no campo Categoria, escolha um dos oito ícones e salve. Todos os atalhos com essa categoria mostram o mesmo ícone na lista. Sair/reabrir preserva a escolha; trocar sem salvar e cancelar preserva a escolha pendente. Descartar a restaura. |
| Variáveis | Posicionar cursor ou selecionar trecho; clicar no token insere/substitui nessa posição. Prévia usa o mecanismo real. |
| Busca e categorias | Nome, comando, categoria e conteúdo filtram a lista; Todos/Mais usados usam dados reais. Estado vazio permite limpar filtros/criar. O × fica dentro do campo, aparece somente com texto e limpa a busca. |
| Rascunho | Editar sem salvar e clicar em outro atalho, Novo ou Importar: Cancelar mantém texto e seleção. Descartar alterações permite continuar. Escape ou clique fora também cancela. |
| Divisores | Arrastar; Tab até o divisor e usar setas; Home ou clique duplo restaura. Botão de variáveis oculta/mostra o painel sem apagar conteúdo. |
| Ajuda `?` | Busca, tópicos, demonstrações e Mostrar na tela (fixo no rodapé) funcionam. A indicação não muda formato ou rascunho. Clique fora do cartão fecha sem acionar os controles atrás; Escape/X/Entendi também fecham. |
| Comando inválido/duplicado | Erro mantém o conteúdo atual; `:` e `?` não passam a ser gatilhos. Legados incompatíveis são preservados. |
| Exclusão/importação | Exclusão usa uma janela com o tema do app e identifica o atalho. Cancelar/Escape/clique fora mantêm o atalho e rascunho; Enter prioriza Cancelar. Só Excluir atalho confirma. Importação real usa a origem selecionada em Configurações e cria backup. |
| Uso fora do app | Digitar `/teste` em um aplicativo de texto e confirmar usa o atalho salvo. Acento Rápido continua nos campos internos. |
| Temas e tamanho | Claro, Preto e Windows; janela normal/mínima, teclado e DPI do seu monitor. Nenhuma ferramenta deve ficar inacessível. |
| Reinício | Salvar, sair e reabrir: conteúdo e imagens permanecem. Fechar para bandeja mantém o rascunho somente em memória. |

No editor de conteúdo, Tab insere uma tabulação; Ctrl+Tab navega para o próximo controle.
Salvar o atalho grava o conteúdo; criar/editar não muda o arquivo até salvar.
Escolha a origem da importação em Configurações antes de usar Importar na tela Atalhos.

## Referência e diferenças deliberadas

- Contrato fixado em `docs/design/3.3.0/contract/reference/app/page.tsx` e `studio.css` (publicação 11).
- Ordem/hierarquia: cabeçalho, workspace, lista/categorias/busca, editor e variáveis.
- Reutiliza tokens, botões e movimento da Captura. Entrada 180 ms; controles 140 ms; redução respeita a fundação existente.
- Mantém o editor WPF real e todos os comandos de texto rico; não usa localStorage ou os dados fictícios do Lab.
- Mantém os limites e áreas de interação dos divisores aprovados na #54, com restauração por teclado/mouse.
- Mantém filtros Todos/Mais usados explícitos e a origem real de importação. A prévia pode ser recolhida.
- A ajuda visual reutiliza a mesma janela da Captura com 11 tópicos próprios.
- Ícones de categoria são opcionais, armazenados em `settings.json` e incluídos no backup completo. Importação/exportação apenas de `snippets.md` mantém conteúdo e nomes, mas não transporta os ícones. Não altera o formato dos atalhos. Se só a preferência visual falhar ao gravar, o status informa que o atalho foi salvo e pede repetir a escolha do ícone.
- Sem edição temporal de vídeo/GIF, alterações de armazenamento ou release nesta etapa.

## Evidência automatizada e limites

`--shortcuts-workspace-smoke <pasta>` usa uma fixture WPF sem hooks/bandeja/updater e dados temporários dentro da pasta de evidência.
Verifica rascunho e cancelamento (inclusive ícone pendente), inserção real de variável/prévia, salvar/reabrir texto simples/rico/imagem, persistência do ícone, olho da prévia, botão de limpar busca, filtro vazio, painel de variáveis e modo protegido.
Gera evidências Claro/Preto/Windows em 1440×900 e 980×680, com rasterização 100/125/150/200%.
Rasterização não substitui teste físico de DPI, posicionamento de monitores, entrada global ou interação manual.

A issue permanece aberta até aprovação visual e funcional do piloto.

## Fonte, tamanho e cores (08/10/2026)

- Selecione um trecho de texto formatado. Troque a fonte para Georgia ou Consolas e o tamanho para 24. Confira conteúdo e prévia, salve e abra novamente.
- Apague o conteúdo, escolha uma fonte antes de digitar e escreva uma frase. A fonte deve ser mantida.
- O tamanho 10,5 pt corresponde aos 14 pixels padrão do editor. Valores encontrados no conteúdo são inseridos na ordem numérica da lista, sem alterar o texto existente.
- Cor do texto e Marca-texto abrem a paleta no estilo da Captura, com cores principais, navegação horizontal e RGB/hexadecimal no final. Não há janela nativa de cores.
- Selecione um trecho, escolha uma cor e confira que somente esse trecho muda. RGB inválido (por exemplo 300) deve mostrar validação sem aplicar. Esc ou clique fora fecha a paleta.
- Salve e reabra o atalho para conferir fonte, tamanho, cor e marca-texto.

## Complementos de Atalhos — expandir, duplicar, favoritos/fixados e código

- **Expandir editor:** editar um rascunho, expandir, redimensionar a janela e recolher. Confirmar que conteúdo, formatação e estado de rascunho foram mantidos e que lista/variáveis voltaram ao estado anterior. A prévia fica abaixo durante a expansão. Salvar continua acessível.
- **Duplicar:** abrir Opções do atalho (três pontos) ou botão direito na lista, escolher Duplicar, alterar nome/comando e salvar. Conferir original intacto, ID distinto, comando sem conflito, conteúdo rico/imagens/código preservados. Duplicação não grava até Salvar.
- **Favoritos/fixados:** marcar/desmarcar pelo menu. Favoritos e Fixados filtram separadamente; fixados aparecem primeiro entre os resultados da busca/categoria. Fechar/reabrir e importar/exportar snippets.md deve conservar as preferências. Alterar uma preferência com um rascunho em edição não salva nem descarta esse rascunho.
- **Código:** selecionar Texto formatado, posicionar o cursor fora de listas/tabelas, clicar em Código junto a Link/Imagem/Tabela. Escolher linguagem, colar código e usar Salvar bloco. O bloco aparece no conteúdo e na prévia; o lápis abre a edição, recolher não altera o conteúdo, Copiar copia só o código. Salvar o atalho grava tudo.
- Testar texto antes/depois do bloco e vários blocos, undo/redo de inserção e edição, cancelar popup, código longo, sintaxe nos três temas e popup na janela mínima.
- Usar código com `_`, `*`, `<tags>`, aspas, tabs/espaços, linhas vazias, crases e `{{nome}}`/`{{tab}}`. Salvar/reabrir, copiar e expandir devem preservar esses caracteres; somente as variáveis fora do código são resolvidas.
- O realce de sintaxe não executa nem reformata o código. O app de destino pode ignorar a aparência HTML, mas deve receber o código literal no formato de texto simples.
- AvalonEdit 6.3.1.120 (MIT) é incorporado ao executável; licença em Assets/AvalonEdit/LICENSE.txt. Não há download de recursos em tempo de uso.

## Revisão — prévia expandida, alturas, editar código, emojis e divulgação

- Expandir editor mostra a prévia abaixo. Arrastar a alça inferior de conteúdo/prévia aumenta ou diminui cada campo; com foco na alça, ↑/↓ ajustam e Home restaura. Ações e Salvar ficam fixos; rolagem mantém o conteúdo longo acessível. As alturas são mantidas na sessão e cada modo tem sua altura.
- Clicar fisicamente no lápis de um bloco no conteúdo deve abrir Editar bloco de código com a linguagem/fonte correta. Alterar e Salvar bloco substitui somente aquele bloco; cancelar e undo/redo preservam o conteúdo. Testar depois de salvar/reabrir e de repetir undo/redo. A prévia é somente leitura.
- Emojis fica ao lado de Código em Texto formatado. Reutiliza busca, categorias, paginação e Meus emojis do catálogo Noto da Captura. Inserir entre palavras, salvar/reabrir, desfazer/refazer, copiar/expandir em aplicativo que aceite HTML/imagens. PNG próprio em assets conserva o visual; mover os dados requer a pasta inteira. Remover uma imagem da coleção de Meus emojis não remove a cópia no atalho. Apps somente texto recebem o texto alternativo.
- A mensagem inicial dos dados novos divulga o app com o link oficial releases/latest. Dados existentes não são sobrescritos. Para incluir nos seus dados anteriores, três pontos → Divulgar SlashDesk cria um rascunho novo com /slashdesk (ou comando livre numerado). Conferir conteúdo e salvar.
