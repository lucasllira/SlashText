# SlashDesk — Shell e Atalhos V2

Pacote de handoff independente do Figma para implementar o piloto visual do SlashDesk em WPF.

## Arquivos

- `prototype.html`: referência visual interativa e mensurável.
- `styles.css`: tokens, medidas, estados e regras responsivas.
- `app.js`: dados demonstrativos e interações de busca, categoria, filtro, seleção e tema.
- `light-1440x900.png`: referência congelada do tema claro.
- `dark-1440x900.png`: referência congelada do tema escuro.
- `design-spec.md`: medidas, tokens e regras de implementação.
- `functional-inventory.md`: funções que não podem desaparecer.
- `CODEX-PROMPT.md`: prompt final para a tarefa de implementação.

## Como revisar

Abra `prototype.html` em um navegador. Use o botão de tema no cabeçalho para alternar entre claro e escuro. A busca, as categorias, o filtro de exibição e a seleção de atalhos são independentes e funcionais. Arraste os divisores verticais para redimensionar a lista, o editor e as variáveis; use duplo clique para restaurar a largura padrão.

Os dados visíveis são demonstrativos. A implementação WPF deve usar as coleções, bindings, handlers, comandos, serviços e persistência já existentes no aplicativo.

## Ordem de prioridade para implementação

1. Preservação funcional e inventário atual do repositório.
2. Estrutura e medidas registradas em `design-spec.md` e `styles.css`.
3. Capturas PNG como referência visual congelada.
4. Interações do protótipo como descrição dos estados.

O pacote não autoriza merge, tag ou Release. O ponto de parada é um PR em rascunho com build portátil de teste.

