# Execução de testes

Uma entrada para os contratos e testes locais existentes, com logs por processo e resumo JSON/Markdown. Requer Windows e PowerShell 7.2 ou superior. Não instala pré-requisitos nem inicia o app normalmente.

O [mapa de responsabilidades do código e dos smokes](code-map.md) indica onde ficam a coordenação de `MainWindow`, os modos de linha de comando e os domínios da suíte. A separação da atividade 2 da #159 conserva a execução sequencial e os comandos abaixo.

## Perfis

| Perfil | Execução | Fora do perfil |
|---|---|---|
| Quick | 11 contratos/regressões de fonte: Actions/pins, idiomas, inventário, integridade, Captura/barra, fundação, workspace, release simulada, geração de avisos | Não compila nem prova comportamento WPF, pixels ou hardware |
| Full | Quick + restore/build Release x64, suíte de domínio real, GIF decodificado e 19 entradas de fixture do app: UI, dados/recuperação, nomes acessíveis, avisos, gravação fake e OCR offline | Não grava tela/áudio reais, instala hooks, atualiza o app ou substitui validação de pacotes/proveniência |
| Hardware | Com opt-in: três cenários de parada do gravador real em janela fictícia, sem áudio. Áudio do PC/mic somente com segundo opt-in | Não comprova falha real de kernel/driver nem dispensa ouvir os arquivos/observar liberação de dispositivos |

O Full usa os leitores, serviços, controles e renderers reais sobre fixtures; não converte esses testes em verificações textuais. O domínio tenta criar uma estação de clipboard privada; se Windows negar, não usa o clipboard pessoal e o resumo fica **INCOMPLETE** mesmo que a suíte antiga termine com exit 0. OCR usa corpus fictício e recursos locais; pode precisar do Visual C++ x64.

```powershell
# No PowerShell, a partir da raiz do repositório:
./scripts/test.ps1 -Profile Quick
./scripts/test.ps1 -Profile Full -OutputDirectory C:/SlashDeskTests

# Inspecionar o plano sem executar testes:
./scripts/test.ps1 -Profile Full -List

# Seleção explícita: aprovação significa apenas esse subconjunto.
./scripts/test.ps1 -Profile Quick -Task languages,ui-integrity

# Binários já compilados: caminhos exatos, sem selecionar o primeiro EXE encontrado.
./scripts/test.ps1 -Profile Full -SkipBuild `
  -AppExecutable ./src/SlashText/bin/x64/Release/net10.0-windows/SlashDesk.exe `
  -TestExecutable ./tests/SlashText.SmokeTests/bin/x64/Release/net10.0-windows/SlashText.SmokeTests.exe
```

`-DotnetPath` aceita um SDK explicitamente selecionado. Ele deve estar acessível aos apphosts; quando instalado em diretório próprio, configure `DOTNET_ROOT` e PATH **somente na sessão** antes de executar. A escolha/política permanente do SDK pertence à atividade 3; esta entrega não altera SDK, runtime, projeto ou recursos do produto. Não execute Full enquanto outro build/publish altera os mesmos bin/obj.

Antes de enviar switches de fixture, o runner exige FileVersion do app igual à do projeto atual. EXEs antigos/desconhecidos ficam UNAVAILABLE: um app que não conhece um switch poderia iniciar normalmente. A comparação de versão evita esse erro de uso; não autentica um binário adversarial. Use somente build próprio ou artefato cuja origem/hash já foi conferida.

Os smokes WPF apresentam janelas fictícias. Execute em desktop disponível/conta descartável quando pertinente; render e UIA não comprovam DPI físico, Narrador ou integração entre aplicativos reais. Full não equivale ao conjunto de todos os jobs de publicação e segurança.

Use uma pasta curta para Full: a fixture inerte de recuperação também passa pelo limite MP4 incluindo o GUID/sufixo temporário. Em checkout profundo, o preflight marca essa etapa UNAVAILABLE com orientação de encurtar `-OutputDirectory`, em vez de esperar um backend que o produto recusou antes de iniciar. Não se remove a validação nativa para fazer o teste passar.

## Hardware exige consentimento específico

```powershell
# Usar uma pasta curta, vazia/disponível para as evidências próprias do teste.
# Grava somente a janela fictícia, sem fontes de áudio:
./scripts/test.ps1 -Profile Hardware -AllowHardware `
  -AppExecutable '<caminho exato do SlashDesk.exe>' -OutputDirectory C:/SlashDeskTests

# Acrescenta gravação real de áudio do PC/microfone e um tom da fixture:
./scripts/test.ps1 -Profile Hardware -AllowHardware -IncludeAudio `
  -AppExecutable '<caminho exato do SlashDesk.exe>' -OutputDirectory C:/SlashDeskTests
```

Os caminhos entre `<...>` são exemplos a substituir. `-AllowHardware` autoriza os cenários sem áudio; `-IncludeAudio` acrescenta consentimento para fontes reais. Feche sons/conteúdo privados antes desse segundo comando. Arquivos são locais; não há upload pelo runner. Sem opt-in ou EXE explícito, nenhuma captura é iniciada e o resultado é INCOMPLETE/exit 2.

A verificação de áudio deixa REVIEW_REQUIRED: conferir os dispositivos ausentes em native.json, reproduzir as quatro combinações e registrar resultado/percepção de sincronismo e liberação de mic. Não editar um relatório automático para inventar o aceite humano; anexar registro separado com o hash do EXE. O preflight nativo de caminhos continua obrigatório; se a pasta/arquivo ultrapassar os limites aceitos, corrigir o local do teste, sem retirar o guard. Não usar este perfil para encerrar processos do app de uso ou testar dados reais.

## Evidências, falha e cancelamento

Cada invocação cria `test-results/<perfil>-<UTC>-<id>/`, ignorado pelo Git, ou uma pasta nova sob `-OutputDirectory`. Não sobrescreve evidências anteriores. Cada etapa possui stdout.log/stderr.log e suas próprias fixtures/resultados. summary.json contém SHA da fonte, árvore suja, perfil, seleção parcial, opt-ins, duração por etapa, status e hashes do executável/DLL adjacente quando houver; summary.md facilita a leitura. Para binário externo/SkipBuild, registrar também sua fonte: o SHA do checkout não autentica um EXE externo.

| Estado | Significado | Exit geral |
|---|---|---|
| PASS | Todas as etapas selecionadas passaram com evidência exigida | 0 |
| FAIL | Falha/assertion, saída inesperada, timeout ou evidência obrigatória ausente | 1 |
| UNAVAILABLE / REVIEW_REQUIRED / NOT_RUN | Pré-requisito/subteste indisponível, aceite físico pendente ou etapa não executada; geral INCOMPLETE se não houver falha/cancelamento | 2 |
| CANCELLED | Pedido de cancelamento; próximas etapas não iniciadas | 130 |

Use `-CancelFile <caminho de um marcador ainda inexistente>` para cancelamento observável. Para cancelar, crie esse arquivo por outro terminal: o runner encerra somente a árvore do subprocesso que iniciou, conserva logs e grava CANCELLED. Uma marca já existente impede iniciar. Remova a marca apenas quando quiser autorizar outra execução. Ctrl+C/encerramento forçado do terminal não substitui essa evidência; interrupções abruptas podem impedir o relatório final. Não repita o teste até ficar verde sem explicar falhas anteriores.

Scripts antigos são executados em novos processos PowerShell: `exit`, mudanças de diretório e códigos nativos não contaminam etapas seguintes. Logs são drenados assincronamente; timeout/cancelamento encerram apenas os filhos próprios. Nomes de etapas são únicos e sem travessia de diretório; toda evidência obrigatória precisa existir e estar não vazia.

## Como um agente registra um bug

1. Registrar issue, SHA/árvore suja, perfil/subconjunto, binário/hash/runtime quando houver, ambiente e comando literal sem conteúdo pessoal.
2. Guardar summary.json, stdout/stderr da etapa e reprodução mínima em fixture. Distinguir falha do produto, instrumentação, indisponibilidade e revisão física pendente.
3. Descrever esperado/observado e o primeiro resultado; aplicar correção mínima e repetir a etapa afetada, depois o conjunto necessário. Não reduzir tolerâncias/cobertura para esconder a causa.
4. Publicar somente síntese sanitizada: saídas locais podem conter caminhos e fixtures. Não anexar gravações ou dados do proprietário automaticamente.
5. Entregar testes/limites e próxima issue/modelo/esforço. Solicitar teste humano somente quando automação não cobrir a mudança.

## Mapa dos 13 workflows preservados

| Workflow | Responsabilidade / reaproveitamento |
|---|---|
| build.yml | CI normal e check obrigatório `build`; mantém testes reais, transportes e pacotes. Usa a entrada Quick para os cinco contratos antes separados e testa o próprio orquestrador. |
| capture-ocr-pilot.yml | Regressão WPF/OCR e EXE publicado; usa a mesma Quick para seis contratos e mantém o domínio e casos próprios. |
| about-pilot.yml | Piloto isolado de Sobre, WPF, persistência e publicação própria. |
| auxiliary-pilot.yml | Auxiliares: escolhas, foco e notificações em piloto próprio. |
| languages-pilot.yml | Catálogos e piloto PT/EN/ES; mantém isolamento/publicação. |
| quick-accent-pilot.yml | Controles e preferências do Acento em piloto próprio. |
| settings-pilot.yml | Configurações e transações de backup/emoji em piloto próprio. |
| statistics-pilot.yml | Métricas e persistência em piloto próprio. |
| candidate.yml | Pacotes normais Portable/Installed e testes dos EXEs exatos, inventário/SBOM/checksums. |
| security.yml | Gitleaks, NuGet, regressões de scanners e inventário/avisos dos artefatos. |
| codeql.yml | Extração/análise C#/Actions/Python. |
| tag-release.yml | Origem main, versão, aprovação e tag estável. |
| release.yml | Identidade da tag, build/pacotes, publisher protegido, emissão/verificação do atestado e publicação. |

Os seis pilotos de tela têm estrutura parecida mas escopos e propriedades MSBuild diferentes. Nesta entrega só foram deduplicadas chamadas com benefício direto; não foram apagados pilotos, etapas de pacote ou evidências. Workflows reutilizáveis não são necessários para essa consolidação. Release/tag/candidate/security/CodeQL e suas identidades/permissões permanecem nos arquivos atuais. Testes criptográficos reais e startup de pacote são feitos nos jobs próprios, não simulados como parte de Quick.

## Validar o orquestrador

```powershell
./scripts/test-runner-regression.ps1
```

Usa subprocessos inertes e verifica argumentos literais, sucesso/falha, falta de ferramenta/evidência, indisponibilidade embutida, timeout, cancelamento em andamento/antes de iniciar, retenção de logs, preservação de evidência, travessia de diretório e gates reais do perfil hardware. Não captura tela/áudio nem consulta o clipboard. Execução no CI é parte do check build; artefatos `SlashDesk-test-orchestration-evidence` e `SlashDesk-capture-shared-test-evidence` retêm os resultados.
