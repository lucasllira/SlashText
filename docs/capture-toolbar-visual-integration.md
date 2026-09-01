# Integração da barra de captura aprovada

Este branch integra à captura real o Visual Contract aprovado no PR #29. Ele é
empilhado sobre `feat/capture-toolbar-visual-contract` e não deve ser integrado
diretamente à `main`.

## Alterações

- tema preto neutro e tokens do Visual Contract;
- ícones canônicos em grade 20 × 20;
- botões de 40 × 40 e botão dividido Capturar;
- Emoticons como ferramenta própria da barra;
- catálogo curado com 36 PNGs oficiais Google Noto Emoji;
- o mesmo asset é usado no seletor, no overlay e no bitmap final;
- o posicionamento monitor-aware do PR #28 permanece inalterado.

## Checklist manual no Windows

1. Faça uma seleção perto de cada um dos quatro cantos da área útil.
2. Confirme que barra e popovers permanecem totalmente visíveis.
3. Abra Capturar e teste padrão, Copiar e Salvar.
4. Teste cada ferramenta, inclusive propriedades de forma e contraste dos
   controles `Sem preenchimento` e `Contorno`.
5. Insira emojis de linhas diferentes do catálogo e altere o tamanho.
6. Confirme que o emoji visto no overlay é igual ao resultado copiado e salvo.
7. Teste Desfazer, Refazer, Apagar e Refazer seleção.
8. Repita em 100%, 125% e 150% de escala e, se disponível, em monitor secundário.

## Licença dos emojis

Origem: <https://github.com/googlefonts/noto-emoji>, arquivos `png/128`.
A licença e o aviso incluídos acompanham os assets em
`src/SlashText/Assets/NotoEmoji`.
