# Visual Contract — barra de marcações

## Objetivo

Este contrato transforma o mockup aprovado em requisitos verificáveis. Ele não
altera captura, posicionamento monitor-aware, anotações, histórico ou saída do
bitmap. A primeira entrega é uma janela WPF de preview aberta com:

```text
SlashDesk.exe --capture-toolbar-preview
```

O preview não inicializa dados, bandeja, atualização ou monitoramento de teclado.
Sua janela respeita a área útil do Windows e reduz automaticamente de 1280 × 800
quando DPI, resolução ou barra de tarefas disponibilizam menos espaço.

## Relação com o Design System

O Design System fornece tokens e componentes reutilizáveis para o aplicativo.
Este Visual Contract fixa a aparência e os estados de um componente específico.
Após aprovação, os assets canônicos poderão substituir os ícones provisórios da
captura real sem reinterpretar o desenho.

## Tokens — tema preto

| Token | Valor | Uso |
|---|---:|---|
| Canvas | `#000000` | fundo grande e overlay |
| Toolbar | `#111111` com 97% | barra flutuante |
| Elevated | `#1A1A1A` | popovers |
| Hover | `#252525` | hover neutro |
| Pressed | `#080808` | pressionado |
| Selected | `#10373C` | seleção discreta |
| Border | `#3A3A3A` | contornos |
| Text | `#F5F5F5` | texto e ícone principal |
| Muted | `#B8B8B8` | texto secundário |
| Accent | `#22C7D9` | ação e seleção |
| Danger | `#FF6B73` | ação destrutiva |

## Medidas obrigatórias

| Elemento | Medida |
|---|---:|
| Barra principal | 54 px de altura visual |
| Botão de ícone | 40 × 40 px |
| Ícone | 20 × 20 px em grade óptica |
| Viewport do ícone | 20 × 20 px reais, sem `Stretch` |
| Botão Capturar | 86 × 40 px |
| Segmento de menu | 32 × 40 px |
| Gap interno | 2 px |
| Separador | 1 × 24 px |
| Raio da barra | 12 px |
| Raio dos botões | 8 px |
| Borda selecionada | 1 px ciano |
| Popover | 12 px de raio; 12 px de padding |

## Assets canônicos

Os `Geometry` com prefixo `PreviewIcon.` em
`Styles/CaptureToolbarVisualContract.xaml` são a fonte oficial desta etapa.
É proibido substituir um asset por texto, emoji monocromático, fonte de ícones ou
outra geometria durante a integração. O ícone Número é um marcador circular com
o número 1, nunca um quadrado contendo “12”.

## Estados demonstrados

1. Barra principal: ferramenta Seta selecionada.
2. Menu Capturar: configuração atual, Copiar e Salvar.
3. Formas: retângulo, elipse, linha, seta, número e emoticon; propriedades de
   preenchimento e contorno.
4. Emoticons: 36 carimbos Unicode frequentes, categorias, rolagem e tamanho
   configurável. A seleção foi ampliada com base nos caracteres comuns exibidos
   por teclados móveis, incluindo a referência visual fornecida do Google.

## Regras de comportamento

- Barra e popovers permanecem dentro da área útil do monitor.
- O preview não substitui os testes de 100%, 125% e 150% de DPI.
- Somente uma ferramenta principal pode ficar selecionada.
- Popovers devem ser ancorados ao comando que os abriu.
- Escape fecha o preview; teclas 1 a 4 alternam os estados.
- O preto é neutro. Não são permitidas superfícies azul-marinho.
- Ícones usam espessura 1,6, terminação arredondada e grade 20 × 20.
- O botão reserva exatamente 20 × 20 px depois de borda e padding; nenhum traço
  pode tocar ou ultrapassar o viewport.
- Emoticons usam Segoe UI Emoji. Cada item também possui uma cor de fallback
  explícita para permanecer legível quando o WPF renderiza o glifo monocromático.

## Critério de aprovação

Cada estado deve ser capturado em 1440 × 900 no Windows. A aprovação manual
verifica primeiro composição, hierarquia e desenho dos ícones. Depois, uma
comparação perceptual pode detectar regressões; diferenças de antialiasing de
fonte não devem reprovar sozinhas.

Checklist:

- [ ] Número é reconhecido imediatamente como marcador numerado.
- [ ] Todos os ícones possuem o mesmo peso e alinhamento óptico.
- [ ] Capturar é percebido como botão dividido.
- [ ] Menu Capturar é compacto e ancorado.
- [ ] Formas e propriedades aparecem em uma barra contextual independente.
- [ ] Emoticons são coloridos, possuem 36 opções e continuam legíveis com rolagem.
- [ ] Estados selecionados não parecem botões grandes ou pesados.
- [ ] Não existem superfícies azuladas no tema preto.
- [ ] Nenhuma ação é cortada nas bordas do monitor.
- [ ] Preview aprovado antes de integrar os assets à captura real.
