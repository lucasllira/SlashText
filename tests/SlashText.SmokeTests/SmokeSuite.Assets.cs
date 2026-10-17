using SlashText.Models;
using SlashText.Services;

internal static partial class SmokeSuite
{
    private static void RunAssetMaintenanceChecks()
    {
        var assetMaintenanceRoot = Path.Combine(
            Path.GetTempPath(),
            "SlashDeskAssetMaintenanceTests",
            Guid.NewGuid().ToString("N"));
        var assetMaintenanceDirectory = Path.Combine(assetMaintenanceRoot, "assets");
        Directory.CreateDirectory(assetMaintenanceDirectory);
        try
        {
            var referencedAsset = Path.Combine(assetMaintenanceDirectory, "used.png");
            var orphanAsset = Path.Combine(assetMaintenanceDirectory, "orphan.png");
            var temporaryAsset = Path.Combine(assetMaintenanceDirectory, "capture.tmp");
            File.WriteAllBytes(referencedAsset, [1, 2, 3]);
            File.WriteAllBytes(orphanAsset, [4, 5, 6, 7]);
            File.WriteAllBytes(temporaryAsset, [8]);

            var assetMaintenance = new AssetMaintenanceService(assetMaintenanceDirectory);
            var assetReport = assetMaintenance.Analyze(
            [
                new Snippet
                {
                    Format = SnippetFormat.Markdown,
                    Content = "![Uma](assets/used.png)\n![Duplicada](assets/used.png)\n" +
                              "![Externa](https://example.com/image.png)"
                }
            ]);
            Require(assetReport.ReferencedAssetCount == 1, "referências de assets sem duplicação");
            Require(assetReport.UnsafeReferenceCount == 0, "referências externas não são seguidas");
            Require(
                assetReport.Orphans.Count == 1 &&
                assetReport.Orphans[0].RelativePath == "orphan.png",
                "somente asset sem referência é classificado como órfão");
            Require(
                File.Exists(referencedAsset) && File.Exists(orphanAsset),
                "análise e cancelamento não alteram arquivos");

            var backupObservedBeforeDeletion = false;
            var backupMarker = Path.Combine(assetMaintenanceRoot, "before-cleanup.zip");
            var cleanupResult = assetMaintenance.DeleteOrphansAfterBackup(
                assetReport,
                () =>
                {
                    backupObservedBeforeDeletion = File.Exists(orphanAsset);
                    File.WriteAllText(backupMarker, "backup");
                    return backupMarker;
                });
            Require(backupObservedBeforeDeletion, "backup ocorre antes da exclusão");
            Require(cleanupResult.DeletedCount == 1, "asset órfão removido após backup");
            Require(File.Exists(referencedAsset), "asset referenciado preservado");
            Require(!File.Exists(orphanAsset), "asset órfão removido");
            Require(File.Exists(temporaryAsset), "arquivo temporário ignorado");

            var blockedAsset = Path.Combine(assetMaintenanceDirectory, "blocked.png");
            File.WriteAllBytes(blockedAsset, [9]);
            var unsafeReport = assetMaintenance.Analyze(
            [
                new Snippet
                {
                    Format = SnippetFormat.Markdown,
                    Content = "![Inválida](assets/../outside.png)"
                }
            ]);
            Require(unsafeReport.UnsafeReferenceCount == 1, "caminho local inseguro detectado");
            var unsafeBackupCalled = false;
            RequireThrows<InvalidOperationException>(
                () => assetMaintenance.DeleteOrphansAfterBackup(
                    unsafeReport,
                    () =>
                    {
                        unsafeBackupCalled = true;
                        return backupMarker;
                    }),
                "limpeza bloqueada por referência insegura");
            Require(!unsafeBackupCalled && File.Exists(blockedAsset), "bloqueio não modifica assets");
        }
        finally
        {
            Directory.Delete(assetMaintenanceRoot, recursive: true);
        }
    }
}
