# Suporte e perguntas frequentes

## Pedir ajuda

Use [Issues](https://github.com/lucasllira/SlashText/issues) para dúvidas, bugs e melhorias. Procure um relato existente; para um novo problema, escolha o [formulário de bug ou melhoria](https://github.com/lucasllira/SlashText/issues/new/choose). Uma pergunta de uso pode ser aberta como issue em branco. O repositório não usa Discussions nem possui atendimento com prazo garantido.

Suspeitas de vulnerabilidade ou dados expostos seguem a [política de segurança e seu canal privado](SECURITY.md). Issues, comentários e anexos de suporte são públicos: compartilhe apenas exemplos fictícios e evidências revisadas.

Para agilizar um bug, informe versão em **Sobre**, origem do pacote (estável/RC/piloto/build próprio), Windows, passos, esperado/observado e frequência. Em problemas visuais, acrescente tema, idioma, escala e monitores; em gravação, modo Monitor/Região/Janela e fontes de áudio escolhidas, sem nomes/IDs pessoais de dispositivos.

## Qual pacote baixar?

A [última estável](https://github.com/lucasllira/SlashText/releases/latest) é o canal habitual. Pré-lançamentos são identificados na [página de releases](https://github.com/lucasllira/SlashText/releases) e ignorados pelo atualizador estável. Pilotos antigos usam versões/fixtures próprias; consulte o [índice histórico](docs/history.md) antes de reproduzir seus roteiros.

## Preciso instalar o .NET ou Visual Studio?

Os pacotes distribuídos são Windows x64 e incluem o .NET necessário. Visual Studio/SDK só fazem parte do desenvolvimento. O OCR nativo pode precisar do Visual C++ Redistributable x64; confira o diagnóstico do app e os [pré-requisitos e instruções oficiais ligados no guia](docs/development.md). Não apague dados para resolver falta de pré-requisito.

## Onde estão meus dados e como atualizo?

No pacote Portable normal, `SlashDeskData` fica ao lado do EXE. O modo Installed usa `%LocalAppData%\SlashDesk`; os pilotos usam dados separados conforme seu roteiro. O perfil Installed do repositório é staging de validação, não um instalador distribuído.

Use **Configurações → Dados locais → Abrir pasta** para conferir a localização e as opções de backup/restauração. Feche pela bandeja antes de uma substituição manual do portátil, preserve uma cópia dos dados e siga o [guia de armazenamento, atualização e rollback](docs/storage-and-updates.md). Não misture perfis reais com fixtures de teste.

## O app mostrou dados protegidos ou uma falha de restauração

Preserve os originais e backups. Registre a mensagem sanitizada e siga as ações oferecidas pelo app; consulte [recuperação de restauração](docs/restore-recovery.md). Não apague `settings.json`, `snippets.md` ou histórico para ocultar o aviso e não envie a pasta inteira de dados numa issue pública.

## Como conferir a origem do download?

Confira repositório, release e checksum do pacote escolhido. A partir da RC 3.3.0 há [proveniência do GitHub e verificador manual](docs/release-provenance.md). Isso não equivale a assinatura Authenticode/reputação SmartScreen, e o atualizador não verifica a assinatura de proveniência internamente. A estável 3.2.0 é anterior a esse fluxo; não apresente um artefato antigo como se tivesse atestado.

## Posso contribuir ou executar os testes?

Sim: consulte [CONTRIBUTING.md](CONTRIBUTING.md), [ambiente de build](docs/development.md) e [perfis de teste](docs/testing.md). A automação usa fixtures; testes de hardware/áudio exigem o consentimento descrito no guia. Para agentes, há [instruções específicas](AGENTS.md).
