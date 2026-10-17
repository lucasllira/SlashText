# Histórico de documentação e pilotos

Este índice identifica a versão e a finalidade dos registros preservados. Para uso e manutenção atuais, começar pelo [índice principal](README.md), [ambiente de build](development.md) e [runner de testes](testing.md).

**Histórico não significa descartável.** Contratos, inventários e roteiros ainda ajudam a reproduzir falhas e explicar compatibilidade. Um checklist antigo desmarcado não reabre uma issue concluída; uma aprovação antiga não valida os bytes atuais. Consultar o estado da issue/PR e os hashes do pacote antes de usar um roteiro.

## Versões e design anteriores

| Registro | Versão/base e aplicabilidade |
|---|---|
| [Design system anterior](design-system.md) | Guia 3.0, aplicado inicialmente na 2.8.1 e no redesign 3.0.0. Mantido para componentes legados e contratos; não é a autoridade única dos novos estilos `Lab.*` da 3.3.0. |
| [Inventário 2.9.1](ui-inventory-2.9.1.md) | Baseline anterior ao redesign, ligada ao manifesto ainda consumido por `scripts/compare-ui-inventory.ps1`. |
| [Inventário 3.0.0](ui-inventory-3.0.0.md) | Comparação e contagens da entrega 3.0.0; não inventário atualizado da main. |
| [Referência visual 3.0.0](redesign-3.0.0-reference.md) | Identifica os mockups aprovados em agosto de 2026. |
| [Notas 3.0.0](release-notes-3.0.0.md) | Registro da versão publicada, preservado sem reescrever seus destaques como estado atual. |
| [Notas 3.2.0](releases/3.2.0.md) | Base de atualização/rollback e, na consulta de 17/10/2026, última estável. A nota de reformulação futura é contextual àquela versão. |
| [Shell/Atalhos V2](design/shortcuts-v2/README.md) | Handoff e protótipo do trabalho anterior ao Visual Lab 3.3.0. [Especificação](design/shortcuts-v2/design-spec.md), [preservação funcional](design/shortcuts-v2/functional-inventory.md) e [inventário do piloto](design/shortcuts-v2/pilot-inventory.md) conservam sua base original. `CODEX-PROMPT.md`, citado na lista original do handoff, não faz parte deste checkout versionado. |
| [Inventário de confiabilidade](reliability-functional-inventory.md) | Base do PR #25; descreve fluxos e cobertura antes das alterações daquela entrega. |
| [Barra Fluent](capture-toolbar-fluent.md) | Escopo da barra transitória no piloto anterior à migração 3.3.0. |
| [Visual Contract da barra](capture-toolbar-visual-contract.md) | Contrato do preview anterior, ainda conferido por smoke. Não substituir seus tokens pelos da nova fundação silenciosamente. |
| [Integração da barra](capture-toolbar-visual-integration.md) | Registro do branch empilhado sobre o PR #29. A restrição de merge descrita no corpo pertence àquela entrega, não é orientação para branches atuais. |

## Referências da migração 3.3.0

| Registro | Como usar |
|---|---|
| [Fundação visual #62](design/3.3.0/README.md) | Regras e limitações da fundação inicial sobre a 3.2.0; suas instruções de seguir para #63 são históricas. |
| [Paridade da base 3.2.0](design/3.3.0/parity.md) e [inventário mecânico](design/3.3.0/ui-inventory.json) | Preservação funcional durante a migração; linhas e contagens pertencem ao commit registrado, não ao checkout atual. |
| [Contrato congelado](design/3.3.0/contract/README.md), [implementação de origem](design/3.3.0/contract/implementation.md) e [manifesto](design/3.3.0/contract-manifest.json) | Arquivos originais de referência, mantidos byte a byte. Não são o código WPF produtivo nem uma autorização de alterar os serviços. |
| [Feedback da Captura](design/3.3.0/capture-pilot-feedback.md) | Revisão do teste de 06/10/2026 na #63/PR #72; orientações de parada e pendências são daquele teste. |
| [Interfaces auxiliares](design/3.3.0/auxiliary-surfaces.md) | Inventário da entrega #69 e evidências versionadas daquele estado. |
| [Localização](design/3.3.0/localization.md) | Contrato técnico PT/EN/ES ainda aplicável à manutenção, embora localizado na pasta da migração. |

As imagens em `design/3.3.0/evidence/`, os protótipos e os manifestos permanecem nos caminhos originais. São evidências/referências, não screenshots garantidamente atuais de toda a aplicação.

## Roteiros dos pilotos

Os guias abaixo acompanham **pacotes e fixtures específicos** da preparação da 3.3.0. Vários EXEs têm versão interna 3.2.0 e dados em `SlashDeskPilotData`; um pacote normal Portable usa `SlashDeskData`, e Installed usa o perfil descrito no [guia de armazenamento](storage-and-updates.md). A RC pública não é intercambiável com esses pilotos. Não executar comandos destrutivos, testes de bloqueio ou cenários com dados reais por copiar um roteiro antigo.

| Roteiro | Entrega original |
|---|---|
| [Editor de Captura](capture-editor-pilot-testing.md) | #63: editor unificado, isolamento e regressão de captura. |
| [Complementos da Captura](capture-complements-pilot-testing.md) | #74: complementos e preferências do piloto. |
| [Captura + OCR](capture-ocr-pilot-testing.md) | Evolução do piloto de Captura após PR #75, com revisões de OCR/áudio. |
| [Atalhos](shortcuts-workspace-pilot-testing.md) | #64: workspace de Atalhos com Captura já integrada. |
| [Acento Rápido](quick-accent-pilot-testing.md) | #65: linhas compactas e estado do recurso. |
| [Estatísticas](statistics-pilot-testing.md) | #66: métricas e persistência. |
| [Configurações](settings-pilot-testing.md) | #67: preferências, importação e backup. |
| [Sobre](about-pilot-testing.md) | #68: identidade, links e atualização compartilhada. |
| [Interfaces auxiliares](auxiliary-pilot-testing.md) | #69: diálogos, menus, foco e notificações. |
| [Idiomas](languages-pilot-testing.md) | #82: interface PT/EN/ES aplicada ao reabrir. |
| [Retenção local](retention-pilot-testing.md) | #108: diagnósticos e temporários. |
| [Destinos MP4](recording-targets-pilot-testing.md) | #114: monitor, região e janela. |
| [Dados protegidos](protected-storage-pilot-testing.md) | #116: recuperação de preferências e histórico. |
| [Compatibilidade de backups](backup-compatibility-pilot-testing.md) | #117: recusa de dados incompatíveis antes de substituir o perfil. |
| [Recuperação de restauração](restore-recovery-pilot-testing.md) | #118: interrupção, retomada e preservação dos originais. |
| [Backups legados](legacy-backups-pilot-testing.md) | #119: versões antigas de atalhos preservadas. |
| [Falhas nas contagens](usage-writes-pilot-testing.md) | #126: escrita auxiliar de estatísticas e recuperação. |
| [Preferências da Captura](capture-preferences-pilot-testing.md) | #127: falha ao salvar e retomada das ações reais. |
| [Preparação da gravação](recording-operation-pilot-testing.md) | #128: reserva, concorrência e cancelamento. |

O corpo de cada guia foi preservado; somente o aviso de aplicabilidade foi acrescentado. Não marcar seus testes como executados novamente por indexá-los. Testes focados posteriores (GIF, MP4, atalhos, OCR, acessibilidade e caminhos longos) estão no [índice técnico atual](README.md#comportamentos-e-regressões-por-domínio).

## Aceites e retenção

Para a identidade exata de um candidato, aceites físicos e limites aceitos, consultar o **estado atual** da [#70](https://github.com/lucasllira/SlashText/issues/70). A RC1 tem proveniência própria; uma futura estável precisa de nova conferência e autorização. A [#37](https://github.com/lucasllira/SlashText/issues/37) depende da observação de uma release estável real por um consumidor já aberto, não de piloto/pré-lançamento.

O [plano de limpeza](repository-cleanup.md) preserva documentos, contratos, dados e evidências. Este índice não move ou exclui arquivos, não reduz retenção e não substitui a aprovação de uma limpeza posterior.
