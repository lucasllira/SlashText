# Inventário e plano de limpeza — #159, atividade 4

**Planejamento somente leitura. Zero arquivos existentes do repositório excluídos ou movidos; nenhuma exclusão autorizada.** A versão 3.3.0 ser a próxima estável não torna antigos contratos, testes ou dados descartáveis. Esta etapa entrega classificação, retenção e uma lista exata para revisão; não executa limpeza.

## Escopo e resultado

Snapshot da main `39f06daa94b0fabc38e90e7485f03db21600f93b`, em 17/10/2026. O [plano estruturado](repository-cleanup-plan.json) contém os 36 alvos exatos, medidas e referências. Os 520 arquivos versionados foram classificados: 410 ativos, 33 referências históricas e 77 contratos/recursos protegidos. **Recomendação: manter todos; nenhuma remoção versionada nesta entrega.** A organização dos documentos históricos fica para a atividade 5.

Foram registrados SHA-256 de **13.549 arquivos locais ignorados**, com **5.567.246.023 bytes lógicos** (aproximadamente 5,18 GiB). **128 caminhos protegidos de dados não foram percorridos nem tiveram conteúdo lido/hash calculado**; por isso o volume é um limite inferior, não o tamanho total das pastas. Não seguir links/junctions; nenhum foi encontrado nos alvos medidos. Não houve erros de leitura nos arquivos incluídos.

As saídas e caches de build somam **2.845 arquivos / 3.265.424.745 bytes** (aproximadamente 3,04 GiB) sujeitos a revisão depois das condições abaixo. Esse número **não é espaço aprovado nem garantia de espaço físico liberado**: inclui cópias, pode incluir hardlinks, e partes podem precisar ficar retidas. Evidências e caches de recursos permanecem fora dessa proposta de remoção.

Essa é principalmente uma organização **local**: as pastas ignoradas não estão publicadas como arquivos do GitHub. Apagá-las não reduz o histórico Git nem altera as releases. Para melhorar a navegação do repositório, seguir a atividade 5; não apagar história, tags ou releases.

## Lista por alvo

MiB medidos são bytes lógicos / 1.048.576. “Protegidos” inclui diretórios e arquivos de dados omitidos. “Revisar” não autoriza a exclusão da pasta, de arquivos novos ou dos dados que possam aparecer depois do snapshot.

| Alvo relativo ao repositório | Arquivos com hash | MiB conhecidos | Protegidos omitidos | Decisão |
|---|---:|---:|---:|---|
| `publish-about` | 1 | 134.41 | 0 | Revisar após retenção |
| `publish-accent` | 3 | 402.86 | 0 | Revisar após retenção |
| `publish-auxiliary` | 1 | 134.44 | 0 | Revisar após retenção |
| `publish-languages` | 1 | 135.85 | 0 | Revisar após retenção |
| `publish-ocr` | 1 | 134.25 | 0 | Revisar após retenção |
| `publish-security` | 1 | 135.85 | 0 | Revisar após retenção |
| `publish-security-installed` | 483 | 255.34 | 0 | Revisar após retenção |
| `publish-settings` | 3 | 403.15 | 0 | Revisar após retenção |
| `publish-statistics` | 1 | 134.33 | 0 | Revisar após retenção |
| `about-pilot-package` | 4 | 134.41 | 0 | Revisar após retenção |
| `auxiliary-pilot-package` | 5 | 134.44 | 0 | Revisar após retenção |
| `languages-pilot-package` | 4 | 135.85 | 0 | Revisar após retenção |
| `quick-accent-pilot-package` | 4 | 134.28 | 0 | Revisar após retenção |
| `recording-audio-pilot-package` | 6 | 134.28 | 0 | Revisar após retenção |
| `about-evidence` | 81 | 142.73 | 3 | Preservar |
| `auxiliary-evidence` | 699 | 177.90 | 12 | Preservar |
| `languages-evidence` | 1.477 | 237.61 | 19 | Preservar |
| `ocr-evidence` | 1.406 | 84.41 | 5 | Preservar |
| `quick-accent-evidence` | 837 | 617.06 | 20 | Preservar |
| `recording-audio-evidence` | 492 | 158.41 | 6 | Preservar |
| `security-evidence` | 7 | 17.50 | 0 | Preservar |
| `settings-evidence` | 1.240 | 501.18 | 53 | Preservar |
| `statistics-evidence` | 487 | 180.50 | 10 | Preservar |
| `src/SlashText/bin` | 498 | 346.61 | 0 | Revisar após retenção |
| `src/SlashText/obj` | 1.758 | 133.77 | 0 | Revisar após retenção |
| `tests/SlashText.SmokeTests/bin` | 20 | 88.65 | 0 | Revisar após retenção |
| `tests/SlashText.SmokeTests/obj` | 24 | 0.92 | 0 | Revisar após retenção |
| `tools/SlashDesk.ResourceInventory/bin` | 5 | 0.18 | 0 | Revisar após retenção |
| `tools/SlashDesk.ResourceInventory/obj` | 20 | 0.24 | 0 | Revisar após retenção |
| `scripts/security/__pycache__` | 2 | 0.04 | 0 | Revisar após retenção |
| `src/SlashText/Assets/NotoEmoji/Full` | 3.973 | 24.79 | 0 | Preservar |
| `src/SlashText/Assets/NotoEmoji/catalog-assets.zip` | 1 | 24.83 | 0 | Preservar |
| `src/SlashText/Assets/Ocr/eng.traineddata` | 1 | 14.69 | 0 | Preservar |
| `src/SlashText/Assets/Ocr/Fast/eng.traineddata` | 1 | 3.92 | 0 | Preservar |
| `src/SlashText/Assets/Ocr/por.traineddata` | 1 | 7.78 | 0 | Preservar |
| `src/SlashText/Assets/Ocr/Fast/por.traineddata` | 1 | 1.89 | 0 | Preservar |

Não incluir `.git/`, outros checkouts, SDKs/ferramentas privados, auditorias, Downloads, backups do proprietário, mídias e `sources/` sincronizados nessa lista. Pastas de teste futuras como `test-results/` não existiam neste snapshot e não estão autorizadas implicitamente. Preferências `*.user`/`*.suo` também não entram em limpeza genérica.

## Duplicados, unicidade e dados

Há **1.127 grupos de hashes repetidos** no escopo medido e **6.072 arquivos sem outra cópia idêntica nesse escopo**. Isso não verifica todos os discos ou o GitHub, e duplicação não comprova que um arquivo perdeu sua função. A lista privada associa cada arquivo candidato ao seu hash, tamanho, horário de alteração, número de cópias, hardlinks e eventuais cópias em áreas retidas.

Foram encontrados vários pilotos com ProductVersion `3.2.0+<commit>` apesar de terem sido usados nos testes da 3.3.0. Preservar seu commit/hash e os roteiros correspondentes; não identificá-los apenas pelo nome da pasta ou pelo número curto da versão.

`publish-security/SlashDesk.exe` e `publish-security-installed/SlashDesk.exe` não têm outra cópia de hash igual no inventário. **Reter antes de considerar remoção.** No Installed, preservar o conjunto completo de DLLs/recursos e inventário; um apphost sozinho não recompõe a distribuição. Os demais exemplares repetidos também exigem conferir se a cópia retida está íntegra e fora da futura lista de exclusão.

Áreas `SlashDeskData`, `SlashDeskPilotData`, backups, capturas/gravações, arquivos de preferências/uso/histórico e parciais `.recording.mp4` foram tratados como protegidos mesmo quando parecem fixtures. Dado ignorado pelo Git não significa dado descartável. As nove pastas de evidência ficam retidas, inclusive os 128 caminhos omitidos.

A triagem por nome/caminho não identifica todo possível conteúdo pessoal. Um arquivo sem origem confirmada continua exigindo revisão e retenção, mesmo que esteja numa pasta de build e conste como candidato para análise.

## O que permanece necessário ou histórico

| Item | Consumidor/justificativa | Decisão |
|---|---|---|
| `snippets.md` da raiz | O [projeto](../src/SlashText/SlashText.csproj) o embute como `SlashText.Defaults.snippets.md`; embalagem/testes também o usam. Não confundir com dados do usuário. | Manter |
| `scripts/ui-inventory-2.9.1.json` | [compare-ui-inventory.ps1](../scripts/compare-ui-inventory.ps1) carrega essa baseline pelo nome relativo a `$PSScriptRoot`. | Manter |
| `docs/design/3.3.0/contract/**` e `contract-manifest.json` | [visual-foundation-smoke.ps1](../scripts/visual-foundation-smoke.ps1) verifica os hashes congelados; estilos e inventário dependem do contrato. | Manter sem reformatar |
| Código, testes, scripts, projetos, estilos, PT/EN/ES, catálogo e licenças | Consumidos por build, testes e distribuição; não há substituição comprovada nesta análise. | Manter |
| Workflows e `.github/repository-controls/**` | Pilotos ainda são produtores de artefatos; políticas e release têm contratos próprios. | Manter |
| `docs/ui-inventory-3.0.0.md` | Ainda está ligado pelo README; descreve uma referência histórica. | Manter e indexar na atividade 5 |
| `docs/ui-inventory-2.9.1.md`, `docs/redesign-3.0.0-reference.md`, `docs/release-notes-3.0.0.md`, `docs/design/shortcuts-v2/**` | Registros de decisões/design e baselines. Não achar uma referência textual não prova inutilidade. | Marcar/indexar como histórico antes de cogitar movimentação |
| Notas da 3.2.0, roteiros de pilotos e evidências versionadas | Compatibilidade, suporte, aceites e reprodução das correções. O caminho das notas da 3.2.0 também é exercitado com fixture na regressão de release. | Manter |
| Noto `Full/`, ZIP e modelos OCR | Caches ignorados, restaurados por commit/hash conforme [ambiente de build](development.md). Preservam funcionamento offline e rastreabilidade. | Fora da limpeza básica |
| Auditoria/candidato privados e SDK 10.0.402 | Provas da #70, proveniência, Windows limpo, rollback e ferramentas para build. Estão fora do escopo relativo do repo. | Preservar; nenhuma limpeza externa inferida |

Foi feita busca em fonte, projetos, testes, workflows e documentação, incluindo variantes de caminho relativo e nome. O mapa privado separa referências textuais de consumidores confirmados. As receitas que produzem `publish-*` e `*-pilot-package` ainda existem nos workflows: a pasta gerada pode ser recriada, mas um piloto histórico exato exige sua origem e ambiente originais.

## Retenção antes de uma eventual exclusão

1. Preservar o candidato/release escolhido da 3.3.0 e a base de rollback: EXE/ZIP, checksums, atestado/bundle, inventário/SBOM/avisos, commit, SDK/runtime, run e resultados. Não substituir a RC1 por um binário novo sem nova identidade/validação da #70.
2. Para cada piloto histórico cuja saída for candidata, manter pelo menos um pacote completo verificável por hash, com commit, flags de build, roteiro, resultados e aceite. Conferir se o Git possui a origem e se dependências/recursos podem ser restaurados; não prometer reprodução byte a byte sem prova. Arquivo único ou origem não confirmada exige retenção, não descarte.
3. Intermediários de `bin/obj` e `__pycache__` podem ser reconstruídos, após preservar o candidato validado, as provas e o ambiente. Não é preciso guardar cada objeto compilado por tempo indefinido. Não limpar durante build/publish/teste.
4. Proposta de janela mínima: manter provas/candidato/rollback até concluir a #70 e por pelo menos **30 dias após a publicação estável**, sem automatizar exclusão. Incidente, suporte, rollback ou observação ainda pendente da #37 estende a retenção. A janela é uma regra de retenção, não uma autorização futura para apagar.
5. Artefatos do GitHub expiram conforme o workflow: candidato em 30 dias, inventário de segurança em 14 dias. Preservar localmente as provas necessárias antes dessa expiração; não assumir que o link continuará disponível. Nesta atividade nenhuma cópia existente foi movida ou apagada.

## Manifesto exato e condição de execução

O inventário completo e a lista de **2.845 arquivos candidatos para revisão** ficam na evidência privada da atividade, para não publicar caminhos pessoais nem metadados das fixtures. A lista é de arquivos exatos, não de diretórios para remoção recursiva. Identidades:

- Inventário completo: SHA-256 `0a384f2160f2c8d9060a2a84682ff97af579786740a414c5fe1866e6ad2bb788`.
- Lista exata/dry-run: SHA-256 `388f45224fd4a46ebe2fa8911d3ac76facbfda6cb37691b47c0bfbb076a4d738`.
- Todo alvo e arquivo possui `deleteApproved: false`; nenhum executor de exclusão foi criado.

A inspeção disponível não encontrou processo com caminho visível no repositório; alguns processos não expõem o caminho. Isso **não comprova ausência de locks, processos com argumentos relativos ou uso posterior**. O snapshot é insuficiente como autorização de remoção.

Uma limpeza posterior exige retenção concluída, lista exata atualizada/revisada e aprovação explícita do proprietário. Antes de qualquer operação: conferir raiz absoluta e cada componente, recusar travessia/reparse points, arquivos versionados/protegidos/desconhecidos, comparar novamente hash/horário e processos/locks, e abortar em divergência. Nunca converter os alvos desta tabela em um comando de exclusão recursiva, nem usar `git clean -xfd`.

## Verificação desta entrega

Os testes inertes do inventário recusaram caminhos absolutos, `..`, separadores ambíguos, glob e caminhos de dados protegidos. Um teste com **junction real do Windows**, criado apenas em fixture própria do workspace, confirmou recusa de travessia. Symlink simbólico separado não foi criado por falta de privilégio; não foi tratado como teste aprovado. Código, SDK/runtime, recursos, workflows e pacotes permanecem iguais à base; esta entrega adiciona somente documentação/plano e seu link.

Atividade 4 é planejamento; sua conclusão não depende de apagar arquivos. Próxima: **atividade 5 — índice e identificação da documentação atual/histórica**, GPT-6.1 Sol / medium. Nenhum teste manual do app é necessário para revisar este plano; eventual exclusão terá aceite separado.
