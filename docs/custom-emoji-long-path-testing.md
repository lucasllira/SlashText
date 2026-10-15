# Importação de emojis em caminhos longos (#138)

`CaptureStampCatalog.Import` grava o PNG normalizado em `FileStream`, fechado
antes de publicar o índice. Isso evita o limite do overload de filename do GDI+
em caminhos que o .NET consegue abrir. A limpeza de uma importação que falhou
remove somente o destino criado por aquela operação. Formato do catálogo,
normalização e limites de bytes/pixels permanecem iguais.

## Teste focado

Após compilar Release/x64, executar com uma pasta base curta e exclusiva:

```powershell
dotnet build SlashText.sln -c Release -p:Platform=x64
dotnet ./tests/SlashText.SmokeTests/bin/x64/Release/net10.0-windows/SlashText.SmokeTests.dll --capture-stamp-path-smoke "$env:TEMP/emoji-138"
```

O teste gera subpastas próprias, verifica os caminhos absolutos antes de removê-las
e preserva a pasta base. O modo focado salva `result.json` com status PASS/FAIL;
falha também produz saída diferente de zero e detalhes no stderr. Usar o runtime
Windows/.NET do build. Em um sandbox, escolher uma área gravável que permita
criação e movimentação atômica de arquivos, e não somente criação.

As origens e o catálogo do cenário de caminhos longos excedem 260 caracteres e
incluem espaços e Unicode. As fixtures também são gravadas por stream, para o
teste alcançar o `Import` real sem falhar na preparação. A pasta base precisa ser
curta o suficiente para o cenário independente de bloqueio do índice ficar abaixo
de 260 caracteres; uma assertion explícita verifica essa pré-condição.

## Cenários e evidência

- PNG transparente 1200×600 vira 512×256; JPEG 600×1200 vira 256×512;
  PNG 32×20 mantém tamanho. Pixels centrais, alpha, proporção e PNG persistido
  são verificados após normalização e recarga.
- Rótulos Unicode, identidades e entradas/arquivos anteriores permanecem intactos.
  Os originais podem ser removidos e `ValidateDirectory` continua funcionando.
- Um handle permite ler o índice e impede sua substituição. A importação deve
  reportar falha de acesso/compartilhamento, manter um snapshot de todos os bytes
  anteriores e não deixar PNG ou temporário novo órfão. Um temporário anterior
  fictício deve sobreviver. Ao liberar o único bloqueio, a mesma importação
  funciona e permanece no catálogo recarregado.

A mensagem Win32 de falha pode omitir o caminho. O teste verifica tipo/HResult,
preservação dos dados e sucesso após liberar o único bloqueio, sem depender de
texto localizado, eventos temporizados de filesystem ou stack trace.

Os cenários também integram a suíte geral `SlashText.SmokeTests`, já executada
pelo CI. Não há acesso ao clipboard pessoal, captura de tela/áudio, mudança de
ACL ou necessidade de teste manual para comprovar esta correção localizada.
