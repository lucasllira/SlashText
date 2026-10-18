# Política de segurança

## Relatar em privado

Para suspeita de vulnerabilidade, exposição de dados ou credenciais, use [Reportar uma vulnerabilidade](https://github.com/lucasllira/SlashText/security/advisories/new), em **Security → Advisories → Report a vulnerability**. O relato privado é separado das issues públicas e depende de o recurso estar habilitado no repositório.

Se o botão estiver indisponível, abra uma issue dizendo apenas **“Preciso de um canal privado de segurança”** e aguarde a orientação do mantenedor. Não inclua componente, impacto, reprodução, anexos ou detalhes técnicos nessa solicitação pública. Não existe e-mail de segurança publicado por esta política.

No relato privado, inclua o que souber:

- versão exata, origem do pacote/commit e Windows utilizado;
- comportamento, impacto possível e condições necessárias;
- passos mínimos com dados fictícios e resultado esperado/observado;
- componente/dependência e hash do executável, quando disponíveis;
- correção sugerida, se houver.

Não é necessário ter uma exploração completa para comunicar uma suspeita. Não envie segredos ativos, clipboard, documentos, imagens ou dados de terceiros para demonstrá-la. Caso um segredo real tenha sido exposto, seu responsável deve revogá-lo/rotacioná-lo; retirar o texto de um comentário não revoga a credencial.

## Versões e triagem

O foco de manutenção é a última versão estável publicada e o código/candidato em preparação. Na consulta de **17/10/2026**:

| Versão/canal | Tratamento |
|---|---|
| v3.3.0, última estável | Recebe relatos e triagem; uma correção pode ser entregue na próxima versão, sem promessa de backport separado. |
| main e candidatos posteriores | Desenvolvimento: relatos ajudam a corrigir a próxima entrega. Não representam o pacote estável nem garantem ausência de falhas. |
| Versões anteriores | Sem compromisso de manutenção paralela. Informe se o problema ainda afeta a última estável; se não puder conferir, relate a suspeita em privado mesmo assim. |

Confira a [última estável](https://github.com/lucasllira/SlashText/releases/latest), as [releases](https://github.com/lucasllira/SlashText/releases) e a [validação da 3.3.0](https://github.com/lucasllira/SlashText/issues/70) para o estado atual. O número no projeto ou o nome de um piloto não comprova lançamento.

O mantenedor avalia alcance, impacto, reprodução e correção com quem relatou. Combine a divulgação dos detalhes para permitir a preparação de uma correção/mitigação. O projeto não oferece prazo contratual de resposta, recompensa ou backport automático. Este documento não autoriza alterar sistemas/dados de terceiros para investigar.

## Referências de manutenção

[Verificações contínuas](docs/security-baseline.md), [confiança no OCR nativo](docs/ocr-native-trust.md), [proveniência e seus limites](docs/release-provenance.md) e [controles de release](docs/repository-release-controls.md) descrevem os mecanismos existentes. Um relatório automático sem achados não certifica segurança; o atualizador mantém os limites documentados de verificação de assinatura.
