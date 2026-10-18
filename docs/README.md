# Documentação do SlashDesk

Comece pela finalidade da tarefa. Os guias de manutenção descrevem a implementação do checkout; os registros históricos conservam as decisões, versões e roteiros da entrega original.

## Uso e versões

| Preciso de… | Ler |
|---|---|
| Download e primeiros passos | [README do produto](../README.md) e [última versão estável](https://github.com/lucasllira/SlashText/releases/latest) |
| Recursos detalhados, variáveis e limitações | [Guia de uso](user-guide.md) |
| Próximas entregas e ideias futuras | [Roadmap](roadmap.md) e [organização de issues/marcos](project-navigation.md) |
| Pedir ajuda ou consultar perguntas frequentes | [Suporte](../SUPPORT.md) |
| Relatar uma vulnerabilidade em privado | [Política e canal de segurança](../SECURITY.md) |
| Entender a versão estável atual | [Notas da 3.3.0](releases/3.3.0.md) e [validação/publicação #70](https://github.com/lucasllira/SlashText/issues/70) |
| Consultar a versão anterior e compatibilidade | [Notas da 3.2.0](releases/3.2.0.md), [armazenamento e atualização](storage-and-updates.md) e [compatibilidade de backups](backup-compatibility.md) |
| Saber onde ficam os dados e como atualizar | [Armazenamento, migração, backup e rollback](storage-and-updates.md) |
| Entender privacidade e recuperação | [Clipboard](clipboard-privacy.md), [diagnósticos e temporários](local-data-retention.md), [histórico de capturas](capture-history-security.md) e [recuperação de restauração](restore-recovery.md) |
| Consultar avisos e licenças | [Manutenção/consulta dos avisos offline](third-party-notices.md), [avisos de terceiros](../THIRD-PARTY-NOTICES.md) e [licença do projeto](../LICENSE) |

**Situação conferida em 17/10/2026:** a estável é [**v3.3.0**](https://github.com/lucasllira/SlashText/releases/tag/v3.3.0), produzida no commit `834d7878ef02e2fc90572bd17df6aee898c1d3fd`, com hashes e proveniência do pacote público verificados. A RC1 anterior permanece como histórico, ignorada pelo atualizador estável. Mudanças posteriores da main não alteram os bytes da release. A observação da consulta periódica real continua na #37; publicar não aprova esse critério automaticamente. Conferir releases e a #70 antes de escolher um pacote; não substituir dados reais por fixtures dos pilotos.

## Desenvolvimento e testes

| Assunto | Guia atual |
|---|---|
| Pré-requisitos, SDK/runtime e recursos Noto/OCR | [Ambiente de desenvolvimento e build](development.md) |
| Pontos de entrada e responsabilidades | [Mapa do código e dos smokes](code-map.md) |
| Trabalho por agentes, evidências e aceites | [Instruções do repositório](../AGENTS.md) |
| Contribuir com correção, documentação ou proposta | [Guia de contribuição](../CONTRIBUTING.md) |
| Execução automatizada e registro de bugs | [Perfis Quick, Full e Hardware](testing.md) |
| Localização PT/EN/ES | [Contrato de localização](design/3.3.0/localization.md) |
| Aparência e preservação funcional da 3.3.0 | [Fundação visual](design/3.3.0/README.md), [matriz de paridade](design/3.3.0/parity.md) e [inventário da base](design/3.3.0/ui-inventory.json) |
| Recursos visuais congelados | [Contrato original](design/3.3.0/contract/README.md) e [manifesto de hashes](design/3.3.0/contract-manifest.json) |
| Limpeza local e retenção | [Inventário e plano](repository-cleanup.md) e [classificação estruturada](repository-cleanup-plan.json) |

A fundação e a matriz registram a base da migração; não são um estado de aceite de todas as telas atuais. Para manutenção, conferir os controles, serviços e estilos reais do checkout usando o mapa do código. O contrato congelado conserva os bytes originais, inclusive seus exemplos e textos; não o reformatar para atualizar documentação.

### Comportamentos e regressões por domínio

| Domínio | Referências técnicas |
|---|---|
| Atalhos | [Validação em lote](snippet-batch-validation.md) e [lista virtualizada](shortcut-list-validation.md) |
| Acessibilidade | [Nomes acessíveis nos controles reais](accessibility-names.md) |
| Arquivos externos e emojis | [Limites de processamento](external-input-limits.md) e [importação em caminhos longos](custom-emoji-long-path-testing.md) |
| GIF | [Duração sob carga](gif-timeline-validation.md) e [cancelamento/recursos](gif-cancellation-validation.md) |
| MP4 | [ScreenRecorderLib 7.0.1 e áudio](screenrecorder-701-testing.md), [parada e recuperação](recording-stop-recovery.md) e [limites de caminhos](recording-output-paths.md) |
| Dados e restauração | [Compatibilidade do backup](backup-compatibility.md), [recuperação](restore-recovery.md) e [acesso a arquivos do histórico](capture-history-security.md) |
| Privacidade | [Preservação do clipboard](clipboard-privacy.md) e [retenção local](local-data-retention.md) |

Esses documentos explicam contratos e como reproduzir testes focados. Resultados de um commit ou EXE não aprovam automaticamente outro. Para executar hoje, começar pelo [runner atual](testing.md); consultar os [roteiros históricos](history.md) somente com o pacote e as fixtures correspondentes. Hardware/áudio exigem consentimento próprio; teste indisponível não vira aprovação.

## Distribuição, segurança e manutenção

| Assunto | Guia atual |
|---|---|
| Pacotes Portable/Installed, atualização e rollback | [Armazenamento e atualizações](storage-and-updates.md) |
| Preparação e critérios do candidato final | [Validação do candidato](release-candidate.md) |
| Tag, main e aprovação de publicação | [Controles do repositório e release](repository-release-controls.md) |
| Origem assinada dos artefatos e verificador manual | [Proveniência e limites de confiança](release-provenance.md) |
| Pins, allowlist e atualizações de Actions | [Manutenção das Actions](github-actions-maintenance.md) |
| Scanners, dependências e inventário/SBOM | [Baseline contínua de segurança](security-baseline.md) |
| Bibliotecas nativas de OCR e risco residual | [Confiança e revisão do OCR](ocr-native-trust.md) |
| Avisos e licenças incorporados ao pacote | [Avisos offline](third-party-notices.md) |

Alterar arquivos de política não altera as configurações do GitHub. Build verde não autoriza merge, tag ou publicação. O atualizador não verifica a assinatura de proveniência internamente; Installed é staging de validação, não um instalador distribuído. Os guias acima detalham esses limites.

## Histórico preservado

O [índice histórico](history.md) reúne o design anterior à 3.3.0, inventários/baselines, decisões da migração e os 19 roteiros de pilotos. Mantém caminhos e documentos de versões anteriores para suporte, regressão e rastreabilidade. Datas, checklists e frases como “próxima etapa” nesses registros pertencem à entrega original; o andamento atual fica na issue correspondente.

O índice organiza a leitura; não autoriza apagar ou mover arquivos, reabrir análises concluídas ou repetir toda a validação manual. Próximas etapas da organização ficam na [#159](https://github.com/lucasllira/SlashText/issues/159).
