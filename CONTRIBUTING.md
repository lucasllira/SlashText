# Contribuir com o SlashDesk

Correções, documentação e sugestões são bem-vindas. O aplicativo é Windows x64, C#/WPF em .NET 10; repositório e namespace mantêm o nome `SlashText`.

## Escolher o canal

- Dúvidas de uso: [suporte e perguntas frequentes](SUPPORT.md).
- Bug ou melhoria: procure um relato existente em [Issues](https://github.com/lucasllira/SlashText/issues) e acrescente informação útil; se não houver, use [o formulário correspondente](https://github.com/lucasllira/SlashText/issues/new/choose).
- Suspeita de vulnerabilidade ou exposição de dados: siga a [política de segurança](SECURITY.md) e o canal privado, sem detalhes em uma issue pública.

Mudança ampla de arquitetura, dependência ou comportamento deve ter proposta de escopo antes da implementação. Uma correção pequena pode ser apresentada diretamente em PR com reprodução e justificativa. Mantenha as conversas respeitosas e focadas no problema.

## Preparar a alteração

1. Leia o [ambiente de desenvolvimento](docs/development.md) e apenas os guias do [componente afetado](docs/README.md). Agentes seguem também [AGENTS.md](AGENTS.md).
2. Trabalhe em uma branch própria, preservando alterações locais e dados existentes. Reproduza com dados fictícios ou cópia isolada autorizada.
3. Faça uma alteração coesa e explique o comportamento antes/depois. Siga `.editorconfig`; preserve formatos/defaults, contratos visuais, recursos/licenças e compatibilidade de dados. Evite reformatação em massa e dependências sem benefício claro.
4. Acrescente ou ajuste testes quando o comportamento e o risco exigirem. Documentação simples não precisa de testes que só repitam seu texto.

Não anexe perfis pessoais, backups completos, clipboard, credenciais, caminhos de documentos ou gravações privadas. Revise imagens e logs antes de publicar. Arquivos de teste ficam fora de dados de uso; não limpe `SlashDeskData`/`SlashDeskPilotData` para fazer um teste passar.

## Validar conforme o impacto

O [guia de testes](docs/testing.md) define os perfis, pré-requisitos, seleção de etapas, evidências e cancelamento. Na raiz, com PowerShell 7.2+:

```powershell
pwsh -NoProfile -File ./scripts/test.ps1 -Profile Quick
pwsh -NoProfile -File ./scripts/test.ps1 -Profile Full -List
```

Quick valida contratos de fonte. Para regras/UI/dados, execute os cenários afetados e o Full quando necessário, em fixtures e numa pasta de evidência adequada. Pacotes, segurança e proveniência têm jobs próprios. Não execute builds/publishes concorrentes sobre os mesmos `bin/obj`.

Hardware, tela/áudio e clipboard pessoal exigem os limites/autorizações do guia; não são pré-requisito automático de toda contribuição. Registre falha, indisponibilidade e revisão física pendente como tais. Não reduza guards/assertions nem repita até obter verde sem explicar o primeiro resultado.

## Entregar uma PR

Use o [template de PR](.github/pull_request_template.md) como roteiro, removendo instruções de exemplo e campos sem aplicação. Inclua:

- problema, comportamento resultante, escopo e issue relacionada quando houver;
- comandos, resultado real, SHA/versão do binário quando pertinente e evidências sanitizadas;
- limites da validação, riscos de regressão e teste manual apenas se necessário, com passos e resultado esperado;
- documentação afetada e pendências que a PR não resolve.

Não marque uma verificação como aprovada por ela constar no template. A revisão e o CI devem corresponder ao commit final. A main exige PR/check obrigatório; a integração fica a cargo do mantenedor, sem bypass administrativo. Para uma atividade parcial de uma issue maior, prefira `Refs #...`; use encerramento automático apenas quando a issue inteira estiver atendida e seu encerramento fizer parte do aceite.

Publicar tag/release e mudar controles do repositório são decisões separadas da revisão de código. Consulte o [fluxo protegido de release](docs/repository-release-controls.md); os arquivos de política não aplicam configurações no GitHub.
