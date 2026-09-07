# SlashDesk 3.3 — contrato de implementação

Este pacote acompanha o Visual Lab. Ele fornece fonte de referência e recursos iniciais; não é um patch pronto nem uma conversão automática para WPF.

## Arquivos
- `reference/app/page.tsx`: seis telas, navegação, estados e fluxos de demonstração.
- `reference/app/capture-simulation.tsx`: seleção, movimento/redimensionamento, anotação e recorte.
- `reference/app/studio.css`: geometria, tipografia, estados, temas, regras de adaptação e animações exatas.
- `reference/app/globals.css`: estilos existentes usados pelo Lab anterior e pelo catálogo.
- `tokens.json`: cores extraídas diretamente do CSS e medidas/movimentos centrais. O CSS é a referência completa para exceções por largura.
- `light.xaml` e `black.xaml`: ResourceDictionary com brushes reais extraídos do CSS. Não foram compilados no Windows; exigem integração com o ThemeService existente.
- `implementation.md`: plano de migração, mapa de componentes e critérios de fidelidade.

Os arquivos React dependem dos componentes e do ambiente do projeto Sites; esta seleção de fontes serve como contrato, não como aplicativo autônomo. A versão completa continua no repositório de origem do Site. O exemplo do Lab não deve substituir serviços de armazenamento, expansão, captura, importação ou atualização do desktop.
