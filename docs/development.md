# Ambiente de desenvolvimento e build

Execute os comandos na raiz do repositório, em Windows x64. Requer Git, PowerShell **7.2 ou superior** (`pwsh`), Python 3 para inventário/validações de segurança e o SDK definido em [`global.json`](../global.json). Não é necessário instalar Visual Studio para usar os comandos abaixo; um editor/IDE é opcional.

## SDK, runtime e servicing

- Mínimo: **SDK 10.0.402**, da faixa **10.0.4xx**, sem previews. Esse SDK corresponde ao runtime **10.0.13** já validado no CI. Um SDK anterior, como 10.0.401, deve ser recusado; instalar a versão compatível, sem retirar o `global.json` para contornar a restrição.
- `rollForward: latestPatch` seleciona o maior patch instalado da mesma faixa, a partir do mínimo. Não instala atualizações e não aceita automaticamente outra faixa, como 10.0.5xx, nem .NET 11. Execute a partir da raiz ou de uma subpasta do repo para que o resolver encontre o arquivo.
- Todos os workflows que instalam .NET usam `10.0.4xx` com qualidade `ga`: obtêm servicing estável da mesma faixa, em vez de congelar o mínimo para sempre. Não combinar esse canal com `global-json-file`, pois o setup instalado também passaria a obter a versão mínima separadamente. Mudar de faixa exige revisar `global.json`, os workflows, runtime publicado e avisos.
- O SDK seleciona ferramentas; não fixa sozinho o runtime embutido no pacote. Não adicionar `RuntimeFrameworkVersion` antigo, reaproveitar assets de restore de outro SDK ou reduzir os patches para satisfazer um teste. Os perfis Portable/Installed continuam self-contained e os inventários dos artefatos devem comprovar o runtime efetivamente distribuído.
- No fechamento desta atividade, o mínimo de distribuição é **10.0.13**, conferido pelo target `ValidateRuntimeServicing` do projeto a partir dos runtime packs resolvidos. Essa verificação não fixa o patch que será selecionado. Patches posteriores continuam sujeitos à revisão dos avisos de runtime em `src/SlashText/Assets/ThirdParty/catalog.json`; o gerador deve recusar uma versão ainda não revisada. Conferir a atualização e seus avisos, não afrouxar a proteção ou rebaixar o runtime.

A Microsoft documenta a [seleção por global.json](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json), o [canal da versão 10](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json) e os canais aceitos pelo [setup-dotnet revisado](https://github.com/actions/setup-dotnet/blob/67a3573c9a986a3f9c594539f4ab511d57bb3ce9/README.md). Verificar servicing na preparação de cada candidato e quando houver atualização de segurança. Alteração de SDK/runtime muda a identidade dos artefatos e requer nova validação da #70.

```powershell
dotnet --version
dotnet --info
pwsh --version
python --version
git --version
dotnet restore SlashText.sln -p:Platform=x64
dotnet build SlashText.sln -c Release --no-restore -p:Platform=x64
./scripts/runtime-servicing-regression.ps1
```

Instalação privada do SDK é permitida: configurar `DOTNET_ROOT` e o início de `PATH` **somente na sessão**, apontando para a mesma instalação. Isso também permite aos EXEs de testes encontrar o runtime. Não colocar caminhos do computador do proprietário em `global.json` nem alterar variáveis permanentes do Windows como parte do build.

Para testar o aplicativo normalmente, `dotnet run --project src/SlashText/SlashText.csproj -p:Platform=x64` ativa os recursos normais e usa a pasta de dados do modo instalado. Para validação automatizada sem dados pessoais, usar [os perfis de testes](testing.md), sem iniciar normalmente o app como substituto de uma fixture.

## Recursos offline e Visual C++

O build invoca `scripts/restore-noto-emoji.ps1`, `scripts/restore-ocr-models.ps1` e o gerador de avisos. A primeira restauração precisa de rede para os recursos ausentes e para NuGet. Com pacotes e recursos já obtidos/verificados, os recursos OCR/Noto são reutilizados localmente; isso não garante que auditorias de dependências ou outros passos de rede funcionem offline.

- **Noto:** o commit e os hashes de blobs estão no catálogo em `src/SlashText/Assets/NotoEmoji/catalog.json`. O script verifica os 3.972 PNGs, mantém `Full/` e gera `catalog-assets.zip` para embutir no app. Esses caches são ignorados pelo Git; não editar o catálogo, seus hashes ou o marcador `.verified` para fazer um build passar.
- **OCR:** os scripts baixam inglês/português nos conjuntos `best` e `fast`, a partir de commits fixados, conferindo SHA-256 antes de substituir o arquivo. Os quatro `.traineddata` também são ignorados pelo Git. Fonte, versão, licença e integridade continuam sendo conferidas no inventário do pacote.
- **Avisos:** o gerador usa arquivos upstream revisados e os pacotes/runtime realmente resolvidos, sem acessar a rede durante a geração. Versão desconhecida ou hash divergente deve impedir o pacote até revisão.
- **Visual C++ x64:** o OCR nativo pode precisar do Microsoft Visual C++ v14 Redistributable x64; o SDK .NET não o substitui. Consultar as [instruções oficiais da Microsoft](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist) e o diagnóstico do app. Um runner com esse componente instalado não prova o comportamento em Windows limpo; não instalar no computador principal silenciosamente para esconder uma indisponibilidade.

Não apagar caches, dados ou evidências para simular build limpo. O CI valida um checkout novo; uma reprodução local pode usar outro checkout/pasta de saída, mantendo a origem, SDK, comandos e hashes registrados. Não executar dois builds/publishes sobre os mesmos `bin/obj` ao mesmo tempo.

## Validar os dois formatos distribuídos

Use uma pasta nova de saída/evidência a cada execução. `Installed` é o layout de distribuição em diretório, **não um instalador**. O portátil deve conter apenas `SlashDesk.exe`; os dados do usuário não fazem parte do pacote.

```powershell
dotnet restore src/SlashText/SlashText.csproj --runtime win-x64 -p:Platform=x64
dotnet publish src/SlashText/SlashText.csproj -c Release -p:Platform=x64 -p:PublishProfile=Portable --no-restore --output test-results/package-check/portable
dotnet publish src/SlashText/SlashText.csproj -c Release -p:Platform=x64 -p:PublishProfile=Installed --no-restore --output test-results/package-check/installed
./scripts/candidate-package-smoke.ps1 -Executable test-results/package-check/portable/SlashDesk.exe -Layout Portable -OutputDirectory test-results/package-check/evidence-portable
./scripts/candidate-package-smoke.ps1 -Executable test-results/package-check/installed/SlashDesk.exe -Layout Installed -OutputDirectory test-results/package-check/evidence-installed
./scripts/security-inventory.ps1 -Executable test-results/package-check/portable/SlashDesk.exe -OutputDirectory test-results/package-check/inventory-portable
./scripts/security-inventory.ps1 -Executable test-results/package-check/installed/SlashDesk.exe -Layout directory -OutputDirectory test-results/package-check/inventory-installed
```

O workflow `Validate Release Candidate` executa os mesmos perfis normais em checkout limpo, testa o startup em conta descartável do CI, valida os dois EXEs, avisos offline, idiomas e seis cenários de OCR, gera inventário/SBOM e guarda ZIPs/checksums. No teste local, o startup normal é `UNAVAILABLE` para proteger o perfil pessoal; registrar essa limitação, sem configurar `GITHUB_ACTIONS=true` para burlá-la. Verificar nos inventários `frameworks` e nos avisos a versão distribuída, além de SDK, SHA da fonte e hashes dos EXEs/ZIPs.

A regressão de servicing executa o target real do projeto sobre metadados inertes de packs: mínimo, patch posterior, Core antigo, Desktop antigo e packs ausentes. Ela comprova o guard, não a compatibilidade de um runtime futuro; os testes dos pacotes e a revisão de avisos continuam necessários.

Leia [os critérios do candidato](release-candidate.md) antes de solicitar teste físico ou aceitar uma release. Gerar artefatos de validação não cria tag, atestado ou release pública, nem fecha a #70.

## Formatação e material protegido

`.editorconfig` segue espaços e CRLF já usados no checkout, com dois espaços para projetos/JSON/YAML e quatro para C#/XAML/scripts. Markdown preserva espaços finais significativos e não exige quebra de prosa. O contrato congelado `docs/design/3.3.0/contract/**` e material upstream em `src/SlashText/Assets/**` ficam fora das regras de formatação.

Não rodar formatação em massa, inclusive em ferramentas que ignorem EditorConfig. `.gitattributes`, os manifestos e os verificadores de hashes continuam sendo a autoridade para integridade desses arquivos. Compare o diff antes de enviar uma alteração e limite ajustes mecânicos aos arquivos realmente editados.
