# Trabalho no SlashDesk

SlashDesk é um aplicativo local Windows x64, C#/WPF em .NET 10. O repositório e o namespace continuam `SlashText`; o executável é `SlashDesk.exe`. Preserve serviços, formatos, defaults e compatibilidade de dados. Responda em português do Brasil, mantendo nomes técnicos/código no idioma existente.

## Localizar antes de editar

Confirme issue/escopo, branch, SHA e alterações locais; preserve trabalho alheio. Leia somente os guias do componente afetado, usando o [índice](docs/README.md). Registros no [histórico](docs/history.md) pertencem às suas bases/pacotes, não são pendências atuais por si só.

| Componente | Entrada / leitura necessária |
|---|---|
| Startup e janela principal | `src/SlashText/App.xaml.cs`, `src/SlashText/MainWindow.xaml`, `src/SlashText/MainWindow.xaml.cs` e os partials do domínio; [mapa de responsabilidades](docs/code-map.md) |
| Interface e temas | `src/SlashText/Views/`, `src/SlashText/Styles/VisualLab/`, [fundação visual](docs/design/3.3.0/README.md) e estilos reais do checkout |
| Regras e persistência | `src/SlashText/Services/`, `src/SlashText/Models/`; localizar o consumidor e o store real; [armazenamento](docs/storage-and-updates.md) |
| Idiomas | `src/SlashText/Localization/UiText.cs`, catálogos de `Localization/` e [contrato PT/EN/ES](docs/design/3.3.0/localization.md) |
| Testes | `tests/SlashText.SmokeTests/Program.cs` e os arquivos `SmokeCommandLine.cs`, `SmokeSuite*.cs` e `*Checks.cs` nesse diretório; [runner e limites](docs/testing.md) |
| Build/recursos | `src/SlashText/SlashText.csproj`, `global.json`, `scripts/`; [ambiente de desenvolvimento](docs/development.md) |
| CI/distribuição | `.github/workflows/` e [controles de release](docs/repository-release-controls.md); para pacotes, [candidato](docs/release-candidate.md) e [proveniência](docs/release-provenance.md) |

Use `rg` para seguir handlers/bindings em XAML e todos os partials antes de mudar um callback. Serviços executam as operações; a janela coordena a UI. Helpers em `Design/` também participam das fixtures do EXE. Mantenha a ordem dos smokes que compartilham `AppPaths`; não paralelize esses domínios. Não introduza outra arquitetura ou dependência sem benefício demonstrado no escopo.

## Implementar e verificar

1. Reproduza a issue com fixture mínima ou cópia isolada autorizada; registre esperado/observado e se a falha é do produto, ambiente ou instrumentação.
2. Explique brevemente causa, componentes e correção proposta. Para funcionalidade nova, apresente plano curto com fluxo de dados e critérios antes da implementação.
3. Faça a menor alteração coesa. Preserve foco, handlers, localização, descarte de recursos, transações e proteções existentes; siga `.editorconfig` sem reformatação em massa.
4. Execute os testes necessários ao impacto e conserve o primeiro resultado. Não troque comportamento real por testes de strings nem reduza assertions/guards para obter verde. Amplie testes quando houver falha ou risco ainda não coberto.
5. Entregue diff, SHA, comandos/resultados, limites e próxima issue/atividade com modelo e esforço. Solicite teste humano somente para cobertura necessária que a automação não comprovou.

Comandos na raiz, Windows x64 e PowerShell 7.2+. Use o SDK compatível com `global.json`; a política de servicing e recursos fica no [guia de build](docs/development.md). SDK privado usa `DOTNET_ROOT`/PATH somente na sessão. Não instale ferramentas nem altere o Windows silenciosamente.

```powershell
dotnet --version
pwsh -NoProfile -File ./scripts/test.ps1 -Profile Quick
pwsh -NoProfile -File ./scripts/test.ps1 -Profile Full -List
```

Quick verifica contratos de fonte; não prova execução WPF. Full compila e executa domínio/UI/OCR em fixtures; não substitui os jobs de segurança, pacotes/proveniência ou hardware. Antes de Full, confira pré-requisitos e o comprimento da pasta de evidência: use uma saída curta, nova e autorizada se o caminho afetar o preflight MP4. Não execute builds/publishes concorrentes nos mesmos `bin/obj`. Para subconjunto, EXEs exatos, cancelamento e evidências, siga [testing.md](docs/testing.md); um subconjunto aprovado vale somente para ele.

## Dados e recursos protegidos

- Use dados fictícios ou cópias autorizadas, mantendo originais. Não inicie normalmente o app como teste automatizado: `dotnet run`/EXE sem modo seguro pode acessar perfil real, migrar dados e instalar hooks. Confira origem/hash/versão do EXE antes de switches de fixture; não envie argumentos novos a um piloto antigo/desconhecido.
- Clipboard pessoal, hooks e tela/áudio reais não são fixtures. Se a estação privada de clipboard estiver indisponível, registre `UNAVAILABLE`; não use o clipboard pessoal como alternativa. Hardware exige autorização específica e opt-in `-AllowHardware`; PC/microfone exigem também `-IncludeAudio`, EXE exato e roteiro conforme o guia. Não grave conteúdo privado ou encerre processos de uso.
- Preserve `SlashDeskData`, `SlashDeskPilotData`, backups, capturas, gravações e evidências. Nada de limpeza recursiva genérica, `git clean -xfd` ou remoção de dados para resolver teste. O [plano de limpeza](docs/repository-cleanup.md) não autoriza exclusões.
- Mantenha `docs/design/3.3.0/contract/**` e seu manifesto byte a byte, assim como textos upstream e recursos com hashes revisados em `src/SlashText/Assets/`. Não altere hashes, marcadores de verificação ou licenças para contornar falhas de build.
- Arquivos sincronizados sob `sources/`, quando presentes no workspace do projeto, são referência somente leitura. Não edite/mova/exclua esse material. Não publique caminhos pessoais, textos copiados, imagens, áudio, credenciais ou logs brutos; revise e sanitize a evidência antes de anexar.

## Conclusão, aceites e custo

Registre issue/PR, fonte e árvore suja, SDK/runtime quando pertinentes, EXE/ZIP e hashes para testes de binário, perfil/subconjunto, comando, resumo/logs, duração e limites. Preserve evidências sem sobrescrevê-las. `UNAVAILABLE`, `REVIEW_REQUIRED`, `NOT_RUN` e cancelamento não viram PASS; registre o aceite humano separadamente. Para teste manual necessário, entregue pacote/hash/pasta, preparação, passos, resultado esperado e como coletar o resultado.

Modelo/esforço é recomendação operacional conforme disponibilidade e escolha do usuário: GPT-6.1 Sol / **low** para docs/templates e ajustes simples; **medium** para correções localizadas e manutenção; **high** para migração/preservação de dados, concorrência, segurança e recursos nativos quando a complexidade justificar. Explique o motivo, respeite o modelo designado na issue e não mude configurações automaticamente nem imponha Astra para todo trabalho. Nas respostas de entrega, informe sempre a próxima issue/atividade e o modelo/esforço sugerido.

Prossiga com implementação/revisões autorizadas sem pedir novamente permissões já concedidas. Antes de integrar na main, excluir dados, alterar controles do GitHub ou publicar tag/release, confira autorização específica existente; se faltar, peça aprovação sobre o resultado concreto e validado. Build verde e aprovação de PR não autorizam publicação. Mantenha pins/permissões e ambiente protegido; nenhuma integração por bypass administrativo.

Confira o estado atual de releases e da #70: número no projeto/nomes de pilotos não prova lançamento estável. Novo SHA/artefato exige identidade própria e regressão afetada; não apresente a RC1 como contendo mudanças posteriores. #37 depende da observação da release estável real por consumidor já aberto. Não feche issues de análise/release apenas por CI ou screenshots.

Formato e escopo do arquivo: [orientação oficial de AGENTS.md](https://learn.chatgpt.com/docs/agent-configuration/agents-md). Os guias ligados acima detalham as regras do produto; não é necessário carregá-los todos para uma alteração simples.
