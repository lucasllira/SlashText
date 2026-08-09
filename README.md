# SlashDesk

UtilitÃ¡rio portÃ¡til e local para Windows que reÃºne expansÃ£o de texto, Acento
RÃ¡pido e captura de tela. Foi pensado para ambientes pessoais e corporativos
onde instalar vÃ¡rios aplicativos ou enviar conteÃºdo para a nuvem nÃ£o Ã© uma opÃ§Ã£o.

> O projeto continua no repositÃ³rio `SlashText` para preservar links e histÃ³rico.
> A partir da versÃ£o 2.0, o produto e o executÃ¡vel se chamam **SlashDesk**.

## Recursos

### Interface

- design system prÃ³prio com grafite, branco quente e ciano funcional;
- temas claro, escuro ou sincronizado com o Windows;
- menu horizontal compacto, foco visÃ­vel e estados claros de seleÃ§Ã£o;
- layout responsivo a partir do tamanho mÃ­nimo de 980 Ã— 680;
- onboarding e editor de captura usando os mesmos componentes visuais.

### Atalhos de texto

- atalhos iniciados por `/` ou `:` em Outlook, Teams, navegadores e outros apps;
- texto simples ou formatado com fonte, tamanho, cores, marca-texto, listas,
  alinhamento, tabelas, imagens e hiperlinks;
- variÃ¡veis preenchÃ­veis, datas automÃ¡ticas, cÃ¡lculos de data e `{{tab}}`;
- sugestÃµes flutuantes, preview e estatÃ­sticas locais.
- importaÃ§Ã£o de `snippets.md`, exportaÃ§Ãµes JSON do Text Blaze e arquivos YAML
  do Espanso, com conversÃ£o das variÃ¡veis compatÃ­veis e proteÃ§Ã£o contra conflitos.

### Acento RÃ¡pido

- conjuntos configurÃ¡veis, incluindo somente PortuguÃªs (Brasil);
- suporte a Caps Lock, Shift e layouts de teclado diferentes;
- avanÃ§o previsÃ­vel de uma opÃ§Ã£o por toque na tecla de ativaÃ§Ã£o, sem o
  auto-repeat do Windows pular caracteres;
- posiÃ§Ã£o, atraso, ordenaÃ§Ã£o e aplicativos excluÃ­dos configurÃ¡veis.

### Captura local

- monitor ativo;
- seleÃ§Ã£o livre de regiÃ£o;
- reconhecimento da janela sob o cursor;
- atalho global independente para cada aÃ§Ã£o, gravado ao pressionar a combinaÃ§Ã£o;
- teclas de funÃ§Ã£o, `Print Screen`, teclado, roda, botÃ£o central e botÃµes
  laterais do mouse;
- combinaÃ§Ãµes como `PrintScreen`, `F10`, `Ctrl+Shift+WheelUp` e `Alt+MouseX1`;
- pasta automÃ¡tica com variÃ¡veis `{year}`, `{month}`, `{month-name}` e `{day}`;
- nome com `{date}`, `{time}`, `{type}` e `{app}`;
- PNG ou JPEG com qualidade configurÃ¡vel;
- ediÃ§Ã£o durante a prÃ³pria seleÃ§Ã£o de regiÃ£o, sem abrir outra janela, com seta,
  marca-texto, retÃ¢ngulo, cÃ­rculo, lÃ¡pis, texto, numeraÃ§Ã£o, cores, espessura,
  desfazer e refazer;
- aÃ§Ãµes de copiar, salvar ou concluir usando a regra ativa;
- salvamento automÃ¡tico, clipboard e histÃ³rico local das Ãºltimas capturas;
- estatÃ­sticas integradas de atalhos, acentos e capturas por tipo;
- sem upload, conta ou compartilhamento externo.

## Primeira inicializaÃ§Ã£o e atualizaÃ§Ãµes

Na primeira abertura, o SlashDesk apresenta as funÃ§Ãµes principais e explica onde
os dados permanecem. A verificaÃ§Ã£o em background consulta as Releases oficiais
de `lucasllira/SlashText`, ignora drafts e prereleases no canal estÃ¡vel e pode ser
desativada em **ConfiguraÃ§Ãµes**. Ela nÃ£o envia atalhos, capturas, estatÃ­sticas ou
identificadores pessoais.

## Arquivos portÃ¡teis

O ZIP portÃ¡til contÃ©m apenas:

```text
SlashDesk.exe
```

O runtime self-contained e os componentes nativos sÃ£o incorporados ao executÃ¡vel.
Quando necessÃ¡rio, o .NET os extrai no cache interno de bundle em `%TEMP%\.net`.
Na ediÃ§Ã£o portÃ¡til, os dados permanentes ficam ao lado do executÃ¡vel:

```text
SlashDesk.exe
SlashDeskData/
```

Na ediÃ§Ã£o instalada, permanecem em `%LocalAppData%\SlashDesk`. Em ambos os modos,
a origem contÃ©m os mesmos nomes e formatos:

- `settings.json`: preferÃªncias;
- `usage.json`: contadores locais;
- `capture-history.json`: tipo, horÃ¡rio, tamanho e caminho das capturas recentes;
- `assets/`: imagens usadas nos atalhos;
- `Backups/`: um ZIP por dia ou sob demanda, com restauraÃ§Ã£o e retenÃ§Ã£o das sete
  cÃ³pias mais recentes.
- `Logs/`: diagnÃ³sticos locais sem conteÃºdo de snippets ou capturas;
- `Updates/`: temporÃ¡rios controlados da atualizaÃ§Ã£o portÃ¡til.

A primeira execuÃ§Ã£o portÃ¡til prioriza um `SlashDeskData` vÃ¡lido jÃ¡ existente. Se
ele ainda nÃ£o existir, os dados legados de `%LocalAppData%\SlashDesk` sÃ£o copiados
para staging, validados, respaldados e sÃ³ entÃ£o ativados. A origem antiga nÃ£o Ã©
apagada. Se as duas origens existirem, a origem portÃ¡til prevalece e a outra Ã©
preservada em backup, sem mesclagem destrutiva.

A publicaÃ§Ã£o instalada usa o perfil `Installed` e gera a pasta self-contained
que serÃ¡ consumida por um instalador futuro. A publicaÃ§Ã£o portÃ¡til usa o perfil
`Portable` e gera um Ãºnico executÃ¡vel self-contained `win-x64`. Para atualizar o
portÃ¡til, o SlashDesk valida o ZIP e o SHA-256, encerra o processo principal e usa
uma cÃ³pia temporÃ¡ria do prÃ³prio executÃ¡vel para substituir atomicamente somente
`SlashDesk.exe`. Se a nova versÃ£o nÃ£o confirmar a inicializaÃ§Ã£o, o executÃ¡vel
anterior Ã© restaurado. `SlashDeskData` nunca Ã© incluÃ­do nem substituÃ­do.

A compilaÃ§Ã£o instalada ainda nÃ£o oferece atualizaÃ§Ã£o automÃ¡tica: enquanto nÃ£o
existir um instalador transacional validado, ela abre a Release oficial para
atualizaÃ§Ã£o manual e mantÃ©m `%LocalAppData%\SlashDesk` fora do staging.

## VariÃ¡veis de texto

| VariÃ¡vel | Resultado |
|---|---|
| `{{nome}}` | Solicita um valor antes de inserir |
| `{]ú×Ÿm¢G§²ÚîÆ­yÒFF–ærÒæWrF†–6¶æW72ƒ‚’ÀĞ¢VffV7BÒæWr7—7FVÒåv–æF÷w2äÖVF–äVffV7G2äG&÷6†F÷tVffV7@Ğ¢°Ğ¢&ÇW%&F—W2ÒbÀĞ¢6†F÷tFWF‚Ò2ÀĞ¢÷6—G’Ò6W'f–6W2åF†VÖU6W'f–6Rä—4F&²òã#‚¢ã ¢ÒÀĞ¢6†–ÆBÒö—FV×0Ğ¢Ó°Ğ¢6öçFVçBÒ÷7W&f6S°Ğ Ğ¢6÷W&6T–æ—F–Æ—¦VB³Ò…òÂò’ÓàĞ¢°Ğ¢f"†æFÆRÒæWrv–æF÷t–çFW&÷†VÇW"‡F†—2’ä†æFÆS°Ğ¢6WEv–æF÷tÆöær††æFÆRÂwvÄW…7G–ÆRÂvWEv–æF÷tÆöær††æFÆRÂwvÄW…7G–ÆR’Âw4W„æô7F—fFR“°Ğ¢Ó°Ğ¢ĞĞ Ğ¢V&Æ–2fö–BWFFU7VvvW7F–öç2„•&VDöæÇ”Æ—7CÅ6æ—WCâ6æ—WG2Âö–çB÷6—F–öâĞ¢°Ğ¢–b‡6æ—WG2ä6÷VçBÓÒĞ¢°Ğ¢†–FR‚“°Ğ¢&WGW&ã°Ğ¢ĞĞ Ğ¢÷7W&f6Rä&6¶w&÷VæBÒf–æD''W6‚‚%7W&f6T''W6‚"Â''W6†W2åv†—FR“°Ğ¢÷7W&f6Rä&÷&FW$''W6‚Òf–æD''W6‚‚$F—f–FW$''W6‚"Â''W6†W2äÆ–v‡Dw&’“°Ğ¢ö—FV×2ä6†–ÆG&Vâä6ÆV"‚“°Ğ¢f÷&V6‚‡f"6æ—WB–â6æ—WG2Ğ¢°Ğ¢f"&÷rÒæWrw&–B²Ö&v–âÒæWrF†–6¶æW72ƒ‚ÂbÂ‚Âb’Ó°Ğ¢&÷rä6öÇVÖäFVf–æ—F–öç2äFB†æWr6öÇVÖäFVf–æ—F–öâ²v–GF‚ÒæWrw&–DÆVæwF‚ƒR’Ò“°Ğ¢&÷rä6öÇVÖäFVf–æ—F–öç2äFB†æWr6öÇVÖäFVf–æ—F–öâ²v–GF‚ÒæWrw&–DÆVæwF‚ƒÂw&–EVæ—EG—Rå7F"’Ò“°Ğ¢&÷rä6†–ÆG&VâäFB†æWrFW‡D&Æö6°Ğ¢°Ğ¢FW‡BÒ6æ—WBåG&–vvW"ÀĞ¢föçDfÖ–Ç’ÒæWrföçDfÖ–Ç’‚$666F–ÖöæòÂ6öç6öÆ2"’ÀĞ¢föçEvV–v‡BÒföçEvV–v‡G2å6VÖ”&öÆBÀĞ¢f÷&Vw&÷VæBÒf–æD''W6‚‚$66VçD''W6‚"ÂæWr6öÆ–D6öÆ÷$''W6‚„6öÆ÷"äg&öÕ&v"ƒ‚Â#bÂ3’’’Ğ¢Ò“°Ğ¢f"æÖRÒæWrFW‡D&Æö6°Ğ¢°Ğ¢FW‡BÒ6æ—WBäæÖRÀĞ¢FW‡EG&–ÖÖ–ærÒFW‡EG&–ÖÖ–ærä6†&7FW$VÆÆ—6—2ÀĞ¢f÷&Vw&÷VæBÒf–æD''W6‚‚$×WFVD''W6‚"ÂæWr6öÆ–D6öÆ÷$''W6‚„6öÆ÷"äg&öÕ&v"ƒƒ’Â“’Â2’’Ğ¢Ó°Ğ¢w&–Bå6WD6öÇVÖâ†æÖRÂ“°Ğ¢&÷rä6†–ÆG&VâäFB†æÖR“°Ğ¢ö—FV×2ä6†–ÆG&VâäFB‡&÷r“°Ğ¢ĞĞ Ğ¢ÆVgBÒ÷6—F–öâåƒ°Ğ¢F÷Ò÷6—F–öâå“°Ğ¢–b‚—5f—6–&ÆRĞ¢°Ğ¢6†÷r‚“°Ğ¢ĞĞ¢ĞĞ Ğ¢&—fFR7FF–2''W6‚f–æD''W6‚‡7G&–ær¶W’Â''W6‚fÆÆ&6²’ÓàĞ¢7—7FVÒåv–æF÷w2äÆ–6F–öâä7W'&VçBåG'”f–æE&W6÷W&6R†¶W’’2''W6‚óòfÆÆ&6³°Ğ Ğ¢´FÆÄ–×÷'B‚'W6W#3"æFÆÂ"ÂVçG'•ö–çBÒ$vWEv–æF÷tÆöær"•ĞĞ¢&—fFR7FF–2W‡FW&â–çBvWEv–æF÷tÆöær„–çEG"v–æF÷rÂ–çB–æFW‚“°Ğ Ğ¢´FÆÄ–×÷'B‚'W6W#3"æFÆÂ"ÂVçG'•ö–çBÒ%6WEv–æF÷tÆöær"•ĞĞ¢&—fFR7FF–2W‡FW&â–çB6WEv–æF÷tÆöær„–çEG"v–æF÷rÂ–çB–æFW‚Â–çBfÇVR“°Ğ§ĞĞ