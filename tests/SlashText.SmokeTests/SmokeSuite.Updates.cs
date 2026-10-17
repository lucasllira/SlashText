using SlashText.Models;
using SlashText.Services;
using System.Diagnostics;

internal static partial class SmokeSuite
{
    private static async Task RunUpdateChecksAsync(string root)
    {
        var updateTests = Path.Combine(root, "updates");
        Directory.CreateDirectory(updateTests);
        var now = new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);
        var updateHandler = new FakeUpdateHttpHandler(ReleaseJson("3.0.0"));
        var updateService = new UpdateService(
            new HttpClient(updateHandler),
            Path.Combine(updateTests, "state.json"),
            "2.9.1",
            () => now,
            TimeSpan.FromSeconds(1));
        var updateFound = await updateService.CheckAsync(force: true);
        Require(
            updateFound.UpdateAvailable &&
            updateFound.LatestVersion == "3.0.0" &&
            updateFound.Release?.PortableAsset.Name == "SlashDesk-3.0.0-portable-win-x64.zip" &&
            updateFound.DownloadSize == 12_345,
            "release estável aplica versão e artefatos exatos");
        Require(updateHandler.Calls == 1, "consulta única ao GitHub");

        await updateService.IgnoreVersionAsync("3.0.0");
        var ignored = await updateService.CheckAsync(force: true);
        Require(ignored.Status == UpdateCheckStatus.Ignored && !ignored.UpdateAvailable,
            "versão ignorada não é oferecida novamente");
        updateHandler.ResponseJson = ReleaseJson("3.1.0");
        var afterIgnored = await updateService.CheckAsync(force: true);
        Require(afterIgnored.UpdateAvailable && afterIgnored.LatestVersion == "3.1.0",
            "versão superior volta a ser oferecida");

        await updateService.RemindLaterAsync("3.1.0");
        var cachedDeferred = await updateService.CheckAsync();
        Require(cachedDeferred.Status == UpdateCheckStatus.Cached && !cachedDeferred.UpdateAvailable,
            "cache automático respeita lembrar depois");
        now += UpdateService.AutomaticCheckInterval + TimeSpan.FromMinutes(1);
        var deferred = await updateService.CheckAsync();
        Require(deferred.Status == UpdateCheckStatus.Deferred && !deferred.UpdateAvailable,
            "verificação automática adia somente a versão atual");
        var manualAfterDeferral = await updateService.CheckAsync(force: true);
        Require(
            manualAfterDeferral.Status == UpdateCheckStatus.UpdateAvailable &&
            manualAfterDeferral.UpdateAvailable &&
            manualAfterDeferral.LatestVersion == "3.1.0",
            "busca manual volta a oferecer versão adiada");
        now += UpdateService.RemindLaterInterval + TimeSpan.FromMinutes(1);
        var afterDeferral = await updateService.CheckAsync();
        Require(afterDeferral.UpdateAvailable, "lembrar depois volta a oferecer automaticamente");

        var cacheHandler = new FakeUpdateHttpHandler(ReleaseJson("3.0.0"));
        var cacheService = new UpdateService(
            new HttpClient(cacheHandler),
            Path.Combine(updateTests, "cache-state.json"),
            "2.9.1",
            () => now,
            TimeSpan.FromSeconds(1));
        await Task.WhenAll(cacheService.CheckAsync(), cacheService.CheckAsync());
        Require(cacheHandler.Calls == 1, "pedidos simultâneos usam exclusão e cache");

        var stableFilterHandler = new FakeUpdateHttpHandler(ReleaseJson(
            "3.0.0", includeStable: false, includeDraft: true, includePrerelease: true));
        var stableFilterService = new UpdateService(
            new HttpClient(stableFilterHandler),
            Path.Combine(updateTests, "stable-filter.json"),
            "2.9.1",
            () => now,
            TimeSpan.FromSeconds(1));
        var stableFilter = await stableFilterService.CheckAsync(force: true);
        Require(stableFilter.Status == UpdateCheckStatus.NoRelease,
            "draft e prerelease são ignoradas no canal estável");

        var currentService = new UpdateService(
            new HttpClient(new FakeUpdateHttpHandler(ReleaseJson("2.9.1"))),
            Path.Combine(updateTests, "current.json"),
            "2.9.1",
            () => now,
            TimeSpan.FromSeconds(1));
        Require((await currentService.CheckAsync(force: true)).Status == UpdateCheckStatus.UpToDate,
            "nenhuma atualização quando versões coincidem");

        var offlineService = new UpdateService(
            new HttpClient(new FakeUpdateHttpHandler(new HttpRequestException("offline"))),
            Path.Combine(updateTests, "offline.json"),
            "2.9.1",
            () => now,
            TimeSpan.FromSeconds(1));
        Require((await offlineService.CheckAsync(force: true)).Status == UpdateCheckStatus.Offline,
            "ausência de internet não lança erro invasivo");

        var timeoutService = new UpdateService(
            new HttpClient(new FakeUpdateHttpHandler(TimeSpan.FromMilliseconds(200))),
            Path.Combine(updateTests, "timeout.json"),
            "2.9.1",
            () => now,
            TimeSpan.FromMilliseconds(20));
        Require((await timeoutService.CheckAsync(force: true)).Status == UpdateCheckStatus.Offline,
            "timeout de atualização é tratado");

        var notificationGate = new UpdateNotificationGate();
        Require(
            !notificationGate.TryMark(null) &&
            notificationGate.TryMark("3.2.0") &&
            !notificationGate.TryMark("3.2.0") &&
            notificationGate.TryMark("3.3.0"),
            "monitor notifica uma única vez por versão durante a sessão");
        notificationGate.Mark("3.4.0");
        Require(!notificationGate.TryMark("3.4.0"),
            "busca manual impede repetição automática da mesma versão");

        var transactionRoot = Path.Combine(root, "update-transaction");
        Directory.CreateDirectory(transactionRoot);
        var transactionData = Path.Combine(transactionRoot, "SlashDeskData");
        Directory.CreateDirectory(transactionData);
        var preservedUpdateData = new Dictionary<string, string>
        {
            ["snippets.md"] = "atalhos, categorias, hyperlinks e variáveis preservados",
            ["settings.json"] = "{\"theme\":\"Dark\",\"checkUpdatesOnStartup\":true}",
            ["usage.json"] = "{\"totalExpansions\":17}",
            ["capture-history.json"] = "[{\"id\":\"preservado-na-atualizacao\"}]"
        };
        foreach (var item in preservedUpdateData)
        {
            await File.WriteAllTextAsync(Path.Combine(transactionData, item.Key), item.Value);
        }
        var targetExecutable = Path.Combine(transactionRoot, "SlashDesk.exe");
        var stagedExecutable = Path.Combine(transactionRoot, "SlashDesk.new.exe");
        var backupExecutable = Path.Combine(transactionRoot, "SlashDesk.previous.exe");
        var failedExecutable = Path.Combine(transactionRoot, "SlashDesk.failed.exe");
        await File.WriteAllTextAsync(targetExecutable, "versão anterior");
        await File.WriteAllTextAsync(stagedExecutable, "versão nova");
        PortableUpdateFileTransaction.Apply(targetExecutable, stagedExecutable, backupExecutable);
        Require(
            await File.ReadAllTextAsync(targetExecutable) == "versão nova" &&
            await File.ReadAllTextAsync(backupExecutable) == "versão anterior" &&
            preservedUpdateData.All(item =>
                File.ReadAllText(Path.Combine(transactionData, item.Key)) == item.Value),
            "substituição troca somente o executável e preserva atalhos, histórico, configurações e estatísticas");
        PortableUpdateFileTransaction.Rollback(targetExecutable, backupExecutable, failedExecutable);
        Require(
            await File.ReadAllTextAsync(targetExecutable) == "versão anterior" &&
            preservedUpdateData.All(item =>
                File.ReadAllText(Path.Combine(transactionData, item.Key)) == item.Value),
            "rollback restaura executável sem alterar atalhos, histórico, configurações e estatísticas");

        var replacementInvoked = false;
        RequireThrows<IOException>(() => PortableUpdateFileTransaction.Apply(
            targetExecutable,
            failedExecutable,
            backupExecutable,
            (_, _, _) =>
            {
                replacementInvoked = true;
                throw new IOException("falha simulada");
            }), "falha na substituição é propagada");
        Require(replacementInvoked && preservedUpdateData.All(item =>
                File.ReadAllText(Path.Combine(transactionData, item.Key)) == item.Value),
            "falha na substituição preserva dados");

        var portableUpdateHome = Path.Combine(root, "Portátil Atualização");
        Directory.CreateDirectory(portableUpdateHome);
        var portableExecutable = Path.Combine(portableUpdateHome, "SlashDesk.exe");
        File.Copy(Environment.ProcessPath!, portableExecutable);
        AppPaths.Initialize(new AppDataEnvironment(DistributionMode.Portable, portableUpdateHome,
            Path.Combine(root, "legacy-update")));
        AppPaths.EnsureDataLayout();
        var updaterDataSentinel = Path.Combine(AppPaths.DataDirectory, "history-sentinel.txt");
        await File.WriteAllTextAsync(updaterDataSentinel, "histórico intacto");
        var updaterVersion = Version.Parse(
            FileVersionInfo.GetVersionInfo(portableExecutable).FileVersion ?? "1.0.0.0").ToString(3);
        var packageBytes = CreatePortableZip(portableExecutable);
        var packageHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(packageBytes))
            .ToLowerInvariant();
        var packageName = $"SlashDesk-{updaterVersion}-portable-win-x64.zip";
        var checksumBytes = System.Text.Encoding.ASCII.GetBytes($"{packageHash}  {packageName}");
        var packageRelease = CreatePackageRelease(updaterVersion, packageName, packageBytes.Length,
            checksumBytes.Length);
        var packageHandler = new FakePackageHttpHandler(packageBytes, checksumBytes);
        var packageService = new PortableUpdateService(
            new HttpClient(packageHandler), portableExecutable, currentProcessId: 0);
        var preparedPackage = await packageService.PrepareAsync(packageRelease);
        Require(
            File.Exists(preparedPackage.Manifest.StagedExecutable) &&
            File.Exists(preparedPackage.Manifest.HelperExecutable) &&
            await File.ReadAllTextAsync(updaterDataSentinel) == "histórico intacto",
            "pacote válido é preparado dentro de SlashDeskData sem alterar histórico");

        var badChecksum = checksumBytes.ToArray();
        badChecksum[0] = badChecksum[0] == (byte)'0' ? (byte)'1' : (byte)'0';
        var invalidChecksumService = new PortableUpdateService(
            new HttpClient(new FakePackageHttpHandler(packageBytes, badChecksum)),
            portableExecutable,
            currentProcessId: 0);
        await RequireThrowsAsync<InvalidDataException>(
            () => invalidChecksumService.PrepareAsync(packageRelease),
            "checksum inválido impede atualização");

        var incompleteRelease = CreatePackageRelease(
            updaterVersion, packageName, packageBytes.Length + 10, checksumBytes.Length);
        var incompleteService = new PortableUpdateService(
            new HttpClient(new FakePackageHttpHandler(packageBytes, checksumBytes)),
            portableExecutable,
            currentProcessId: 0);
        await RequireThrowsAsync<EndOfStreamException>(
            () => incompleteService.PrepareAsync(incompleteRelease),
            "download incompleto impede atualização");

        var concurrentHandler = new FakePackageHttpHandler(packageBytes, checksumBytes)
        {
            Delay = TimeSpan.FromMilliseconds(250)
        };
        var concurrentPackageService = new PortableUpdateService(
            new HttpClient(concurrentHandler), portableExecutable, currentProcessId: 0);
        using var concurrentCancellation = new CancellationTokenSource();
        var firstPrepare = concurrentPackageService.PrepareAsync(
            packageRelease, cancellationToken: concurrentCancellation.Token);
        await concurrentHandler.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await RequireThrowsAsync<InvalidOperationException>(
            () => concurrentPackageService.PrepareAsync(packageRelease),
            "dois pedidos simultâneos de atualização são bloqueados");
        concurrentCancellation.Cancel();
        await RequireThrowsAsync<OperationCanceledException>(
            () => firstPrepare,
            "fechamento durante download cancela preparação");
        Require(await File.ReadAllTextAsync(updaterDataSentinel) == "histórico intacto",
            "cancelamento do download preserva SlashDeskData");
    }
}
