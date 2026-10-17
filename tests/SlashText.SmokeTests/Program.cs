if (await SmokeCommandLine.TryRunAsync(args)) return;

await SmokeSuite.RunAsync();
