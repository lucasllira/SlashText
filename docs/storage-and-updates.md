# Armazenamento e atualizações

## Modos de distribuição

O modo é gravado no assembly pela propriedade `SlashDeskDistribution` dos perfis
de publicação. `Portable.pubxml` usa `Portable`; `Installed.pubxml` usa
`Installed`. Não há inferência pelo nome ou pela localização da pasta.

| Dado | Nome atual | Portátil | Instalado |
|---|---|---|---|
| Atalhos, categorias e conteúdo | `snippets.md` | `SlashDeskData` | `%LocalAppData%\SlashDesk` |
| Preferências | `settings.json` | idem | idem |
| Estatísticas | `usage.json` | idem | idem |
| Histórico de mídia | `capture-history.json` | idem | idem |
| Imagens incorporadas aos atalhos | `assets/` | idem | idem |
| Backups | `Backups/` | idem | idem |
| Logs | `Logs/` | idem | idem |
| Estado e temporários de atualização | `update-state.json`, `Updates/` | idem | idem |

As capturas, GIFs e MP4 continuam no destino escolhido pelo usuário. O histórico
mantém o caminho existente e, quando a mídia está dentro da pasta portátil, também
um caminho relativo seguro. Um arquivo externo movido ou apagado é tratado como
indisponível sem invalidar os demais itens.

`AppDataEnvironment` resolve o modo e `AppPaths` é a fachada única consumida pelos
repositórios e serviços. O registro de inicialização com o Windows guarda apenas o
caminho do executável e não é uma origem de dados.

## Migração e backups

No portátil, uma origem adjacente válida sempre prevalece. Na ausência dela, a
origem legada de `%LocalAppData%\SlashDesk` é copiada para uma pasta de staging,
validada e movida para ativação somente após o sucesso. Antes da ativação é criado
um ZIP com manifesto. A origem antiga nunca é removida automaticamente.

Quando as duas origens têm dados, não ocorre mesclagem silenciosa: a adjacente é
usada e a origem concorrente recebe um backup preservado e um marcador da decisão.
JSONs isolados corrompidos não impedem a leitura dos demais itens do histórico.

Backups de dados incluem manifesto com versão do schema, data, versão do aplicativo
e modo de distribuição. A retenção é de sete cópias e nunca remove a única cópia
válida.

## Atualização portátil

1. `UpdateService` consulta `lucasllira/SlashText` via HTTPS, em background, com
   timeout, cache de seis horas e comparação SemVer.
2. Drafts e prereleases são ignoradas no canal estável. Uma versão ignorada só
   deixa de ser oferecida para aquele número; uma versão superior reaparece.
3. `PortableUpdateService` baixa o ZIP e o arquivo `.sha256` oficial para uma
   operação isolada em `SlashDeskData\Updates`.
4. O SHA-256, o nome do artefato, o conteúdo único do ZIP, a arquitetura PE x64 e
   a versão do executável são validados antes de encerrar o aplicativo.
5. Uma cópia temporária do próprio `SlashDesk.exe` aguarda o processo principal
   sair e usa `File.Replace` para trocar apenas o executável, mantendo uma cópia
   anterior para recuperação.
6. A nova versão é iniciada com um manifesto restrito a `SlashDeskData\Updates`.
   Ela confirma a versão esperada antes de abrir a janela principal.
7. Sem confirmação, o processo novo é encerrado, o executável anterior é restaurado
   atomicamente e reiniciado. Temporários de diagnóstico são preservados na falha.
8. Depois da confirmação, temporários e a cópia de recuperação da operação são
   removidos. A pasta `SlashDeskData` não é movida, compactada ou substituída.

Os logs da operação ficam em `SlashDeskData\Logs` no portátil e em
`%LocalAppData%\SlashDesk\Logs` no instalado. Nenhum log contém o texto dos
atalhos ou o conteúdo capturado.

## Edição instalada

O perfil `Installed` produz apenas staging self-contained para validação. Ainda
não existe instalador transacional no repositório; portanto a edição instalada
abre a Release oficial e não se apresenta como autoatualizável. Velopack não foi
adicionado porque seu layout portátil exige stub, `Update.exe`, `current/`,
`packages/` e marcador `.portable`, incompatíveis com a distribuição exigida de
um único `SlashDesk.exe`. A seleção de um instalador será feita quando o pacote
instalado entrar em escopo, sem compartilhar o mecanismo do portátil.

## Publicação após aprovação

O workflow `Release Windows` é acionado exclusivamente por tags `vX.Y.Z` (ou uma
tag SemVer com sufixo, que gera prerelease). Ele restaura, compila, executa os
smoke tests, publica os dois perfis, valida o portátil de arquivo único, calcula
SHA-256 e usa o `GITHUB_TOKEN` com apenas `contents: write`.

Depois de atualizar `<Version>`, `<AssemblyVersion>` e `<FileVersion>` e obter a
aprovação explícita para uma Release estável:

```powershell
git tag -s vX.Y.Z -m "SlashDesk X.Y.Z"
git push origin vX.Y.Z
```

Sem chave de assinatura configurada, use uma tag anotada (`git tag -a`) somente
após registrar essa limitação. Não execute esses comandos para validar o workflow;
uma tag publicada já aciona a criação da Release. Para ensaiar o atualizador, use
uma tag com sufixo, por exemplo `vX.Y.Z-rc.1`, após aprovação específica; ela será
marcada como prerelease e continuará ignorada pelo canal estável.
