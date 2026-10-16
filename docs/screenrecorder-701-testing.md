# ScreenRecorderLib 7.0.1 — migração e validação #151

O backend MP4 usa 7.0.1 no app e nos testes. As APIs de áudio antigas foram
substituídas por fontes explícitas: loopback para PC e captura para microfone.
Endpoints são enumerados diretamente pela API MMDevice do Windows, sem consulta
de dispositivo padrão. Isso evita uma desreferência nula na enumeração da 7.0.1
quando não existe endpoint padrão, observada no CI. Sem dispositivos retorna
lista vazia; indisponibilidade/erro COM mantém a mensagem de erro controlada.
IDs/friendly names continuam sendo os mesmos identificadores do Windows.
Somente as fontes habilitadas são construídas; sem áudio mantém lista vazia e
trilha desativada. ID vazio continua representando o padrão do Windows, sem
persistir uma resolução específica do dispositivo. Configurações existentes,
defaults, volumes, modo/área e a fila nativa permanecem compatíveis.

Mute usa cópias das fontes com a mesma identidade da sessão. Atualizar uma fonte
com ID novo não atinge a fonte nativa existente na versão 7. Clones mantêm
endpoint, flags e configuração mono; volume zero silencia sem criar novas
fontes. A configuração original não é modificada se a aplicação nativa falhar.
Não habilitar áudio que estava desabilitado no início. A pausa mantém o gate
existente que impede mudanças de áudio até retomar.

Origem revisada: tag `v7.0.1`, commit
`646383234187efd3854e6b1069612b80c3ab9981` do projeto upstream; pacote NuGet
7.0.1 com assinatura de repositório verificada. LICENSE do pacote e aviso de
fifo_map da tag são iguais aos textos preservados. Catálogo/versões/NOTICE foram
atualizados. Não é comprovação da correspondência de cada byte nativo com uma
compilação reproduzível a partir dessa tag.

## Automação sem gravação pessoal

`RecordingAudioMigrationChecks`, na suíte geral, verifica quatro combinações
de fontes × padrão/ID explícito, contagem/tipo de fontes, seleção e volume,
identidades estáveis em mute/restore, ausência de fontes extras, preservação de
preferências e flags. Também faz enumeração somente leitura de endpoints e
verifica listas/erro controlado, sem capturar áudio ou registrar nomes/IDs.

Os smokes WPF de áudio/preparação, reserva da operação, destinos e avisos
offline são reutilizados. CI confirma a suíte geral/inventário no head entregue.
Esses testes não comprovam áudio físico nem encerramento sob falha de driver.

## Teste nativo guiado, iniciado pelo usuário

Feche outros pilotos pela bandeja. Extraia o pacote completo em pasta nova e
use o arquivo `Testar-Audio.cmd`. Ele chama o script deste repositório
`scripts/recording-audio-native-smoke.ps1`, com o EXE do próprio pacote. O teste
só começa depois de pressionar Enter; fechar essa janela antes disso cancela.

O teste grava uma janela fictícia, reproduz um tom e pode captar o microfone e
o áudio do PC. Prepare apenas conteúdo autorizado, sem outras fontes privadas.
Nada é enviado. Os quatro MP4 ficam em `RESULTADOS/run-...`; podem conter som
ambiente/voz. Compartilhe somente o resultado textual sem conteúdo pessoal.

1. Inicie `Testar-Audio.cmd` e confirme quando estiver pronto. Durante as fases
   com microfone, pode dizer uma frase fictícia. Aguarde as quatro amostras.
2. A automação verifica vídeo, presença/ausência de trilha de áudio, duração e
   alinhamento temporal após mute/pausa. Com PC, começa sem o tom e interrompe o
   tom por dois segundos para exercitar silêncio/retomada da 7.0.1. Feche outras
   fontes de áudio para que essa fase seja realmente silenciosa. O script não
   certifica silêncio se outra aplicação estiver reproduzindo som.
3. Se faltar fonte, o resultado é INCOMPLETE, nunca PASS para as quatro
   combinações. Informe a combinação omitida; não precisa mudar configurações
   globais para fazer o teste passar.
4. Reproduza `silent`, `computer`, `microphone` e `both`: imagem da janela
   fictícia; sem som em silent; tom em computer; microfone em microphone; ambos
   em both. Pode haver silêncio nas fases planejadas de mute. Confirme que
   retomada/finalização não cortam o áudio e que a fala acompanha a imagem.
5. `summary.json` registra hash do EXE e verificações automáticas. A escuta fica
   PENDING até seu retorno; a existência da trilha não comprova conteúdo audível.

Depois, abra o piloto normalmente com `Abrir-Piloto.cmd` e use somente a janela
de referência `TESTES/referencia.html`: confira um recorte em Região e essa
Janela no MP4, selecionando dispositivos/volumes antes de iniciar. Cada arquivo
deve conter a área escolhida; cancele uma preparação e inicie outra. Saia pelo
menu Sair da bandeja. Nenhum teste precisa importar seus dados reais.

Responda com resultado automático, reprodução dos quatro arquivos e
Região/Janela/dispositivo, ou indique a etapa que falhou. Não teste travamentos
de driver ou perda forçada de hardware aqui.

## Decisões remanescentes

A atualização não resolve automaticamente #106. Parada anormal e a integração
da PR #110 continuam separadas e exigem evidência; os testes normais anteriores
não aprovam outro artefato. A #151 só entra na main após CI e validação aplicável.
Nenhuma publicação estável/tag é autorizada por este roteiro. Próxima #106,
GPT-6.1 Sol/high; depois aceite #84 e candidato #70.
