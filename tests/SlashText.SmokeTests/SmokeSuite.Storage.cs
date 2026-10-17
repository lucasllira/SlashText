using SlashText.Models;
using SlashText.Services;
using System.IO.Compression;

internal static partial class SmokeSuite
{
    private static async Task RunMigrationChecksAsync(string root)
    {
        var storageRoot = Path.Combine(root, "armazenamento portátil çã com espaços");
        var portableHome = Path.Combine(storageRoot, "origem portátil");
        var localDataRoot = Path.Combine(storageRoot, "local-app-data");
        var installedLegacy = Path.Combine(localDataRoot, "SlashDesk");
        Directory.CreateDirectory(portableHome);
        Directory.CreateDirectory(installedLegacy);
        var preservedSnippetId = Guid.NewGuid();
        await File.WriteAllTextAsync(
            Path.Combine(installedLegacy, "snippets.md"),
            $"## /preservado\n<!-- slashtext:{{\"id\":\"{preservedSnippetId}\",\"name\":\"Preservado\",\"category\":\"Migração\",\"format\":\"plain\",\"enabled\":true,\"confirmKeys\":[\"Enter\"]}} -->\n```text\nConteúdo\n```\n");
        await File.WriteAllTextAsync(Path.Combine(installedLegacy, "settings.json"),
            """{"theme":"Dark","checkUpdatesOnStartup":false}""");
        await File.WriteAllTextAsync(Path.Combine(installedLegacy, "usage.json"),
            "{\"snippets\":[{\"snippetId\":\"" + preservedSnippetId +
            "\",\"count\":9,\"charactersSaved\":42}]," +
            "\"quickAccent\":{\"count\":2,\"characters\":{}}}");
        await File.WriteAllTextAsync(Path.Combine(installedLegacy, "capture-history.json"),
            """[{"id":"history-preserved","createdAt":"2026-08-06T12:00:00-03:00","type":"monitor","mediaKind":"image","filePath":"C:\\Capturas\\preservada.png","width":800,"height":600}]""");

        var portableEnvironment = new AppDataEnvironment(
            DistributionMode.Portable,
            portableHome,
            localDataRoot);
        AppPaths.Initialize(portableEnvironment);
        Require(
            AppPaths.DataDirectory == Path.Combine(portableHome, "SlashDeskData") &&
            AppPaths.LogsDirectory == Path.Combine(portableHome, "SlashDeskData", "Logs") &&
            AppPaths.BackupsDirectory == Path.Combine(portableHome, "SlashDeskData", "Backups"),
            "modo portátil centraliza dados ao lado do executável");
        var migrationResult = AppPaths.EnsureDataLayout();
        Require(
            migrationResult.Migrated &&
            Directory.Exists(installedLegacy) &&
            File.Exists(AppPaths.SnippetsFile) &&
            File.ReadAllText(AppPaths.SnippetsFile).Contains(preservedSnippetId.ToString(), StringComparison.Ordinal) &&
            File.ReadAllText(AppPaths.CaptureHistoryFile).Contains("history-preserved", StringComparison.Ordinal) &&
            migrationResult.BackupPath is not null && File.Exists(migrationResult.BackupPath),
            "migração copia, valida, ativa e preserva origem, atalhos e histórico");
        using (var migrationArchive = ZipFile.OpenRead(migrationResult.BackupPath!))
        {
            Require(
                migrationArchive.GetEntry("migration-manifest.json") is not null &&
                migrationArchive.Entries.Any(item => item.FullName.EndsWith("snippets.md", StringComparison.Ordinal)),
                "backup de migração contém manifesto e atalhos");
        }

        var simultaneousHome = Path.Combine(storageRoot, "duas origens");
        var simultaneousData = Path.Combine(simultaneousHome, "SlashDeskData");
        Directory.CreateDirectory(simultaneousData);
        await File.WriteAllTextAsync(Path.Combine(simultaneousData, "settings.json"),
            """{"theme":"Light"}""");
        AppPaths.Initialize(new AppDataEnvironment(
            DistributionMode.Portable,
            simultaneousHome,
            localDataRoot));
        var simultaneous = AppPaths.EnsureDataLayout();
        Require(
            !simultaneous.Migrated &&
            simultaneous.CompetingSourcePreserved &&
            File.ReadAllText(AppPaths.SettingsFile).Contains("Light", StringComparison.Ordinal) &&
            simultaneous.BackupPath is not null && File.Exists(simultaneous.BackupPath),
            "duas origens priorizam portátil sem mescla destrutiva e preservam a outra origem");

        var movedHome = Path.Combine(storageRoot, "portátil movido");
        Directory.Move(portableHome, movedHome);
        AppPaths.Initialize(new AppDataEnvironment(DistributionMode.Portable, movedHome, localDataRoot));
        Require(
            File.ReadAllText(AppPaths.SnippetsFile).Contains(preservedSnippetId.ToString(), StringComparison.Ordinal) &&
            File.ReadAllText(AppPaths.CaptureHistoryFile).Contains("history-preserved", StringComparison.Ordinal),
            "atalhos e histórico acompanham a pasta portátil movida");

        var movedCaptureDirectory = Path.Combine(movedHome, "Capturas");
        Directory.CreateDirectory(movedCaptureDirectory);
        var movedCapture = Path.Combine(movedCaptureDirectory, "imagem preservada.png");
        await File.WriteAllBytesAsync(movedCapture, [1, 2, 3]);
        var relativeRecord = new CaptureRecord
        {
            FilePath = Path.Combine(portableHome, "Capturas", "imagem preservada.png"),
            PortableRelativePath = Path.Combine("Capturas", "imagem preservada.png")
        };
        Require(
            CapturePathResolver.Resolve(relativeRecord, AppPaths.Current) == movedCapture,
            "caminho relativo localiza mídia após mover a pasta portátil");

        await File.WriteAllTextAsync(AppPaths.CaptureHistoryFile,
            "[{\"id\":\"valid-item\",\"createdAt\":\"2026-08-06T12:00:00-03:00\"," +
            "\"type\":\"regiao\",\"mediaKind\":\"image\",\"filePath\":\"C:\\\\ok.png\"," +
            "\"width\":100,\"height\":80}," +
            "{\"id\":\"corrupt-item\",\"createdAt\":[],\"width\":\"inválido\"}]");
        var tolerantHistory = new CaptureService();
        await tolerantHistory.LoadAsync();
        Require(
            tolerantHistory.History.Count == 1 && tolerantHistory.History[0].Id == "valid-item",
            "item de histórico corrompido não impede carregar registros válidos");

        var editedHistory = new CaptureService();
        await editedHistory.LoadAsync();
        Require(editedHistory.IsHistoryProtected, "Partial history remains protected until explicit recovery");
        await editedHistory.RecoverHistoryAsync();
        var originalPath = Path.Combine(movedCaptureDirectory, "original-do-editor.png");
        var editedPath = Path.Combine(movedCaptureDirectory, "copia-editada.png");
        using (var originalBitmap = new System.Drawing.Bitmap(80, 60))
        using (var editedBitmap = new System.Drawing.Bitmap(40, 30))
        {
            using (var graphics = System.Drawing.Graphics.FromImage(originalBitmap)) graphics.Clear(System.Drawing.Color.Blue);
            using (var graphics = System.Drawing.Graphics.FromImage(editedBitmap)) graphics.Clear(System.Drawing.Color.Red);
            var originalRecord = await editedHistory.SaveEditedImageAsync(originalBitmap, originalPath, new CaptureSettings());
            var editedRecord = await editedHistory.SaveEditedImageAsync(editedBitmap, editedPath, new CaptureSettings(), originalRecord);
            Require(originalRecord.Id != editedRecord.Id && editedHistory.History[0].Id == editedRecord.Id &&
                    editedRecord.Width == 40 && editedRecord.Height == 30 && editedRecord.MediaKind == "image",
                "Salvar cópia registra o arquivo editado como recente, com ID e dimensões próprios");
            using (var originalFile = new System.Drawing.Bitmap(originalPath))
            using (var editedFile = new System.Drawing.Bitmap(editedHistory.ResolveFilePath(editedRecord)))
                Require(originalFile.GetPixel(10, 10).ToArgb() == System.Drawing.Color.Blue.ToArgb() &&
                        editedFile.GetPixel(10, 10).ToArgb() == System.Drawing.Color.Red.ToArgb(),
                    "Recentes aponta para pixels editados e preserva o original ao salvar uma cópia");
            using (var graphics = System.Drawing.Graphics.FromImage(editedBitmap)) graphics.Clear(System.Drawing.Color.Green);
            var updatedRecord = await editedHistory.SaveEditedImageAsync(editedBitmap, editedPath, new CaptureSettings(), editedRecord);
            Require(updatedRecord.Id == editedRecord.Id && editedHistory.History.Count(r => r.Id == editedRecord.Id) == 1,
                "Concluir atualiza o mesmo registro sem criar duplicatas");
            var reloadedHistory = new CaptureService();
            await reloadedHistory.LoadAsync();
            var reloadedRecord = reloadedHistory.History.First(r => r.Id == editedRecord.Id);
            using var latest = new System.Drawing.Bitmap(reloadedHistory.ResolveFilePath(reloadedRecord));
            Require(latest.GetPixel(10, 10).ToArgb() == System.Drawing.Color.Green.ToArgb() &&
                    reloadedRecord.Width == 40 && reloadedRecord.Height == 30,
                "reiniciar mantém a versão editada e as dimensões corretas do histórico");
            RequireThrows<ArgumentException>(
                () => editedHistory.SaveEditedImageAsync(editedBitmap, Path.Combine(movedCaptureDirectory, "nao-substituir.gif"),
                    new CaptureSettings()).GetAwaiter().GetResult(),
                "edição estática não substitui GIF animado");
        }

        var invalidPortableRoot = Path.Combine(storageRoot, "sem-permissão");
        await File.WriteAllTextAsync(invalidPortableRoot, "não é um diretório");
        var invalidPortable = new AppDataEnvironment(
            DistributionMode.Portable,
            invalidPortableRoot,
            localDataRoot);
        Require(
            !invalidPortable.TryProbePortableWrite(out var portableWriteError) &&
            !string.IsNullOrWhiteSpace(portableWriteError),
            "portátil detecta diretório sem capacidade de gravação sem alternar origem");

        AppPaths.Initialize(new AppDataEnvironment(
            DistributionMode.Installed,
            Path.Combine(storageRoot, "installed-bin"),
            localDataRoot));
        Require(
            AppPaths.DataDirectory == installedLegacy &&
            !AppPaths.IsPortable,
            "modo instalado usa %LocalAppData%\\SlashDesk");
    }

    private static async Task RunAtomicStorageChecksAsync(string root)
    {
        var atomicStorageRoot = Path.Combine(root, "atomic-storage");
        Directory.CreateDirectory(atomicStorageRoot);
        var concurrentJsonPath = Path.Combine(atomicStorageRoot, "concurrent.json");
        var concurrentStore = new JsonFileStore<AppSettings>(concurrentJsonPath);
        await Task.WhenAll(Enumerable.Range(0, 24).Select(index =>
            concurrentStore.SaveAsync(new AppSettings { Theme = $"Theme-{index}" })));
        var concurrentSettings = await concurrentStore.LoadAsync();
        Require(
            concurrentSettings.Theme.StartsWith("Theme-", StringComparison.Ordinal) &&
            Directory.GetFiles(atomicStorageRoot, ".concurrent.json-*.tmp").Length == 0,
            "gravações JSON concorrentes produzem um arquivo completo sem temporários");

        var preservedPath = Path.Combine(atomicStorageRoot, "preserved.txt");
        await File.WriteAllTextAsync(preservedPath, "último conteúdo válido");
        await RequireThrowsAsync<IOException>(
            () => AtomicFilePersistence.WriteAsync(
                preservedPath,
                async (stream, token) =>
                {
                    await stream.WriteAsync("conteúdo incompleto"u8.ToArray(), token);
                    throw new IOException("falha simulada antes da substituição");
                }),
            "falha antes da substituição é propagada");
        Require(
            await File.ReadAllTextAsync(preservedPath) == "último conteúdo válido",
            "falha de gravação preserva o arquivo válido anterior");

        using (var cancelledWrite = new CancellationTokenSource())
        {
            cancelledWrite.Cancel();
            await RequireThrowsAsync<OperationCanceledException>(
                () => AtomicFilePersistence.WriteTextAsync(
                    preservedPath,
                    "não deve substituir",
                    System.Text.Encoding.UTF8,
                    cancelledWrite.Token),
                "gravação cancelada é interrompida");
        }
        Require(
            await File.ReadAllTextAsync(preservedPath) == "último conteúdo válido",
            "cancelamento preserva o arquivo válido anterior");

        var corruptJsonPath = Path.Combine(atomicStorageRoot, "settings.json");
        const string corruptJson = "{\"theme\": \"Dark\", conteúdo inválido";
        await File.WriteAllTextAsync(corruptJsonPath, corruptJson);
        var corruptStore = new JsonFileStore<AppSettings>(corruptJsonPath);
        var fallbackSettings = await corruptStore.LoadAsync();
        var preservedCorruptFiles = Directory.GetFiles(
            atomicStorageRoot,
            "settings.json.corrupt-*.bak");
        Require(
            fallbackSettings.Theme == new AppSettings().Theme &&
            preservedCorruptFiles.Length == 1 &&
            await File.ReadAllTextAsync(preservedCorruptFiles[0]) == corruptJson,
            "JSON inválido é preservado e retorna configuração padrão");
        _ = await corruptStore.LoadAsync();
        Require(
            Directory.GetFiles(atomicStorageRoot, "settings.json.corrupt-*.bak").Length == 1,
            "preservação de JSON corrompido é idempotente");
    }
}
