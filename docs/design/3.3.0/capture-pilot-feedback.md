# Captura 3.3.0 — revisão do teste de 06/10/2026

Escopo: issue #63 / PR #72. Não encerrar a issue ou avançar para outra tela antes da validação do candidato e do overlay nativo previsto na issue.

## Três divergências reportadas

| Item | Causa | Correção |
| --- | --- | --- |
| Novo | Paleta inicial diferente das imagens atuais do Lab; TextBlock interno recebia a cor implícita de texto em vez da cor do botão. | Token opt-in `Lab.capture-primary`: Claro `#337c8f`, Preto `#74d1e5`, extraídos do preenchimento nas imagens enviadas. Texto e ícone usam o Foreground do botão (branco no claro, escuro no preto). Windows acompanha a paleta do sistema. |
| Zoom | LayoutTransform externo era compensado pelo ajuste automático do Viewbox. | Viewport de altura estável, com rolagem; dimensões explícitas de prévia = ajuste ao viewport × 75/100/125%. Coordenadas de anotação e bitmap original permanecem independentes do zoom. |
| Copiar dos Recentes | Clipboard recebia somente FileDrop, que não permite colar a captura como imagem em vários destinos. | Payload com imagem decodificada e FileDrop original; carregamento OnLoad libera o arquivo. PNG/JPEG/GIF podem ser colados como imagem; GIF mantém também o arquivo animado e MP4 mantém cópia de arquivo. |

O contrato congelado em `contract/` permanece inalterado. A nova cor é uma exceção documentada e limitada ao botão Novo, sem repintar todos os controles ou telas. O site não pôde ser lido nesta revisão; as imagens de comparação enviadas pelo usuário são a referência dessa correção de cor.

## Verificações automáticas

- Smoke estrutural: vínculo explícito de cor no Novo, tokens por tema, chamada do zoom interno e ausência de LayoutTransform externo.
- Smoke WPF em STA: dimensões da prévia nos três níveis de zoom e tamanho/pixels da exportação inalterados.
- Payload do clipboard: PNG, JPEG e GIF oferecem imagem e arquivo; MP4 oferece arquivo; arquivo ausente falha sem alterar o clipboard; arquivo de origem não fica bloqueado.
- Build Windows e smokes existentes continuam obrigatórios. O smoke do payload não escreve no clipboard real do runner; colar em aplicativos externos exige teste manual.

## Reteste manual do candidato

1. Comparar Novo com as imagens do Lab em Claro e Preto; alternar Windows e verificar contraste de texto e ícone. Repetir em Imagem, Vídeo e GIF.
2. Abrir uma imagem e alternar 75%, 100%, 125%: o tamanho visível deve mudar, com rolagem quando necessário, sem redimensionar o editor inteiro. Desenhar e exportar; tamanho original e posicionamento devem permanecer corretos. Repetir com imagem alta de Captura longa e ao redimensionar a janela.
3. Clicar Copiar em uma captura PNG/JPEG dos Recentes e colar no Paint ou outro editor. Copiar GIF: testar a imagem estática num editor e o arquivo animado no Explorer. Copiar MP4: colar como arquivo no Explorer.
4. Confirmar que selecionar o cartão e abrir/editar/excluir continuam funcionando, assim como o carrossel, os atalhos, as regras e as animações com redução de movimentos.

Não substituir dados existentes por dados de exemplo nem publicar release 3.3.0 com este candidato. A aprovação da tela não encerra automaticamente o restante da #63.
