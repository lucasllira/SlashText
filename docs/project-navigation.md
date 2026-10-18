# Organizar issues e entregas

Navegação: [roadmap](roadmap.md) · [contribuição](../CONTRIBUTING.md) · [índice](README.md).

Proposta da atividade 8 da [#159](https://github.com/lucasllira/SlashText/issues/159). Em 17/10/2026, o repositório já possui `bug`, `enhancement`, `documentation` e `dependencies`, além das labels padrão e `.NET`. Não há milestone aberto. Esta entrega documenta a convenção; não cria/renomeia labels ou marcos, não altera atribuições e não recategoriza o histórico.

## Labels por finalidade

| Label | Quando usar | Situação |
|---|---|---|
| `bug` | Comportamento observado diferente do esperado, com reprodução | Existente; reutilizar |
| `enhancement` | Proposta de recurso ou melhoria de comportamento | Existente; reutilizar |
| `documentation` | Guias, exemplos, suporte e apresentação | Existente; reutilizar |
| `dependencies` | Atualização de bibliotecas/ferramentas | Existente; preservar uso pelo Dependabot |
| `maintenance` | Organização interna, testes e build sem novo recurso | Proposta, ainda não criada |
| `release` | Validação integrada, distribuição e publicação | Proposta, ainda não criada |

Usar a finalidade principal e acrescentar outra label somente quando ajudar a busca. Labels não substituem prioridade justificada, modelo/esforço, critérios de aceite ou autorização de publicação. Manter `.NET` e as labels padrão existentes; não acrescentar várias categorias de tela, modelo ou prioridade sem necessidade.

Os formulários de bug/melhoria e o template de PR já pedem contexto e validação. Não dependem das labels propostas para funcionar. Dúvidas seguem [suporte](../SUPPORT.md); segurança segue [relato privado](../SECURITY.md).

## Marcos de versão

- Propor um único marco **v3.3.0**, sem data artificial, para o escopo aceito do lançamento. A data deve ser definida a partir dos gates reais.
- Se autorizado posteriormente, conferir as issues ainda abertas antes de relacioná-las ao marco. Após a publicação estável, #70, #61 e #37 continuam em acompanhamento; a #159 foi aceita e encerrada. #70 pode usar `release`; não remover labels atuais automaticamente.
- A #37 depende da observação após a publicação estável. O marco pode continuar aberto durante essa observação; não marcar tudo como concluído apenas porque a release foi publicada.
- Ideias do [roadmap](roadmap.md) ficam sem marco até triagem. Não criar um marco genérico “futuro” que pareça compromisso de entrega.
- Não preencher retroativamente todas as issues/PRs fechadas. Preservar relações e histórico existentes; o marco organiza trabalho aceito, não reescreve releases anteriores.

## Fluxo de uma entrega

1. Issue com objetivo, reprodução ou caso de uso, escopo e critérios verificáveis.
2. Correção/proposta em branch e PR que referencia a issue. Em entrega parcial, usar referência sem encerrar a issue principal.
3. Testes proporcionais e evidência com SHA; validação humana somente quando a automação não comprovar o critério necessário.
4. Registrar aceite e integração autorizada. Fechar somente critérios cumpridos; candidato/tag/release seguem os controles próprios.

Para uma análise, o relatório e seu aceite podem encerrar a análise com correções relacionadas ainda abertas, desde que cada achado permaneça rastreável. Não confundir aceitação do diagnóstico com resolução de todos os achados.
