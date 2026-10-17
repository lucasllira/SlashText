using SlashText.Services;

internal static class SmokeCommandLine
{
    internal static async Task<bool> TryRunAsync(string[] args)
    {
        if (args.Length == 2 && args[0] == "--recording-output-path-smoke")
        {
            await RecordingOutputPathChecks.RunAsync(Path.GetFullPath(args[1]));
            return true;
        }

        if (args.Length == 2 && args[0] == "--recording-stop-smoke")
        {
            await RecordingStopSafetyChecks.RunAsync(Path.GetFullPath(args[1]));
            return true;
        }

        if (args.Length == 3 && args[0] == "--recording-recovery-probe")
        {
            await RecordingStopSafetyChecks.ProbeAsync(Path.GetFullPath(args[1]), args[2]);
            return true;
        }

        if (args.Length == 1 && args[0] == "--release-trust-smoke")
        {
            await ReleaseTrustChecks.RunAsync();
            return true;
        }
        if (args.Length == 2 && args[0] == "--recording-audio-migration-smoke")
        {
            RecordingAudioMigrationChecks.Run(Path.GetFullPath(args[1]));
            return true;
        }
        if (args.Length >= 3 && args[0] == "--capture-ocr-worker")
        {
            if (Environment.GetEnvironmentVariable("SLASHDESK_TEST_OCR_MODE") == "wait")
            {
                using var lease = TemporarySession.AttachWorker(args[1], args[2]);
                if (!Path.GetFullPath(Environment.CurrentDirectory).Equals(Path.GetDirectoryName(Path.GetFullPath(args[1])), StringComparison.OrdinalIgnoreCase)) Environment.Exit(8);
                File.WriteAllText(args[2], "inert worker ready");
                await Task.Delay(TimeSpan.FromMinutes(1));
                Environment.Exit(9);
            }
            Environment.Exit(CaptureOcrService.RunWorker(args));
            return true;
        }
        if (args.Length is 2 or 3 && args[0] == "--ocr-native-trust-smoke")
        {
            await OcrTrustChecks.RunAsync(Path.GetFullPath(args[1]), native: true,
                baseline: args.Length == 3 ? Path.GetFullPath(args[2]) : null);
            return true;
        }
        if (args.Length == 2 && args[0] == "--ocr-trust-smoke")
        {
            await OcrTrustChecks.RunAsync(Path.GetFullPath(args[1]), native: false);
            return true;
        }

        if (args.Length == 2 && args[0] == "--capture-stamp-path-smoke")
        {
            var output = Path.GetFullPath(args[1]);
            Directory.CreateDirectory(output);
            try
            {
                CaptureStampLongPathChecks.Run(output);
                File.WriteAllText(Path.Combine(output, "result.json"), "{\"status\":\"PASS\"}");
            }
            catch (Exception exception)
            {
                File.WriteAllText(Path.Combine(output, "result.json"), System.Text.Json.JsonSerializer.Serialize(new
                { status = "FAIL", exceptionType = exception.GetType().Name, exception.HResult }));
                Console.Error.WriteLine(exception);
                Environment.ExitCode = 1;
            }
            return true;
        }

        if (args.Length == 2 && args[0] == "--snippet-batch-smoke")
        {
            await SnippetBatchChecks.RunAsync(args[1]); return true;
        }
        if (args.Length == 2 && args[0] == "--snippet-batch-benchmark")
        {
            await SnippetBatchBenchmark.RunAsync(args[1]); return true;
        }

        if (args.Length == 2 && args[0] == "--gif-timeline-smoke")
        {
            await GifTimelineChecks.RunAsync(args[1]); return true;
        }
        if (args.Length == 2 && args[0] == "--gif-cancellation-smoke")
        {
            await GifCancellationChecks.RunAsync(args[1]); return true;
        }
        if (args.Length == 2 && args[0] == "--recording-operation-smoke")
        {
            await RecordingOperationChecks.RunAsync(args[1]); return true;
        }
        if (args.Length == 2 && args[0] == "--recording-operation-fixtures")
        {
            SlashText.Design.RecordingOperationSmoke.CreateFixtures(args[1]); return true;
        }
        if (args.Length == 2 && args[0] == "--usage-write-fixtures")
        {
            SlashText.Design.UsageWriteFailureSmoke.CreateFixtures(args[1]); return true;
        }
        if (args.Length == 2 && args[0] == "--usage-write-smoke")
        {
            await UsageWriteFailureChecks.RunAsync(args[1]); return true;
        }
        if (args.Length == 2 && args[0] == "--legacy-backups-fixtures")
        {
            SlashText.Design.LegacyBackupSmoke.CreateFixtures(args[1]); return true;
        }
        if (args.Length == 2 && args[0] == "--legacy-backups-smoke")
        {
            await LegacyBackupChecks.RunAsync(args[1]); return true;
        }
        if (args.Length == 2 && args[0] == "--protected-storage-fixtures")
        {
            SlashText.Design.ProtectedStorageSmoke.CreateFixtures(args[1]); return true;
        }
        if (args.Length == 2 && args[0] == "--protected-storage-smoke")
        {
            await ProtectedStorageChecks.RunAsync(args[1]); return true;
        }
        if (args.Length == 2 && args[0] == "--backup-compatibility-smoke")
        {
            await BackupCompatibilityChecks.RunAsync(args[1]); return true;
        }
        if (args.Length == 2 && args[0] == "--backup-compatibility-fixtures")
        {
            BackupCompatibilityChecks.CreatePilotFixtures(args[1]); return true;
        }
        if (args.Length == 4 && args[0] == "--restore-recovery-worker")
        {
            RestoreRecoveryChecks.Worker(args);
            return true;
        }
        if (args.Length == 2 && args[0] == "--restore-recovery-smoke")
        {
            Directory.CreateDirectory(args[1]);
            await RestoreRecoveryChecks.RunAsync(args[1]);
            return true;
        }
        if (args.Length == 2 && args[0] == "--retention-worker")
        {
            RetentionPrivacyChecks.Worker(args[1]);
            return true;
        }
        if (args.Length == 2 && args[0] == "--retention-smoke")
        {
            Directory.CreateDirectory(args[1]);
            await RetentionPrivacyChecks.RunAsync(args[1]);
            return true;
        }

        if (args.Length == 1 && args[0] == "--clipboard-payload-smoke")
        {
            var clipboardRoot = Path.Combine(Path.GetTempPath(), "slashdesk-clipboard-payload-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(clipboardRoot);
            ClipboardPrivacyChecks.Run(clipboardRoot);
            return true;
        }
        if (args.Length == 2 && args[0] == "--clipboard-native-worker")
        {
            Environment.Exit(ClipboardNativeChecks.RunWorker(args[1]));
            return true;
        }
        if (args.Length == 2 && args[0] == "--clipboard-native-isolated")
        {
            Environment.Exit(ClipboardNativeChecks.RunIsolated(args[1]));
            return true;
        }
        return false;
    }
}
