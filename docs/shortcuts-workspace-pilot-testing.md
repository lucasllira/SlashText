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
| Texto formatado | Fontes, tamanho, B/I/U, cores, marca-texto, imagens, links, listas, tabelas e alinhamento continuam disponíveis. Salvar/reabrir preserva o resultado. |
| Variáveis | Posicionar cursor ou selecionar trecho; clicar no token insere/substitui nessa posição. Prévia usa o mecanismo real. |
| Busca e categorias | Nome, comando, categoria e conteúdo filtram a lista; Todos/Mais usados usam dados reais. Estado vazio permite limpar filtros/criar. |
| Rascunho | Editar sem salvar e clicar em outro atalho, Novo ou Importar: escolher Não mantém texto e seleção. Escolher Sim permite descartar. |
| Divisores | Arrastar; Tab até o divisor e usar setas; Home ou clique duplo restaura. Botão de variáveis oculta/mostra o painel sem apagar conteúdo. |
| Ajuda `?` | Busca, tópicos, demonstrações e Mostrar na tela funcionam. A indicação não muda formato ou rascunho. |
| Comando inválido/duplicado | Erro mantém o conteúdo atual; `:` e `?` não passam a ser gatilhos. Legados incompatíveis são preservados. |
| Exclusão/importação | Confirmações/cancelamento preservam dados; importação real usa a origem selecionada em Configurações e cria backup. |
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
- Sem edição temporal de vídeo/GIF, alterações de armazenamento ou release nesta etapa.

## Evidência automatizada e limites

`--shortcuts-workspace-smoke <pasta>` usa uma fixture WPF sem hooks/bandeja/updater e dados temporários dentro da pasta de evidência.
Verifica rascunho e cancelamento, inserção real de variável/prévia, salvar/reabrir texto rico/imagem, filtro vazio, painel de variáveis e modo protegido.
Gera evidências Claro/Preto/Windows em 1440×900 e 980×680, com rasterização 100/125/150/200%.
Rasterização não substitui teste físico de DPI, posicionamento de monitores, entrada global ou interação manual.

A issue permanece aberta até aprovação visual e funcional do piloto.
