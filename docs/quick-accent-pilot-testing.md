# Piloto 3.3.0 — Acento Rápido (#65)

Extraia o ZIP em uma pasta separada e execute `Abrir-Piloto-Acento-Rapido.cmd` ou `SlashDesk.exe`.
Os dados ficam em `SlashDeskPilotData` ao lado do executável. O piloto não usa os dados da instalação oficial nem oferece atualização automática. A Captura integrada, com OCR e áudio, também está incluída.

## O que avaliar

- Abra **Acento Rápido**. Ative o recurso e escolha a letra no topo da prévia. Os caracteres refletem os conjuntos selecionados.
- Clique nos caracteres para inseri-los no campo de teste. Selecione um trecho e confira a substituição; **Limpar teste** apaga somente esse campo. Os cliques não contabilizam uso global.
- Teste **Somente PT-BR**, **Todos** e os conjuntos individuais. Ao desmarcar o último, Português (Brasil) permanece selecionado. Moedas em E incluem €; uma letra sem opções mostra uma mensagem.
- Altere Espaço/seta esquerda/seta direita, posição, Unicode e prioridade por uso. As preferências são salvas automaticamente e restauradas ao reiniciar.
- Confira atrasos de **100 e 200 ms**, além de 0 e 2.000 ms. Enter confirma o campo numérico. Valores inválidos mostram aviso e preservam o último número válido; desligar o recurso continua possível.
- Digite processos excluídos separados por `;` ou por linha, como `mstsc.exe; game.exe`, saia do campo e reinicie para conferir a lista.
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

Os testes automáticos cobrem controles WPF reais, persistência isolada, conjuntos/Unicode, seleção e cursor, atraso válido/inválido, três teclas/posições, temas, layout mínimo e renderização a 100/125/150/200%. A regressão cobre temporizador independente, cancelamento, reagendamento e Caps/Shift. Teclado físico, alternância real de foco, qualidade visual em monitores com DPI diferente e aplicativos externos exigem esta revisão manual.

Referência visual: contrato congelado da publicação 11, em `docs/design/3.3.0/contract/`. O atraso de 0–2.000 ms e os conjuntos reais preservam o produto existente; a prévia elimina o ciclo automático de demonstração. O painel flutuante permanece com o serviço existente; sua migração visual pertence à #69.
