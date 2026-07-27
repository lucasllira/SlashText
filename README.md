# SlashText

Aplicativo portátil para Windows que expande atalhos iniciados por `/` em e-mails,
Teams, navegadores e outros campos de texto.

## Objetivos da V1

- cadastro e edição dos atalhos pela interface;
- persistência legível em `snippets.md`;
- variáveis preenchíveis, data, hora e cálculo simples de datas;
- expansão confirmada por Enter, Tab ou Espaço;
- texto simples e formatado;
- backups automáticos;
- estatísticas locais básicas;
- execução na bandeja e inicialização com o Windows;
- publicação portátil, self-contained e single-file.

## Tecnologia

- C#;
- WPF;
- .NET 10 LTS;
- sem banco de dados e sem conexão com internet.

## Estrutura

```text
SlashText/
├── src/SlashText/
│   ├── Models/
│   ├── Services/
│   ├── App.xaml
│   └── MainWindow.xaml
├── snippets.md
└── SlashText.sln
```

## Executar

Em um Windows com o SDK do .NET 10:

```powershell
dotnet restore
dotnet run --project .\src\SlashText\SlashText.csproj
```

## Publicar como aplicativo portátil

```powershell
dotnet publish .\src\SlashText\SlashText.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -o .\publish
```

O executável publicado não exige instalação do .NET.

## Privacidade

O SlashText não envia dados para a internet. O detector global deverá manter
somente um buffer curto iniciado por `/`, sem registrar o restante da digitação.
