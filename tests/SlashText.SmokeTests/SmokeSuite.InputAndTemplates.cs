using SlashText.Services;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Diagnostics;

internal static partial class SmokeSuite
{
    private static async Task RunInputAndTemplateChecksAsync(TemplateEngine engine, DateTimeOffset reference)
    {
        var rendered = engine.Render(
            "{{data}}|{{data_curta}}|{{data_extensa}}|{{hora}}|{{mes}}|{{mes_nome}}|" +
            "{{mes_curto}}|{{ano}}|{{ano_curto}}|{{dia_semana_curto}}|{{data:-7d}}|{{tab}}",
            now: reference);

        Require(rendered.StartsWith("27/07/2026|27/07/26|", StringComparison.Ordinal), "datas abreviada e extensa");
        Require(rendered.Contains("|14:35|07|", StringComparison.Ordinal), "variáveis automáticas");
        Require(rendered.Contains("|2026|26|", StringComparison.Ordinal), "ano completo e abreviado");
        Require(rendered.Contains("|20/07/2026|", StringComparison.Ordinal), "cálculo de data");
        Require(rendered.EndsWith(TemplateEngine.TabMarker, StringComparison.Ordinal), "marcador Tab");

        var nativeInputType = typeof(QuickAccentService).GetNestedType(
            "Input",
            BindingFlags.NonPublic);
        Require(nativeInputType is not null, "estrutura nativa do Acento Rápido");
        Require(
            Marshal.SizeOf(nativeInputType!) == (Environment.Is64BitProcess ? 40 : 28),
            "estrutura INPUT compatível com SendInput");
        Require(
            QuickAccentService.ShouldUseUppercase(shiftDown: false, capsLockOn: true),
            "Caps Lock mantém o acento em maiúsculo");
        Require(
            !QuickAccentService.ShouldUseUppercase(shiftDown: true, capsLockOn: true),
            "Shift inverte Caps Lock");
        Require(
            QuickAccentService.ShouldHandleInput(
                enabled: true,
                ownProcessInForeground: true,
                excludedApp: false),
            "Acento Rápido funciona em campos editáveis do próprio SlashDesk");
        Require(
            !QuickAccentService.ShouldHandleInput(
                enabled: true,
                ownProcessInForeground: false,
                excludedApp: true) &&
            !QuickAccentService.ShouldHandleInput(
                enabled: false,
                ownProcessInForeground: true,
                excludedApp: false),
            "Acento Rápido respeita estado inativo e aplicativos excluídos");
        var startupCoordinator = new StartupModuleCoordinator();
        Require(QuickAccentService.ResolveRuntimeState(false, true, true) == QuickAccentRuntimeState.Disabled &&
                QuickAccentService.ResolveRuntimeState(true, false, true) == QuickAccentRuntimeState.Unavailable &&
                QuickAccentService.ResolveRuntimeState(true, true, true) == QuickAccentRuntimeState.Paused &&
                QuickAccentService.ResolveRuntimeState(true, true, false) == QuickAccentRuntimeState.Ready,
            "status distingue preferencia, inicializacao nativa e exclusao do aplicativo");
        Require(QuickAccentService.IsProcessExcluded("GAME", "mstsc.exe; game.exe\nterminal") &&
                QuickAccentService.IsProcessExcluded("terminal", " terminal \r\n") &&
                !QuickAccentService.IsProcessExcluded("gamehelper", "game.exe") &&
                !QuickAccentService.IsProcessExcluded("game", null),
            "status e ativacao compartilham exclusoes exatas, sem diferenca de caixa ou falso positivo por prefixo");
        var healthyModuleStarted = false;
        Require(
            !await startupCoordinator.RunAsync(
                "Histórico de captura",
                () => Task.FromException(
                    new IOException("C:\\Users\\Pessoa\\dado-privado.json"))) &&
            await startupCoordinator.RunAsync(
                "Atalhos",
                () =>
                {
                    healthyModuleStarted = true;
                    return Task.CompletedTask;
                }) &&
            healthyModuleStarted,
            "falha em módulo opcional não impede iniciar módulo saudável");
        Require(
            startupCoordinator.Failures.Count == 1 &&
            startupCoordinator.Failures[0].Module == "Histórico de captura" &&
            startupCoordinator.Failures[0].ExceptionType ==
                typeof(IOException).FullName &&
            typeof(StartupModuleFailure).GetProperty("Message") is null,
            "diagnóstico de inicialização não armazena conteúdo ou mensagem privada");
        Require(
            typeof(SlashText.MainWindow).GetMethod(
                "EnsureSnippetStorageAvailable",
                BindingFlags.NonPublic | BindingFlags.Instance) is not null,
            "falha dos atalhos ativa barreira contra sobrescrita");

        var translationFlags = typeof(KeyboardHookService).GetField(
            "ToUnicodeNoStateChange",
            BindingFlags.NonPublic | BindingFlags.Static);
        Require(
            translationFlags?.GetRawConstantValue() is uint flags && flags == 0x04,
            "leitura do teclado não altera o estado de acentos mortos em layouts ABNT");
        Require(
            QuickAccentService.RemainingDelayMilliseconds(1_000, 1_000, 0) == 0 &&
            QuickAccentService.RemainingDelayMilliseconds(1_000, 1_000, 100) == 100 &&
            QuickAccentService.RemainingDelayMilliseconds(1_000, 1_080, 100) == 20 &&
            QuickAccentService.RemainingDelayMilliseconds(1_000, 1_250, 200) == 0,
            "atrasos de 0, 100 e 200 ms usam tempo monotônico real");

        using (var quickAccentDelay = new QuickAccentDelayController())
        {
            var activation = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var activationClock = Stopwatch.StartNew();
            long activatedAtTicks = 0;
            quickAccentDelay.Schedule(60, () =>
            {
                Interlocked.Exchange(ref activatedAtTicks, activationClock.Elapsed.Ticks);
                activation.TrySetResult();
            });
            await activation.Task.WaitAsync(TimeSpan.FromSeconds(2));
            // Measure the callback itself. A delayed Task.Delay observer is not evidence of early activation.
            Require(
                TimeSpan.FromTicks(Interlocked.Read(ref activatedAtTicks)) >= TimeSpan.FromMilliseconds(35),
                "Acento Rápido não abre antes do atraso, medido no callback com a tolerância existente");

            var cancelledCount = 0;
            quickAccentDelay.Schedule(80, () => Interlocked.Increment(ref cancelledCount));
            quickAccentDelay.Cancel();
            await Task.Delay(120);
            Require(cancelledCount == 0, "soltar a combinação cancela a abertura pendente");

            var deduplicatedCount = 0;
            var latestActivation = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            quickAccentDelay.Schedule(
                80,
                () => Interlocked.Increment(ref deduplicatedCount));
            quickAccentDelay.Schedule(
                20,
                () =>
                {
                    Interlocked.Increment(ref deduplicatedCount);
                    latestActivation.TrySetResult();
                });
            await latestActivation.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await Task.Delay(100);
            Require(
                deduplicatedCount == 1,
                "reagendamento não produz abertura dupla");
        }

        using (var previewService = new QuickAccentService())
        {
            var previewEvents = 0;
            previewService.Changed += (_, _) => previewEvents++;
            previewService.CharacterInserted += (_, _) => previewEvents++;
            Require(previewService.GetPreviewChoices('a') == "áàâã" && previewService.GetPreviewChoices('A') == "ÁÀÂÃ",
                "prévia usa escolhas reais e distingue maiúsculas sem consultar o teclado físico");
            previewService.SetUsage(new Dictionary<char, long> { ['ã'] = 10, ['à'] = 5 });
            Require(previewService.GetPreviewChoices('a') == "ãàáâ", "prévia usa a prioridade real de uso e conserva empates");
            previewService.SortByUsage = false;
            Require(previewService.GetPreviewChoices('a') == "áàâã", "desativar prioridade restaura a ordem dos conjuntos");
            previewService.SetCharacterSets(["Currency", "Special"]);
            Require(previewService.GetPreviewChoices('e') == "€" && previewService.GetPreviewChoices('c') == "¢©" && previewService.GetPreviewChoices('a') == "",
                "prévia respeita moedas, símbolos e letras sem opções");
            previewService.SetCharacterSets([]);
            Require(previewService.GetPreviewChoices('a') == "áàâã" && previewEvents == 0 && !previewService.IsRunning,
                "consultar a prévia não instala hook, dispara eventos nem altera a digitação");
        }

        var portugueseCharacters = QuickAccentService.PreviewCharacters(["PortugueseBrazil"]);
        Require(
            portugueseCharacters.Contains('ã') && !portugueseCharacters.Contains('ä'),
            "conjunto somente PT-BR");
        Require(
            QuickAccentService.PreviewCharacters(["PortugueseBrazil", "German", "Currency"])
                .Contains('€'),
            "combinação de conjuntos do Acento Rápido");

        var fields = engine.GetFillableFields("Olá {{nome}}, chamado {{chamado|INC000}}. {{nome}}");
        Require(fields.Count == 2, "campos únicos");
        Require(fields[1].DefaultValue == "INC000", "valor padrão");

        Require(TriggerRule.TryValidate("/teste", out _), "regra única aceita prefixo barra");
        Require(TriggerRule.TryValidate("/ação_2-rapida", out _), "regra única aceita letras Unicode, números, hífen e sublinhado");
        Require(!TriggerRule.TryValidate(":ação_2-rapida", out _), "regra única rejeita prefixo dois-pontos");
        Require(!TriggerRule.TryValidate("teste", out _), "regra única rejeita gatilho sem prefixo");
        Require(!TriggerRule.TryValidate("/inválido!", out _), "regra única rejeita caractere impossível para o monitor");
        Require(
            !KeyboardHookService.TryNormalizeTranslatedCharacter('?', out _),
            "interrogação não é interpretada como prefixo de atalho");
        Require(
            KeyboardHookService.TryNormalizeTranslatedCharacter('/', out var slashPrefix) &&
            slashPrefix == '/',
            "barra continua sendo aceita como prefixo");
        Require(
            !KeyboardHookService.TryNormalizeTranslatedCharacter(':', out _),
            "dois-pontos não é interpretado como prefixo de atalho");
        Require(
            KeyboardHookService.TryNormalizeTranslatedCharacter('A', out var normalizedLetter) &&
            normalizedLetter == 'a',
            "letras traduzidas continuam normalizadas");

        var modifierState = new KeyboardModifierState();
        modifierState.Update(0xA0, isDown: true);
        var shiftKeyboardState = new byte[256];
        modifierState.ApplyTo(shiftKeyboardState);
        Require(
            (shiftKeyboardState[0x10] & 0x80) != 0 &&
            (shiftKeyboardState[0xA0] & 0x80) != 0 &&
            (shiftKeyboardState[0xA1] & 0x80) == 0,
            "Shift esquerdo observado pelo hook é aplicado à tradução");
        modifierState.Update(0xA0, isDown: false);
        modifierState.Update(0xA2, isDown: true);
        modifierState.Update(0xA5, isDown: true);
        var altGrKeyboardState = new byte[256];
        modifierState.ApplyTo(altGrKeyboardState);
        Require(
            (altGrKeyboardState[0x11] & 0x80) != 0 &&
            (altGrKeyboardState[0x12] & 0x80) != 0 &&
            (altGrKeyboardState[0xA2] & 0x80) != 0 &&
            (altGrKeyboardState[0xA5] & 0x80) != 0,
            "AltGr mantém Control e Alt direito no estado traduzido");
        modifierState.Update(0xA2, isDown: false);
        modifierState.Update(0xA5, isDown: false);
        var releasedKeyboardState = new byte[256];
        modifierState.ApplyTo(releasedKeyboardState);
        Require(
            releasedKeyboardState.All(value => value == 0),
            "soltar modificadores limpa o estado acompanhado pelo hook");

        var maximumTrigger = "/" + new string('a', TriggerRule.MaximumLength - 1);
        Require(TriggerRule.TryValidate(maximumTrigger, out _), "gatilho no tamanho máximo");
        Require(!TriggerRule.TryValidate(maximumTrigger + "a", out _), "gatilho acima do tamanho máximo");
        Require(TriggerRule.ConflictsWith("/TESTE", ["/teste"]), "conflito ignora maiúsculas e minúsculas");
        Require(TriggerRule.IsPrefixOfAnother("/at", ["/atendimento"]), "gatilhos com prefixos semelhantes");

        var bufferState = new KeyboardBufferState();
        bufferState.Append('/', new IntPtr(10), new IntPtr(11));
        bufferState.Append('a', new IntPtr(10), new IntPtr(11));
        bufferState.Append('t', new IntPtr(10), new IntPtr(11));
        Require(bufferState.Text == "/at", "buffer mantém sugestão parcial");
        Require(bufferState.TargetChanged(new IntPtr(12), new IntPtr(11)), "troca de janela detectada no buffer");
        bufferState.Clear(BufferResetReason.MouseClick);
        Require(!bufferState.HasValue && bufferState.LastResetReason == BufferResetReason.MouseClick, "clique limpa buffer");
        bufferState.Append(':', new IntPtr(20), new IntPtr(21));
        Require(!bufferState.HasValue, "dois-pontos não inicia o buffer");
        bufferState.Append('/', new IntPtr(20), new IntPtr(21));
        bufferState.Append('x', new IntPtr(20), new IntPtr(21));
        Require(bufferState.TargetChanged(new IntPtr(20), new IntPtr(22)), "troca de controle focado limpa buffer");

        var planOneTab = ExpansionPlan.Create("Primeiro campo" + TemplateEngine.TabMarker + "Segundo campo");
        Require(planOneTab.Count == 2 && planOneTab[0].SendTabAfter && !planOneTab[1].SendTabAfter, "sequência com um Tab");
        var planManyTabs = ExpansionPlan.Create(
            "Campo 1" + TemplateEngine.TabMarker + "Campo 2" + TemplateEngine.TabMarker + "Campo 3");
        Require(planManyTabs.Count == 3 && planManyTabs.Count(step => step.SendTabAfter) == 2, "sequência com vários Tabs");
        Require(Enum.IsDefined(SuggestionConfirmation.Enter) &&
                Enum.IsDefined(SuggestionConfirmation.Tab) &&
                Enum.IsDefined(SuggestionConfirmation.Space) &&
                Enum.IsDefined(SuggestionConfirmation.Click), "formas de confirmação da sugestão");

        var expansionGate = new SingleFlightGate();
        using (var firstExpansion = expansionGate.TryEnter())
        {
            Require(firstExpansion is not null && expansionGate.IsActive, "primeira expansão entra no single-flight");
            Require(expansionGate.TryEnter() is null, "segunda expansão simultânea é ignorada");
        }
        using var afterExpansion = expansionGate.TryEnter();
        Require(!expansionGate.IsActive || afterExpansion is not null, "trava de expansão liberada");
        var captureGate = new SingleFlightGate();
        try
        {
            using var captureLease = captureGate.TryEnter();
            Require(captureGate.TryEnter() is null, "apenas uma captura simultânea");
            throw new InvalidOperationException("falha simulada");
        }
        catch (InvalidOperationException)
        {
            // A liberação ocorre pelo finally implícito de using.
        }
        Require(!captureGate.IsActive, "trava de captura liberada após exceção");

        var debounce = new DebounceGate(TimeSpan.FromMilliseconds(300));
        Require(debounce.TryAccept(1_000_000), "primeiro evento do debounce");
        Require(!debounce.TryAccept(1_000_001), "evento repetido é bloqueado pelo debounce");
        Require(
            (GlobalCaptureShortcutService.ApplyNoRepeat(2) & GlobalCaptureShortcutService.ModNoRepeat) != 0,
            "MOD_NOREPEAT aplicado a hotkeys");
    }
}
