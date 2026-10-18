# Roadmap do SlashDesk

Navegação: [produto](../README.md) · [documentação](README.md) · [contribuir](../CONTRIBUTING.md).

Este documento separa compromissos do lançamento de ideias para avaliação. Não fixa datas nem transforma uma sugestão em funcionalidade prometida. O estado de execução fica nas issues ligadas abaixo.

## Publicação da 3.3.0 e acompanhamento

Situação conferida em 17/10/2026: [**3.3.0 estável publicada**](https://github.com/lucasllira/SlashText/releases/tag/v3.3.0), fonte `834d7878ef02e2fc90572bd17df6aee898c1d3fd`. A RC1 anterior permanece como histórico. O aceite de critérios pendentes continua nas respectivas issues.

| Frente | Registro e próximo resultado |
|---|---|
| Organização do repositório | [#159](https://github.com/lucasllira/SlashText/issues/159): concluída e aceita; oito entregas integradas e evidências transferidas à #70. Limpeza e labels/marcos continuam somente propostas |
| Candidato integrado e publicação | [#70](https://github.com/lucasllira/SlashText/issues/70): candidato, tag/build e publicação tiveram aceites próprios; pacote público e proveniência verificados. Consolidar o fechamento sem aprovar critérios pendentes de outras issues |
| Aceite da migração visual | [#61](https://github.com/lucasllira/SlashText/issues/61): consolidar o aceite conforme seus critérios, sem reabrir automaticamente análises ou correções concluídas |
| Observação da atualização real | [#37](https://github.com/lucasllira/SlashText/issues/37): registrar o aviso numa consulta posterior à abertura, respeitando cache/polling reais. A observação original foi interrompida; a fixture de reposição e seus limites estão identificados na issue |

O escopo funcional publicado está nas [notas da 3.3.0](releases/3.3.0.md). As análises A–F e correções integradas permanecem rastreáveis no GitHub; não são uma lista de trabalho futuro. Os [critérios do candidato](release-candidate.md) conservam os limites da validação e os aceites humanos.

## Ideias após o lançamento

Sugestões já levantadas pelo proprietário, ainda sem desenho aprovado, estimativa ou versão definida:

| Ideia | Avaliar antes de implementar |
|---|---|
| Histórico por atalho | Utilidade, retenção, privacidade e custo de armazenamento; distinguir do contador de uso existente |
| Projetos editáveis para capturas | Formato de projeto, compatibilidade e preservação das imagens/anotações ao reabrir |
| Edição temporal de gravações | Escopo de cortes, prévia, custo de processamento e dependências nativas |

Essas ideias não bloqueiam a 3.3.0 e não foram implementadas na #159. Para iniciar uma delas, abrir proposta com caso de uso, escopo mínimo, impacto nos dados e critérios de teste; usar [o formulário de melhoria](https://github.com/lucasllira/SlashText/issues/new/choose) após procurar duplicatas.

Bugs encontrados em uso continuam sendo triados pela gravidade e reprodução, independentemente deste roadmap. Vulnerabilidades seguem o [canal privado](../SECURITY.md). Não atribuir data ou marco a uma ideia sem decisão explícita do mantenedor.
