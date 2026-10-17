using SlashText.Services;

internal static partial class SmokeSuite
{
    internal static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), $"slashtext-smoke-{Guid.NewGuid():N}");
        await ReleaseTrustChecks.RunAsync();
        AppPaths.Initialize(new AppDataEnvironment(DistributionMode.Portable, Path.Combine(root, "initial-app"), root, isCapturePilot: true));
        await OcrTrustChecks.RunAsync(Path.Combine(root, "ocr-trust"), native: false);
        await SnippetBatchChecks.RunAsync(Path.Combine(root, "snippet-batch"));
        await RecordingOperationChecks.RunAsync(Path.Combine(root, "recording-operation"));
        await RecordingOutputPathChecks.RunAsync(Path.Combine(root, "recording-paths"));
        await GifCancellationChecks.RunAsync(Path.Combine(root, "gif-cancellation"));
        await UsageWriteFailureChecks.RunAsync(Path.Combine(root, "usage-writes"));

        await ProtectedStorageChecks.RunAsync(Path.Combine(root, "protected-storage"));
        AppPaths.Initialize(new AppDataEnvironment(DistributionMode.Portable, Path.Combine(root, "initial-app"), root, isCapturePilot: true));
        await LegacyBackupChecks.RunAsync(Path.Combine(root, "legacy-backups"));
        var engine = new TemplateEngine();
        var reference = new DateTimeOffset(2026, 7, 27, 14, 35, 0, TimeSpan.FromHours(-3));
        await RunInputAndTemplateChecksAsync(engine, reference);
        RunLayoutChecks();

        var snippetsFile = Path.Combine(root, "snippets.md");
        var backups = Path.Combine(root, "backups");
        try
        {
            Directory.CreateDirectory(root);
            await CaptureHistoryTrustChecks.RunAsync(root);
            await RestoreRecoveryChecks.RunAsync(root);
            await BackupCompatibilityChecks.RunAsync(root);
            await ExternalInputBudgetChecks.RunAsync(root);
            await RetentionPrivacyChecks.RunAsync(root);
            ClipboardPrivacyChecks.Run(root);
            await ClipboardNativeChecks.RunChildAsync(root);
            await RecordingStopSafetyChecks.RunAsync(root);
            await RunCaptureEditorChecksAsync(root);
            await RunRepositoryAndBackupChecksAsync(root, snippetsFile, backups);
            await RunShortcutFormattingChecksAsync(root, engine, reference);
            await RunCaptureSettingsChecksAsync(root);
            await RunRecordingChecksAsync(root);
            await RunGifPipelineChecksAsync(root);
            await RunMigrationChecksAsync(root);
            await RunRenderingChecksAsync(root);
            await RunAtomicStorageChecksAsync(root);
            await RunUpdateChecksAsync(root);
            await RunUnconfirmedStopChecksAsync(root);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        RunAssetMaintenanceChecks();
        Console.WriteLine("SlashText smoke tests: OK");
    }
}
