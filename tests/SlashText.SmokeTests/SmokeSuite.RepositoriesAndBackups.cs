using SlashText.Models;
using SlashText.Services;
using System.IO.Compression;

internal static partial class SmokeSuite
{
    private static async Task RunRepositoryAndBackupChecksAsync(string root, string snippetsFile, string backups)
    {
        var repository = new SnippetMarkdownRepository(snippetsFile, backups);
        var snippet = new Snippet
        {
            Name = "Teste",
            Trigger = "/teste",
            Category = "Geral",
            Content = "Primeiro"
        };

        await repository.SaveAsync([snippet]);
        snippet.Content = "Segundo";
        await repository.SaveAsync([snippet]);
        snippet.Content = "Terceiro";
        await repository.SaveAsync([snippet]);

        var loaded = await repository.LoadAsync();
        Require(loaded.Count == 1 && loaded[0].Content == "Terceiro", "persistência Markdown");

        var colonSnippet = new Snippet
        {
            Name = "Dois pontos legado",
            Trigger = ":teste",
            Category = "Geral",
            Content = "Preservado sem ativar",
            HasLegacyIncompatibleTrigger = true
        };
        await repository.SaveAsync([snippet, colonSnippet]);
        loaded = await repository.LoadAsync();
        Require(
            loaded.Any(item =>
                item.Trigger == ":teste" &&
                item.HasLegacyIncompatibleTrigger),
            "gatilho antigo com dois-pontos é preservado como legado incompatível");

        var legacySnippet = new Snippet
        {
            Name = "Legado preservado",
            Trigger = "/legado.incompatível",
            Category = "Geral",
            Content = "Preservar sem ativar",
            HasLegacyIncompatibleTrigger = true
        };
        await repository.SaveAsync([snippet, colonSnippet, legacySnippet]);
        loaded = await repository.LoadAsync();
        Require(
            loaded.Any(item => item.Trigger == legacySnippet.Trigger && item.HasLegacyIncompatibleTrigger),
            "gatilho legado incompatível é preservado e sinalizado");

        var textBlazeFile = Path.Combine(root, "textblaze.json");
        await File.WriteAllTextAsync(
            textBlazeFile,
            """
            {
              "version": 7,
              "folders": [{
                "name": "Atendimento",
                "snippets": [{
                  "name": "Resposta diária",
                  "shortcut": "/diario",
                  "type": "html",
                  "text": "Data: {time: DD/MM/YYYY; shift=-1D} {key: tab}Pronto"
                }]
              }]
            }
            """);
        var importService = new SnippetImportService();
        var textBlazeImport = await importService.ImportAsync(
            textBlazeFile,
            SnippetImportSource.TextBlaze);
        Require(
            textBlazeImport.Snippets.Count == 1 &&
            textBlazeImport.Snippets[0].Category == "Atendimento",
            "importa pastas e atalhos do Text Blaze");
        Require(
            textBlazeImport.Snippets[0].Content.Contains(
                "{{data:-1d|dd/MM/yyyy}} {{tab}}",
                StringComparison.Ordinal),
            "converte data e Tab do Text Blaze");

        var espansoFile = Path.Combine(root, "base.yml");
        await File.WriteAllTextAsync(
            espansoFile,
            """
            matches:
              - trigger: ":ola"
                label: "Saudação"
                replace: |
                  Olá!
                  Como posso ajudar?
              - triggers: [":obg", ":thanks"]
                replace: "Obrigado!"
              - trigger: "/ola"
                replace: "Entrada duplicada"
            """);
        var espansoImport = await importService.ImportAsync(
            espansoFile,
            SnippetImportSource.Espanso);
        Require(
            espansoImport.Snippets.Count == 3 &&
            espansoImport.Snippets.Any(item => item.Trigger == "/ola") &&
            espansoImport.Snippets.Any(item => item.Trigger == "/obg") &&
            espansoImport.Snippets.Any(item => item.Trigger == "/thanks"),
            "importa Espanso convertendo todos os prefixos para barra");
        Require(
            espansoImport.Warnings.Count(item =>
                item.Contains("prefixo ':' convertido", StringComparison.Ordinal)) == 3 &&
            espansoImport.Warnings.Any(item =>
                item.Contains("duplicado", StringComparison.OrdinalIgnoreCase)),
            "importação informa conversões e conflito após normalizar prefixos");

        var usageFile = Path.Combine(root, "usage.json");
        var settingsFile = Path.Combine(root, "settings.json");
        await File.WriteAllTextAsync(settingsFile, """{"theme":"System"}""");
        await File.WriteAllTextAsync(
            usageFile,
            System.Text.Json.JsonSerializer.Serialize(new List<UsageRecord>
            {
                new() { SnippetId = snippet.Id, Count = 3 }
            }));
        var usage = new UsageService(usageFile);
        await usage.LoadAsync();
        Require(usage.Records.Count == 1 && usage.Records[0].Count == 3, "migração de estatísticas antigas");
        await usage.RecordQuickAccentAsync('á');
        var reloadedUsage = new UsageService(usageFile);
        await reloadedUsage.LoadAsync();
        Require(reloadedUsage.QuickAccent.Count == 1, "estatística do Acento Rápido");
        Require(
            reloadedUsage.QuickAccent.Characters.GetValueOrDefault("á") == 1,
            "ranking de caracteres acentuados");

        var backupService = new BackupService(
            backups,
            [snippetsFile, settingsFile, usageFile]);
        backupService.CreateDailySnapshot();
        backupService.CreateDailySnapshot();
        var backupFiles = Directory.GetFiles(backups, "SlashDesk-backup-*.zip");
        Require(backupFiles.Length == 1, "um backup consolidado por dia");
        using (var archive = ZipFile.OpenRead(backupFiles[0]))
        {
            Require(
                archive.Entries.Select(item => item.Name).Order()
                    .SequenceEqual(["backup-manifest.json", "settings.json", "snippets.md", "usage.json"]),
                "backup contém manifesto, atalhos, preferências e estatísticas");
            var manifestEntry = archive.GetEntry("backup-manifest.json");
            Require(manifestEntry is not null, "backup contém manifesto único");
            using var manifest = System.Text.Json.JsonDocument.Parse(manifestEntry!.Open());
            var manifestFiles = manifest.RootElement.GetProperty("files")
                .EnumerateArray()
                .ToList();
            Require(
                manifest.RootElement.GetProperty("schemaVersion").GetInt32() == 2 &&
                manifestFiles.Count == 3 &&
                manifestFiles.All(item =>
                    item.GetProperty("size").GetInt64() >= 0 &&
                    item.GetProperty("sha256").GetString()?.Length == 64),
                "manifesto v2 registra tamanho e SHA-256 de cada arquivo");
        }
        BackupService.ValidateSnapshot(backupFiles[0]);
        var manualBackup = backupService.CreateManualSnapshot();
        Require(
            File.Exists(manualBackup) && backupService.ListSnapshots().Count == 2,
            "backup manual e listagem de cópias");

        var assetSource = Path.Combine(root, "backup-assets");
        var nestedAssetDirectory = Path.Combine(assetSource, "imagens", "nested");
        Directory.CreateDirectory(nestedAssetDirectory);
        var nestedAsset = Path.Combine(nestedAssetDirectory, "logo.png");
        await File.WriteAllBytesAsync(nestedAsset, [1, 2, 3, 4, 5, 6]);
        var assetBackups = Path.Combine(root, "asset-backups");
        var assetBackupService = new BackupService(
            assetBackups,
            [snippetsFile],
            assetSource);
        var assetBackup = assetBackupService.CreateManualSnapshot();
        using (var archive = ZipFile.OpenRead(assetBackup))
        {
            var assetEntry = archive.GetEntry("assets/imagens/nested/logo.png");
            var manifestEntry = archive.GetEntry("backup-manifest.json");
            Require(
                assetEntry is not null && manifestEntry is not null,
                "asset aninhado e manifesto são incluídos");
            using var manifest = System.Text.Json.JsonDocument.Parse(manifestEntry!.Open());
            var recordedAsset = manifest.RootElement.GetProperty("files")
                .EnumerateArray()
                .Single(item =>
                    item.GetProperty("path").GetString() ==
                    "assets/imagens/nested/logo.png");
            var expectedAssetHash = Convert.ToHexString(
                    System.Security.Cryptography.SHA256.HashData(
                        await File.ReadAllBytesAsync(nestedAsset)))
                .ToLowerInvariant();
            Require(
                recordedAsset.GetProperty("size").GetInt64() == 6 &&
                recordedAsset.GetProperty("sha256").GetString() == expectedAssetHash,
                "manifesto registra tamanho e hash dos bytes arquivados do asset");
        }
        BackupService.ValidateSnapshot(assetBackup);
        Require(
            Directory.GetFiles(assetBackups, "*.tmp").Length == 0,
            "backup validado é publicado sem temporários residuais");

        var tamperedBackup = Path.Combine(root, "tampered-backup.zip");
        File.Copy(manualBackup, tamperedBackup);
        using (var archive = ZipFile.Open(tamperedBackup, ZipArchiveMode.Update))
        {
            var entry = archive.GetEntry("settings.json")
                ?? throw new InvalidOperationException("settings ausente no teste");
            entry.Delete();
            var changed = archive.CreateEntry("settings.json");
            using var writer = new StreamWriter(changed.Open());
            writer.Write("{\"theme\":\"adulterado\"}");
        }
        RequireThrows<InvalidDataException>(
            () => BackupService.ValidateSnapshot(tamperedBackup),
            "adulteração de backup é detectada por tamanho ou hash");

        var duplicateBackup = Path.Combine(root, "duplicate-backup.zip");
        File.Copy(manualBackup, duplicateBackup);
        using (var archive = ZipFile.Open(duplicateBackup, ZipArchiveMode.Update))
        {
            var duplicateEntry = archive.CreateEntry("settings.json");
            using var writer = new StreamWriter(duplicateEntry.Open());
            writer.Write("{}");
        }
        RequireThrows<InvalidDataException>(
            () => BackupService.ValidateSnapshot(duplicateBackup),
            "entrada duplicada no ZIP é rejeitada");

        var legacyBackup = Path.Combine(root, "legacy-schema-1.zip");
        using (var archive = ZipFile.Open(legacyBackup, ZipArchiveMode.Create))
        {
            var snippetsEntry = archive.CreateEntry("snippets.md");
            using (var writer = new StreamWriter(snippetsEntry.Open()))
            {
                writer.Write("# backup legado");
            }
            var manifestEntry = archive.CreateEntry("backup-manifest.json");
            using var manifestWriter = new StreamWriter(manifestEntry.Open());
            manifestWriter.Write(
                "{\"schemaVersion\":1,\"files\":[\"snippets.md\"]}");
        }
        BackupService.ValidateSnapshot(legacyBackup);

        var restorePackageData = Path.Combine(root, "restore-package-data");
        var restorePackageAssets = Path.Combine(restorePackageData, "assets");
        Directory.CreateDirectory(restorePackageAssets);
        var packageSnippets = Path.Combine(restorePackageData, "snippets.md");
        var packageSettings = Path.Combine(restorePackageData, "settings.json");
        await File.WriteAllTextAsync(packageSnippets, "atalhos restaurados");
        await File.WriteAllTextAsync(packageSettings, "{\"theme\":\"Light\"}");
        await File.WriteAllTextAsync(
            Path.Combine(restorePackageAssets, "novo.png"),
            "asset restaurado");
        var restorePackages = Path.Combine(root, "restore-packages");
        var restorePackageService = new BackupService(
            restorePackages,
            [packageSnippets, packageSettings],
            restorePackageAssets,
            restorePackageData);
        var restorePackage = restorePackageService.CreateManualSnapshot();

        var activeRestoreData = Path.Combine(root, "active-restore-data");
        var activeRestoreAssets = Path.Combine(activeRestoreData, "assets");
        Directory.CreateDirectory(activeRestoreAssets);
        var activeSnippets = Path.Combine(activeRestoreData, "snippets.md");
        var activeSettings = Path.Combine(activeRestoreData, "settings.json");
        await File.WriteAllTextAsync(activeSnippets, "atalhos atuais");
        await File.WriteAllTextAsync(activeSettings, "{\"theme\":\"Dark\"}");
        await File.WriteAllTextAsync(
            Path.Combine(activeRestoreAssets, "antigo.png"),
            "asset atual");
        var restoreSafetyBackups = Path.Combine(
            activeRestoreData,
            "Backups");
        var restoreService = new BackupService(
            restoreSafetyBackups,
            [activeSnippets, activeSettings],
            activeRestoreAssets,
            activeRestoreData);
        restoreService.RestoreSnapshot(restorePackage);
        Require(
            await File.ReadAllTextAsync(activeSnippets) ==
                "atalhos restaurados" &&
            await File.ReadAllTextAsync(activeSettings) ==
                "{\"theme\":\"Light\"}" &&
            File.Exists(Path.Combine(activeRestoreAssets, "novo.png")) &&
            !File.Exists(Path.Combine(activeRestoreAssets, "antigo.png")) &&
            Directory.GetFiles(
                restoreSafetyBackups,
                "SlashDesk-backup-before-restore-*.zip").Length == 1,
            "restauração aplica arquivos e substitui assets após criar backup de segurança");
        Require(
            Directory.GetDirectories(
                restoreSafetyBackups,
                ".restore-*").Length == 0,
            "restauração concluída remove staging e rollback");

        await File.WriteAllTextAsync(activeSnippets, "atalhos antes da falha");
        await File.WriteAllTextAsync(activeSettings, "{\"theme\":\"Safe\"}");
        Directory.Delete(activeRestoreAssets, recursive: true);
        Directory.CreateDirectory(activeRestoreAssets);
        await File.WriteAllTextAsync(
            Path.Combine(activeRestoreAssets, "seguro.png"),
            "asset antes da falha");
        restoreService.BeforeRestoreApply = (step, _) =>
        {
            if (step == 1)
            {
                throw new IOException("falha simulada durante aplicação");
            }
        };
        RequireThrows<IOException>(
            () => restoreService.RestoreSnapshot(restorePackage),
            "falha intermediária da restauração é propagada");
        restoreService.BeforeRestoreApply = null;
        Require(
            await File.ReadAllTextAsync(activeSnippets) ==
                "atalhos antes da falha" &&
            await File.ReadAllTextAsync(activeSettings) ==
                "{\"theme\":\"Safe\"}" &&
            File.Exists(Path.Combine(activeRestoreAssets, "seguro.png")) &&
            !File.Exists(Path.Combine(activeRestoreAssets, "novo.png")),
            "rollback restaura integralmente arquivos e assets anteriores");

        var traversalBackup = Path.Combine(root, "restore-traversal.zip");
        using (var archive = ZipFile.Open(
                   traversalBackup,
                   ZipArchiveMode.Create))
        {
            var manifestEntry = archive.CreateEntry("backup-manifest.json");
            using (var writer = new StreamWriter(manifestEntry.Open()))
            {
                writer.Write(
                    "{\"schemaVersion\":1,\"files\":[\"snippets.md\"]}");
            }
            var snippetsEntry = archive.CreateEntry("snippets.md");
            using (var writer = new StreamWriter(snippetsEntry.Open()))
            {
                writer.Write("não deve ser aplicado");
            }
            var traversalEntry = archive.CreateEntry("../escape.txt");
            using var traversalWriter = new StreamWriter(traversalEntry.Open());
            traversalWriter.Write("inseguro");
        }
        RequireThrows<InvalidDataException>(
            () => restoreService.RestoreSnapshot(traversalBackup),
            "path traversal é bloqueado antes de alterar dados ativos");
        Require(
            await File.ReadAllTextAsync(activeSnippets) ==
                "atalhos antes da falha" &&
            !File.Exists(Path.Combine(root, "escape.txt")),
            "backup inseguro não modifica dados nem escreve fora do staging");

        var symlinkBackup = Path.Combine(root, "restore-symlink.zip");
        using (var archive = ZipFile.Open(
                   symlinkBackup,
                   ZipArchiveMode.Create))
        {
            var manifestEntry = archive.CreateEntry("backup-manifest.json");
            using (var writer = new StreamWriter(manifestEntry.Open()))
            {
                writer.Write(
                    "{\"schemaVersion\":1,\"files\":[\"snippets.md\"]}");
            }
            var snippetsEntry = archive.CreateEntry("snippets.md");
            using (var writer = new StreamWriter(snippetsEntry.Open()))
            {
                writer.Write("não deve ser aplicado");
            }
            var linkEntry = archive.CreateEntry("assets/link");
            linkEntry.ExternalAttributes = 0xA000 << 16;
            using var linkWriter = new StreamWriter(linkEntry.Open());
            linkWriter.Write("destino");
        }
        RequireThrows<InvalidDataException>(
            () => restoreService.RestoreSnapshot(symlinkBackup),
            "links simbólicos no ZIP são rejeitados");

        restoreService.RestoreSnapshot(legacyBackup);
        Require(
            await File.ReadAllTextAsync(activeSnippets) ==
                "# backup legado",
            "backup schema 1 continua restaurável");
    }
}
