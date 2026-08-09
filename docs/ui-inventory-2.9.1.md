# Inventário funcional da interface — SlashDesk 2.9.1

Este documento congela a linha de base visual e funcional anterior ao redesign
3.0.0. A origem é a `main` após o merge do PR #20, commit
`57ed4ebfe7857cd293f837c248330ccc142b9604`.

O manifesto verificável está em `scripts/ui-inventory-2.9.1.json`. Ele contém os
123 controles nomeados e os 56 handlers XAML. O script
`scripts/compare-ui-inventory.ps1` compara a implementação atual com esta base e
falha quando uma função desaparece silenciosamente.

## Shell e navegação

- Janela WPF x64, mínimo 980 × 680, com cabeçalho, marca, estado do monitor e
  navegação horizontal.
- Abas: Atalhos, Acento Rápido, Captura, Estatísticas, Configurações e Sobre.
- Histórico permanece dentro de Captura.
- A aba selecionada usa estado visual explícito; o redimensionamento reorganiza
  Atalhos sem recriar serviços.
- Temas: Claro, Escuro e Seguir o Windows, aplicados pelo `ThemeService`.

## Atalhos

- Busca por nome, comando e categoria; categorias e mais usados.
- Criar, selecionar, editar, excluir e salvar snippets.
- Nome, comando, categoria e formatos texto simples/texto formatado.
- Editor rico: fonte, tamanho, negrito, itálico, sublinhado, cor, marca-texto,
  hyperlink, imagem, listas, tabela e alinhamento.
- Preview, indicação de estado e layout responsivo em três áreas.
- Variáveis de campos, Tab, data/hora, partes da data e sistema.
- Expansão `/` e `:`, campos interativos, hyperlinks, formatação e estatísticas de
  uso permanecem fora da camada visual.

## Acento Rápido

- Prévia animada com oito opções e seleção visível.
- Ativação por Espaço, seta esquerda ou seta direita.
- Atraso, posição, código Unicode e ordenação por uso.
- Conjuntos: português, espanhol, francês, alemão, italiano, nórdicos, Europa
  Central, moedas e símbolos especiais.
- Seleção rápida PT-BR/todos, prévia dos caracteres e aplicativos excluídos.
- Preserva Caps Lock, ABNT/dead keys, bloqueio de auto-repeat e estatísticas.

## Captura, gravação e histórico

- Monitor ativo, região, janela e captura com rolagem experimental.
- Atalhos configuráveis por teclado, Print Screen, F10, roda e botões do mouse.
- Delay 0/3/5/10 s; cursor; clipboard; salvamento automático; editor integrado.
- PNG/JPEG, qualidade, destino e templates de nome/pasta.
- MP4: monitor/região/janela, FPS, qualidade, cursor, pausa, retomada,
  finalização assíncrona, timeout, validação e arquivo atômico.
- GIF: região real, 10/20/30 FPS, qualidade, pausa/retomada, contador monotônico,
  fila limitada, preview e salvamento atômico com repetição NETSCAPE2.0.
- Histórico de imagem/GIF/MP4 com filtros, miniatura, data, tipo, tamanho, caminho,
  abrir, copiar, editar, excluir, limpar e retenção.
- Estados: pronto, selecionando alvo, gravando, pausado, retomando, finalizando,
  concluído e falha.

## Estatísticas

- Total de expansões, caracteres economizados, Acento Rápido e capturas.
- Ranking de atalhos, atalhos ativos, caractere favorito e capturas por tipo.
- Tempo economizado e média de caracteres por expansão.
- Somente dados locais reais; a linha de base não possui séries temporais que
  autorizem gráficos ou comparações inventadas.

## Configurações e Sobre

- Tema, fechar para bandeja, iniciar com Windows, sugestões e atualização
  automática.
- Importação SlashDesk (`snippets.md`), Text Blaze (JSON) e Espanso (YAML).
- Backup manual/diário, restauração, pasta e retenção.
- Versão instalada, canal estável, última verificação, resultado, verificar agora
  e notas da versão.
- Sobre: produto, privacidade, licença MIT, projeto GitHub e dados locais.

## Janelas, overlays e controles auxiliares

1. `CaptureEditorWindow`: editor de captura, toolbar, imagem, desenho, blur,
   pixelização, recorte, resize, undo/redo, concluir/cancelar.
2. `RegionCaptureWindow`: seleção e edição inline de região, alças, dimensões,
   selecionar novamente e ferramentas de anotação.
3. `RegionSelectionWindow`: seletor simples usado pelos fluxos de gravação.
4. `RecordingControlWindow`: contador, pausar/retomar/finalizar e estado finalizando.
5. `GifPreviewWindow`: preview antes de salvar e cancelamento por Escape.
6. `VariableInputWindow`: coleta de variáveis antes da expansão.
7. `PromptDialog`: entradas de tabela, hyperlink, texto e resize.
8. `OnboardingWindow`: apresentação inicial e confirmação de armazenamento.
9. `QuickAccentWindow`: popup flutuante de caracteres.
10. `SuggestionWindow`: sugestões ao digitar gatilhos.
11. `UpdateAvailableWindow`: Atualizar agora, Lembrar depois e Ignorar versão.
12. `UpdateProgressWindow`: download, progresso real, cancelamento e aplicação.
13. `ShortcutRecorderBox`: captura teclado, Print Screen, roda e botões do mouse.

Também existem `FolderBrowserDialog`, `OpenFileDialog`, `ColorDialog` e mensagens
WPF para seleção de pasta, importação, restauração, cores, validações, erros e
confirmações.

## Menu da bandeja

- Abrir SlashDesk.
- Novo atalho.
- Separador.
- Sair.
- Duplo clique restaura a janela; fechar pode manter o processo na bandeja.

## Teclado e comandos

- Navegação por Tab/Shift+Tab, Enter, Espaço e Escape conforme controles WPF.
- Editor: Ctrl+Z e Ctrl+Y; Escape cancela.
- Região e preview GIF: Escape cancela.
- Atalhos globais aceitam modificadores Control, Alt, Shift e Windows.
- Captura registra três ações independentes e impede conflitos.
- A implementação é majoritariamente event-driven; os handlers XAML são a API
  funcional entre layout e code-behind e estão enumerados no manifesto.

## Estados e mensagens obrigatórios

- Vazios: nenhuma seleção, busca sem resultado, nenhuma captura, histórico vazio,
  ranking vazio, nenhuma atualização verificada.
- Carregamento: inicialização, backups, verificação/download de atualização,
  captura e finalização de mídia.
- Erro: validação de snippet, atalho em conflito, captura/gravação, arquivo ausente,
  importação/restauração, migração e atualização/rollback.
- Confirmações: excluir snippet/histórico, limpar histórico, restaurar backup,
  aplicar atualização, reiniciar e cancelar operações.
- Atualização: offline, atual, disponível, ignorada, adiada, download, checksum,
  preparação, substituição, confirmação e rollback.
- Migração: origem portátil, origem instalada/legada, duas origens, backup,
  staging, validação, ativação e recuperação.

## Invariantes fora do redesign

- `SlashDeskData` portátil e `%LocalAppData%\SlashDesk` instalado.
- `snippets.md`, configurações, categorias, estatísticas, histórico, assets,
  backups, logs e estado do atualizador preservados.
- Atualização troca somente `SlashDesk.exe`; checksum, PE x64, cópia de recuperação
  e rollback permanecem inalterados.
- ScreenRecorderLib 6.6.0, GIF, MP4, serviços de armazenamento, migração e update
  não serão reescritos por estética.
