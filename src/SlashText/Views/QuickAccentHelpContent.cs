namespace SlashText.Views;

public static class QuickAccentHelpContent
{
    public static ScreenHelpDefinition Create() => new("Como usar Acento Rápido", "Caracteres especiais com as suas preferências — guia local.",
    [
        new("activate", "COMEÇAR", "Ativar e digitar", "Keyboard", "Use os caracteres nos aplicativos onde você digita.",
            ["Ative o Acento Rápido e escolha Espaço, seta esquerda ou seta direita.", "Segure uma letra e toque na tecla de ativação. Mantenha as teclas pressionadas até o atraso configurado para abrir o painel.", "Cada novo toque avança uma opção. Solte a letra para inserir; Esc cancela."],
            "Caps Lock e Shift determinam maiúsculas. Um toque mais curto que o atraso mantém a digitação normal.", "QuickAccentEnabledCheckBox", Demo: "accent"),
        new("preview", "EXPERIMENTAR", "Prévia e campo de teste", "Type", "Confira os caracteres dos conjuntos selecionados.",
            ["Escolha a letra no topo da prévia.", "Clique em um caractere ou use Tab e Espaço para inseri-lo no campo de teste.", "Limpar teste apaga somente o texto desse campo."],
            "Os cliques da prévia são locais e não registram uso global. O campo também permite testar a ativação real quando o piloto estiver aberto.", "QuickAccentPreviewLetterBox", Demo: "accent-preview"),
        new("preferences", "PERSONALIZAR", "Atraso e posição", "Clock3", "Adapte o painel ao seu ritmo.",
            ["Ajuste o atraso de 0 a 2.000 milissegundos pelo controle ou campo; Enter confirma o número.", "Escolha centro superior, centro da tela ou centro inferior.", "Mostrar código Unicode identifica o caractere; priorizar mais usados usa o histórico de digitação real."],
            "As preferências são salvas automaticamente. Valores inválidos mostram um aviso e preservam o último atraso válido.", "QuickAccentDelayBox", Demo: "accent-preferences"),
        new("sets", "PERSONALIZAR", "Conjuntos de caracteres", "Languages", "Selecione idiomas, moedas e símbolos.",
            ["Use Somente PT-BR para a acentuação brasileira ou Todos para os nove conjuntos.", "Ative ou desative cada conjunto individualmente.", "A prévia é atualizada com as opções reais para a letra escolhida."],
            "Ao desmarcar o último conjunto, Português (Brasil) permanece selecionado. Letras sem opções mostram uma mensagem.", "QuickAccentSetsPanel", Demo: "accent-sets"),
        new("exclude", "PERSONALIZAR", "Aplicativos excluídos", "AppWindow", "Evite a ativação em aplicativos específicos.",
            ["Informe os nomes dos processos, como mstsc.exe ou game.exe.", "Separe os nomes por ponto e vírgula ou por linha.", "Saia do campo para salvar a lista."],
            "Use o nome do processo, sem o caminho da pasta. As preferências são preservadas ao reiniciar o piloto.", "QuickAccentExcludedAppsBox", Demo: "accent-exclude")
    ]);
}
