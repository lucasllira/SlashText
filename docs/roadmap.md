# Roadmap do SlashDesk

Navegação: [produto](../README.md) · [documentação](README.md) · [contribuir](../CONTRIBUTING.md).

Este documento separa compromissos do lançamento de ideias para avaliação. Não fixa datas nem transforma uma sugestão em funcionalidade prometida. O estado de execução fica nas issues ligadas abaixo.

## Preparar a 3.3.0

Situação consultada em 17/10/2026: estável **3.2.0**, pré-lançamento **3.3.0-rc.1**. A RC1 pertence ao commit `138e6bbdacf494d2d748e6db68b53c1d46008bc5`; não contém a organização posterior do repositório.

| Frente | Registro e próximo resultado |
|---|---|
| Organização do repositório | [#159](https://github.com/lucasllira/SlashText/issues/159): concluir as atividades, conferir evidências e transferir o resultado à validação do candidato |
| Candidato integrado e publicação | [#70](https://github.com/lucasllira/SlashText/issues/70): gerar o candidato da fonte integrada, validar o impacto das alterações e registrar identidade dos pacotes; tag/publicação estáveis dependem de autorização própria |
| Aceite da migração visual | [#61](https://github.com/lucasllira/SlashText/issues/61): consolidar o aceite conforme seus critérios, sem reabrir automaticamente análises ou correções concluídas |
| Observação da atualização real | [#37](https://github.com/lucasllira/SlashText/issues/37): preparar consumidores já abertos e observar a oferta após a publicação estável, respeitando cache/polling reais |

O escopo funcional preparado está nas [notas da 3.3.0](releases/3.3.0.md). As análises A–F e correções integradas permanecem rastreáveis no GitHub; não são uma lista de trabalho futuro. Os [critérios do candidato](release-candidate.md) conservam os limites da validação e os aceites humanos.

## Ideias após o lançamento

Sugestões já levantadas pelo proprietário, ainda sem desenho aprovado, estimativa ou versão definida:

| Ideia | Avaliar antes de implementar |
|---|---|
| Histórico por atalho | Utilidade, retenção, privacidade e custo de armazenamento; distinguir do contador de uso existente |
| Projetos editáveis para capturas | Formato de projeto, compatibilidade e preservação das imagens/anotações ao reabrir |
| Edição temporal de gravações | Escopo de cortes, prévia, custo de processamento e dependências nativas |

Essas ideias não bloqueiam a 3.3.0 e não foram implementadas na #159. Para iniciar uma delas, abrir proposta com caso de uso, escopo mínimo, impacto nos dados e critérios de teste; usar [o formulário de melhoria](https://github.com/lucasllira/SlashText/issues/new/choose) após procurar duplicatas.

Bugs encontrados em uso continuam sendo triados pela gravidade e reprodução, independentemente deste roadmap. Vulnerabilidades seguem o [canal privado](../SECURITY.md). Não atribuir data ou marco a uma ideia sem decisão explícita do mantenedor.
