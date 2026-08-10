# SlashDesk — Inventário funcional obrigatório

Este arquivo é um checklist de preservação. Ele não substitui a inspeção do repositório: o Codex deve levantar os controles nomeados, handlers, bindings, comandos, estados e serviços reais antes de editar.

## Shell

- Title bar e controles da janela.
- Header da tela e estado de expansão.
- Tema Claro, Escuro e Seguir o Windows conforme suporte atual.
- Navegação horizontal na ordem: Atalhos, Acento Rápido, Captura, Estatísticas, Configurações e Sobre.
- Aba atual selecionada.
- Ações Importar e Novo atalho.
- Status local/versão existentes.

## Busca e organização

- Busca por nome, conteúdo e `/atalho`.
- Busca por categoria, se já suportada.
- Categorias persistidas.
- Categoria “Todos”.
- Contadores por categoria.
- Criação, edição ou organização de categorias existente.
- Filtro “Todos”.
- Filtro “Mais utilizados”.
- Lista completa, rolagem, total e estados vazios.
- Seleção preservada após filtro quando aplicável.

## Operações de snippet

- Criar, selecionar, editar, excluir e salvar.
- Importar de todas as fontes atualmente suportadas.
- Indicador de alterações não salvas.
- Validações e mensagens de erro.
- Todos os tipos de snippet existentes.
- Preview do resultado.
- Texto formatado.
- Cor, negrito, itálico e sublinhado existentes.
- Hyperlink.
- Campos Tab.
- Variáveis automáticas e preenchíveis.
- Aplicativos/escopo de expansão.

## Variáveis e dados

- Lista completa de variáveis e descrições.
- Inserção na posição atual do cursor.
- Tooltips/ajuda existentes.
- `snippets.md` sem mudança de formato.
- `SlashDeskData` sem mudança de localização ou estrutura.
- Persistência e backup existentes.
- Importadores, estatísticas, histórico e expansão global preservados.

## Fora de escopo

Não alterar Acento Rápido, Captura, GIF, MP4, editor de captura, Estatísticas, Configurações, Sobre, updater, rollback, onboarding, diálogos, overlays, tray ou estrutura dos dados.

## Inventário antes/depois exigido

Registrar em tabela:

| Tipo | Antes | Depois | Diferença justificada |
| --- | ---: | ---: | --- |
| Named controls |  |  |  |
| Event handlers |  |  |  |
| Bindings |  |  |  |
| Commands |  |  |  |
| Visual states |  |  |  |
| Serviços tocados |  |  |  |

Qualquer redução deve ser investigada antes de abrir o PR.
