# Inventário funcional da interface — SlashDesk 3.0.0

Este inventário final compara o redesign com a linha de base 2.9.1 congelada no
commit `57ed4ebfe7857cd293f837c248330ccc142b9604`. A comparação automatizada é feita
por `scripts/compare-ui-inventory.ps1`.

## Resultado verificável

- 129 controles nomeados no `MainWindow.xaml` (linha de base: 123);
- 56 handlers XAML preservados (linha de base: 56);
- 13 janelas e componentes auxiliares preservados;
- seis destinos da navegação horizontal preservados;
- nenhum controle, handler, fluxo ou componente obrigatório da linha de base removido.

## Telas e fluxos preservados

- **Atalhos:** busca, categorias, lista, mais usados, criação, edição, exclusão,
  cópia, salvar, importação SlashDesk/Text Blaze/Espanso, texto formatado,
  hyperlinks, variáveis, Tab e prévia.
- **Acento Rápido:** prévia, tecla de disparo, atraso, posição, conjuntos,
  aplicativos excluídos, Caps Lock, ABNT e bloqueio de repetição.
- **Captura:** monitor, janela, região, atraso, cursor, editor, destino, histórico,
  abrir, copiar, editar, excluir, filtros e retenção.
- **GIF:** alvo real, presets fechados 10/20/30 FPS, qualidade, prévia, pausa,
  retomada, contador monotônico e finalização explícita.
- **MP4:** monitor, janela e região, presets, cursor, pausa, retomada, contador,
  finalização assíncrona, timeout, logs e descarte coordenado.
- **Estatísticas:** expansões, caracteres economizados, Acento Rápido, capturas,
  imagens, GIFs, MP4 e atalhos mais usados, somente com dados reais.
- **Configurações:** temas Claro/Escuro/Seguir o Windows, inicialização, bandeja,
  atalhos globais, captura, histórico, backup/restauração e atualização.
- **Sobre:** produto, privacidade, documentação, novidades, suporte, GitHub,
  versão, armazenamento e Releases.

## Componentes auxiliares preservados

`CaptureEditorWindow`, `GifPreviewWindow`, `OnboardingWindow`, `PromptDialog`,
`QuickAccentWindow`, `RecordingControlWindow`, `RegionCaptureWindow`,
`RegionSelectionWindow`, `ShortcutRecorderBox`, `SuggestionWindow`,
`UpdateAvailableWindow`, `UpdateProgressWindow` e `VariableInputWindow`.

## Estados preservados

Estados vazios, validação, carregamento, erro, sucesso, seleção, gravação pronta,
selecionando alvo, gravando, pausada, retomando, finalizando, concluída e falha;
verificação offline/sem atualização/disponível; download, cancelamento,
substituição e rollback; migração, backup e recuperação.

## Mudanças visuais

O redesign centraliza paleta, tipografia, espaçamento, raios, ícones vetoriais,
botões, campos, switches, cards, status, progresso e foco. O mockup é seguido na
hierarquia e densidade, mas funções reais ausentes no desenho continuam visíveis.
Imagens capturadas não recebem tonalidade do tema e seleções não dependem apenas
de cor.
