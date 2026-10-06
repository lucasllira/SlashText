# Google Noto Emoji

Os 36 arquivos PNG desta pasta são uma seleção do projeto oficial
[googlefonts/noto-emoji](https://github.com/googlefonts/noto-emoji), diretório
`png/128`, obtidos em 29 de agosto de 2026.

Eles são distribuídos somente para manter a mesma aparência entre a paleta,
o overlay da captura e o bitmap exportado pelo SlashDesk. Consulte
`LICENSE.txt` e o repositório de origem para os termos aplicáveis.

Noto é uma marca registrada da Google LLC. Este uso não implica endosso.

## Catálogo completo do piloto Captura 3.3.0

`catalog.json` contém 3.972 sequências únicas com imagem disponível no snapshot
`e20cbc2bbec1926686be9f9bee7d1d2cfa1fea0e` de googlefonts/noto-emoji,
diretórios `2D/png/128` e `third_party/region-flags/png`. As 36 opções acima são apenas o acesso rápido legado.
Não incluímos componentes ASCII/flags isolados, aliases duplicados ou glyphs PUA.
Sequências novas sem PNG nesse snapshot não são substituídas por outra imagem.
Essa é a coleção Unicode/Noto pública; não inclui emojis personalizados de
organizações do Google Chat, animações ou Emoji Kitchen.

Os PNGs completos são recuperados **durante a compilação**, em commit fixo,
via `scripts/restore-noto-emoji.ps1`. Cada arquivo é verificado pelo hash Git
registrado no manifesto; a coleção e esta atribuição são incorporadas ao EXE.
Não há download, consulta ou dependência de rede em tempo de execução.
Para desenvolver/compilar: .NET 10, Git e PowerShell 7 (`pwsh`).
Os arquivos gerados em `Full/` não são versionados; o script aceita
`-SourceDirectory` para fornecer um checkout local do snapshot revisado.

Categorias/ordem/shortcodes derivados de googlefonts/emoji-metadata,
`emoji_18_0_ordering.json` (Apache 2.0). Nomes/palavras-chave em português
derivados das anotações Unicode CLDR `pt`, inclusive `annotationsDerived`;
ordem/nome alternativo de `emoji-test.txt` 18.0. Os hashes SHA-256 dos quatro
inputs ficam no manifesto. `UNICODE-LICENSE.txt` contém a licença Unicode
dos metadados. Os nomes sem tradução CLDR usam a descrição inglesa Unicode.
Os PNGs e os metadados Google seguem `LICENSE.txt` (Apache 2.0).
As bandeiras são os assets originais de `third_party/region-flags`, sem as
transformações de onda/sombra geradas na fonte Noto. Consulte `FLAGS-LICENSE.txt`.

Fontes:
- https://github.com/googlefonts/noto-emoji
- https://github.com/googlefonts/emoji-metadata
- https://github.com/unicode-org/cldr-json
- https://unicode.org/Public/emoji/18.0/emoji-test.txt
