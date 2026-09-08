# Fundação visual 3.3.0 — #62

Parte de #61, piloto seguinte #63. Base funcional: v3.2.0, commit `406022943ab13d0d78399b2a797210ae5106b0e3`.

## Referência reproduzível

`contract/` contém **todos os 11 arquivos extraídos sem alteração** do ZIP aprovado da publicação 11. `contract-manifest.json` registra revisão do Site, URL de origem, SHA-256 do ZIP e SHA-256 de cada arquivo extraído. O conteúdo fica recuperável pelo git; o arquivo ZIP em si não é necessário para compilar e não precisa ser recomposto byte a byte. Não confundir a revisão do Site `34de2fafbf9b1de1e15e0e7c2d5e107425cc2be3` com um commit do desktop.

O contrato web é referência visual, não implementação nativa. Os XAML do pacote são apenas sementes; os templates compiláveis estão em `src/SlashText/Styles/VisualLab`. O CSS conserva prioridade sobre o resumo JSON para exceções responsivas, switch e estados. Veja [parity.md](parity.md) e [ui-inventory.json](ui-inventory.json) antes de migrar controles.

## Uso na próxima etapa

1. Mesclar esta fundação somente após checks e revisão manual; usar a main resultante como base da #63.
2. Mesclar `Styles/VisualLab/Components.xaml` no escopo da tela que está sendo migrada.
3. Aplicar estilos **explicitamente**: `Style="{StaticResource Lab.Button}"`, `Lab.PrimaryButton`, `Lab.Field`, `Lab.Combo`, `Lab.Switch`, `Lab.Tab`, `Lab.Card`, `Lab.Popup`.
4. Usar `DynamicResource Lab.text`, `Lab.panel` e demais cores semânticas. `ThemeService.Apply` fornece a nova paleta em paralelo à antiga. Não copiar valores de exemplo para AppSettings.
5. Onde já existe serviço/handler, preservar sua implementação e conectar o componente real. Nunca usar handlers simulados da galeria ou do React em produção.
6. Para ícones: `LabIcon Kind="Camera" Width="18" Height="18"` com Foreground dinâmico. 54 nomes extraídos do Lab em `lucide-nodes.json`; recursos conservam viewport 24×24 e stroke 2 em coordenadas de origem (1,5 DIP ao renderizar 18 DIP).

Os templates usam `LabContentPresenter` para vincular a cor dos rótulos string gerados ao Foreground do componente, preservando AccessText/teclas de acesso e protegendo contra o estilo implícito legado de TextBlock. Conteúdo UIElement e DataTemplates autorais não são alterados.

Paleta base: Claro/Preto com 16 cores do contrato. `Lab.error` é extensão explícita para validação (não presente no Lab). Fonte: Segoe UI Variable Text/Display → Segoe UI; mono Cascadia Mono → Consolas. Nenhuma fonte de ícones instalada é exigida. A aparência da fonte precisa ser conferida no Windows 11 e no fallback. Licença completa Lucide/Feather distribuída como EmbeddedResource `Assets/Lucide/LICENSE.txt`.

## Movimento

`LabMotion` é opt-in. Botão: escala ativa de 1→.98/140 ms; switch: deslocamento 0→18 DIP/180 ms (exceção CSS); página: Y 5→0, opacidade .65→1/180 ms; popup: Y 6→0, escala .98→1, opacidade 0→1/180 ms. Cores de tema: 200 ms. Curvas do contrato via KeySpline; movimentos de entrada usam Storyboard. Movimentos interrompidos mantêm valor final estável e substituem clocks anteriores.

`LabMotion.Reduced` é herdável. A galeria observa também `SystemParameters.ClientAreaAnimation` e mudanças do Windows em execução, propagando a preferência explicitamente para Popup. A galeria não grava preferência nova em AppSettings. Na #63/#67 definir a integração de produção com o contrato de preferências existente, sem mudar defaults.

`LabMotion.Entrance="Page"` ou `"Popup"` reserva o RenderTransform do container opt-in. Não aplicar sobre seleção/retângulo/elementos cujas coordenadas físicas são medidas, nem sobre transform já usado pela função nativa. Hover de cores usa estados WPF imediatos nesta base; o piloto deve avaliar se a transição de cor precisa ser interpolada para maior fidelidade. O realce do botão primário aproxima o brightness(1.06) CSS com sobreposição branca de 6%; registrar a diferença, não declarar identidade pixel a pixel.

## Galeria isolada

Baixar o artefato `SlashDesk-visual-foundation-gallery-win-x64` do PR, extrair em uma pasta separada e executar `Abrir-Galeria.cmd`.

Equivalente: `SlashDesk.exe --design-gallery`. Sem o argumento, o executável inicia o app normal. A galeria retorna **antes** de AppPaths, atualização, mutex, hooks e serviços de captura. Seus controles são exemplos locais, sem operações externas e sem acesso aos dados pessoais.

Testar:

- Claro, Preto e Windows (alterar o tema do Windows com a galeria aberta).
- Tab/Shift+Tab: foco visível; Espaço/Enter: ações e switches; setas: abas/ComboBox; Escape: popup.
- Mouse hover/pressionado, clique repetido, desativados não respondem.
- Campo obrigatório começa inválido, corrige ao digitar e volta a inválido ao limpar.
- Popup/ComboBox abertos, textos e opções legíveis em ambos os temas; concluir/clicar fora/Escape; foco volta à origem.
- Reduzir movimentos: estados continuam mudando sem animação; repetir entrada; interromper alternando controles/tema rapidamente.
- Janela mínima 980×680 e maior; 100/125/150/200%, monitores com DPI diferente; conteúdos excedentes acessíveis via scroll, sem sobreposição.
- Abrir o app normal separadamente com cópia de dados representativos para regressão da 3.2.0. Não usar a galeria como evidência de captura/atalhos funcionais.

## Verificações automáticas e limites

`visual-foundation-smoke.ps1` verifica SHA dos arquivos fixados, paletas e ausência de estilos implícitos globais. A Build Windows compila XAML e executa `--design-gallery-smoke <pasta>` antes dos smokes existentes. O modo de teste escreve apenas na pasta de evidência informada.

Teste da galeria exercita recursos dinâmicos de campo/ComboBox/Popup, validação, switch, interrupção e movimento reduzido. Gera 16 PNGs offscreen: Claro/Preto, 1440×900 e 980×680 DIP, rasterizados a 100/125/150/200%. São testes de renderização, **não de mudança física de monitor ou DPI**, nem screenshots de todas as telas futuras. Se o runner desabilitar animações, a política é respeitada; teste temporal/visual exige sessão interativa.

Aprovação manual pendente até registrada no PR/issue. Não encerrar #62 apenas por build verde e não iniciar todas as telas em conjunto. O gate de fidelidade completa pertence ao piloto #63.

## Retomada

Ler #61, #62, comentários e PR associado. Confirmar SHA e checks atuais. Se a galeria tiver divergência, corrigir aqui e repetir os checks afetados. Após aprovação, seguir para #63. A cada pausa registrar: SHA, concluído, testes reais, testes não feitos, diferenças e próxima ação exata. Essa rotina vale também para GPT 5.6 Sol Média; não depende de um modelo específico.
