# Piloto 3.3.0 — Acento Rápido (#65)

## Revisão v2 — Linhas compactas

Layout aprovado em 09/10/2026: blocos com cantos de 4 px, intervalo de 14 px, ativação única no topo, prévia compacta, idiomas em grade e seções avançadas recolhidas. O piloto abre diretamente em Acento Rápido.

O rótulo do switch indica a preferência **Ativado/Desativado**. A descrição ao lado distingue **Pronto para usar** (hook iniciado), **Pausado neste aplicativo** (processo em primeiro plano excluído) e **Indisponível** (hook não iniciado). Durante a inicialização, aparece **Preparando o Acento Rápido**. A consulta é local, a cada 500 ms enquanto a tela está visível; não registra o aplicativo em primeiro plano nem o texto digitado. O monitor geral de atalhos tem identificação separada.

Se a inicialização falhar, desligar e ativar novamente tenta iniciar o recurso. Desligar também cancela uma abertura pendente. O teste local continua disponível quando a preferência está ativada, mesmo que o hook global esteja indisponível: ele não depende da inserção em outro aplicativo.

Extraia o ZIP em uma pasta separada e execute `Abrir-Piloto-Acento-Rapido.cmd` ou `SlashDesk.exe`.
Os dados ficam em `SlashDeskPilotData` ao lado do executável. O piloto não usa os dados da instalação oficial nem oferece atualização automática. A Captura integrada, com OCR e áudio, também está incluída.

## O que avaliar

- Abra **Acento Rápido**. Ative o recurso e escolha a letra no topo da prévia. Os caracteres refletem os conjuntos selecionados.
- Clique nos caracteres para inseri-los no campo de teste. Selecione um trecho e confira a substituição; **Limpar teste** apaga somente esse campo. Os cliques não contabilizam uso global.
- Teste **Somente PT-BR**, **Selecionar todos** e as caixas dos conjuntos individuais. Ao desmarcar o último, Português (Brasil) permanece selecionado. Moedas em E incluem €; uma letra sem opções mostra uma mensagem.
- Altere Espaço/seta esquerda/seta direita. Abra **Aparência e ordem dos caracteres** para ajustar posição, Unicode e prioridade por uso. As preferências são salvas automaticamente e restauradas ao reiniciar.
- Clique nos botões de **100, 200 e 500 ms** e confira o destaque do valor atual. O campo numérico continua aceitando qualquer inteiro de 0 a 2.000 ms; 150 ms, por exemplo, não destaca nenhum preset. Enter confirma. Valores inválidos mostram aviso e preservam o último número válido; desligar o recurso continua possível.
- Abra **Aplicativos excluídos**. Digite processos separados por `;` ou por linha, como `mstsc.exe; game.exe`, saia do campo e reinicie para conferir a lista e o resumo. Confira a pausa enquanto o aplicativo excluído estiver em primeiro plano e a volta para Pronto para usar ao sair dele. A lista usa correspondência exata de nome, sem distinguir maiúsculas.
- Use o botão **?** para a ajuda pesquisável e **Mostrar na tela** para localizar os controles.
- Confira Claro, Escuro e Sistema; janela mínima; Tab/Shift+Tab, Espaço/Enter, setas e foco visível; a preferência de reduzir animações do Windows.

## Digitação real no Windows

O temporizador, hook, inserção e painel flutuante existentes continuam em uso. A prévia não envia teclas para outros aplicativos.

1. Em um editor de texto, segure A e toque na tecla de ativação. Mantenha as teclas pelo atraso configurado; cada novo toque avança. Solte A para inserir. Esc cancela.
2. Faça um toque mais curto que 100/200 ms e confira a digitação normal. Verifique que manter a combinação abre o painel sem depender de repetição automática.
3. Solte antes do atraso, cancele com Esc, troque de aplicativo, desligue/reative e confira que não ocorre inserção atrasada ou duplicada.
4. Teste Caps Lock, Shift e Caps Lock + Shift nos layouts US e PT-BR/ABNT. Confira maiúsculas e minúsculas.
5. Confira as exclusões no aplicativo indicado. Dentro do SlashDesk, teste Nome, Categoria e conteúdo do editor: o Acento Rápido funciona e não dispara expansão de atalhos nesses campos.
6. Reinicie o piloto e confira todas as preferências.

Os testes automáticos cobrem controles WPF reais, persistência isolada, conjuntos/Unicode, seleção e cursor, presets/atraso válido/inválido, três teclas/posições, temas, layout mínimo, seções recolhidas e renderização a 100/125/150/200%. A ausência real de hook não indica Pronto para usar; as imagens dos quatro estados usam condições explicitamente injetadas no fixture sem hooks. A regressão cobre a resolução dos estados, exclusões, temporizador independente, cancelamento, reagendamento e Caps/Shift. Teclado físico, alternância real de foco, qualidade visual em monitores com DPI diferente e aplicativos externos exigem esta revisão manual.

Referência de identidade: contrato congelado da publicação 11, em `docs/design/3.3.0/contract/`, preservado sem alterações. A composição desta tela segue a proposta Linhas compactas aprovada. O slider antigo foi substituído pelo campo numérico e presets; a prévia por letra substitui a lista redundante de caracteres. O inventário histórico registra esses dois equivalentes sem apagar os demais controles. O atraso de 0–2.000 ms e os conjuntos reais preservam o produto existente. O painel flutuante permanece com o serviço existente; sua migração visual pertence à #69. Seleção assistida de processos e integração com Modo Jogo continuam como sugestões futuras.
