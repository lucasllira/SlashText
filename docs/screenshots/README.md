# Imagens do aplicativo

Imagens da interface real da 3.3.0 em preparação, geradas em 17/10/2026 a partir da fonte `b3795237c377ae662662909e83cce5e5aa5c1153`, antes das alterações documentais desta entrega. Não são imagens da estável 3.2.0 nem prova de que a RC1 contém alterações posteriores da main.

Build local Release x64, SDK 10.0.402, runtime 10.0.13. EXE SHA-256: `43346f7fd3889e6e75126865de2aae044050637c8eec0ccda4ac369dc1898b4c`. Não é um novo pacote distribuído.

| Imagem | Origem | SHA-256 |
|---|---|---|
| [quick-accent-dark.png](quick-accent-dark.png) | `--quick-accent-smoke`, arquivo `accent-Dark-1440-100.png` | `818173f1f0add95e6fb354008372937b9e773a0f39616bc9da7bf9eb6c8a0597` |
| [capture-editor-light.png](capture-editor-light.png) | `--shortcuts-workspace-smoke`, arquivo `capture-Light-objects-True-100.png` | `c436adbd55f84da4db529d2b53e15fd1c75f34f076fe71d64893d0db53c74809` |

As fixtures usam controles WPF reais, dados/imagem fictícios e persistência isolada. Não instalam hooks, não capturam a área de trabalho pessoal, não gravam áudio e não acessam o clipboard pessoal. Textos, ícones e anotações foram conferidos visualmente; não há caminhos pessoais, documentos, credenciais ou dados de uso nas duas imagens selecionadas. Os PNGs foram copiados sem retoque dos resultados originais.

O estado “Monitor de atalhos indisponível” é esperado porque a fixture não ativa o monitor global. A imagem do editor demonstra anotações em uma imagem sintética, não uma captura real. Renderização a 100% em dimensões lógicas de 1440×900 (Acento) e 1440×1000 (editor); não comprova DPI misto físico.

Para reproduzir, compilar a fonte escolhida seguindo o [guia de build](../development.md) e executar os modos acima com pastas de saída novas conforme o [guia de testes](../testing.md). Registrar a identidade do novo EXE; não esperar igualdade binária de builds independentes. Evidências completas permanecem separadas das imagens públicas.
