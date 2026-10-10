namespace SlashText.Views;

public static class QuickAccentHelpContent
{
    public static ScreenHelpDefinition Create() => new("Como usar Acento Rápido", "Caracteres especiais com as suas preferências — guia local.",
    [
        new("activate", "COMEÇAR", "Ativar e digitar", "Keyboard", "Use os caracteres nos aplicativos onde você digita.",
            ["Ative o Acento Rápido no topo e escolha Espaço, seta esquerda ou seta direita.", "Pronto para usar confirma que o recurso foi iniciado. Pausado neste aplicativo indica uma exclusão; Indisponível indica falha na inicialização.", "Segure uma letra e toque na tecla de ativação. Cada novo toque avança uma opção; solte a letra para inserir. Esc cancela."],
            "Caps Lock e Shift determinam maiúsculas. Desligar preserva suas preferências. Se estiver indisponível, desligue e ative novamente para tentar iniciar.", "QuickAccentEnabledCheckBox", Demo: "accent"),
        new("preview", "EXPERIMENTAR", "Prévia e campo de teste", "Type", "Confira os caracteres dos conjuntos selecionados.",
            ["Escolha a letra no topo da prévia.", "Clique em um caractere ou use Tab e Espaço para inseri-lo no campo de teste.", "Limpar teste apaga somente o texto desse campo."],
            "Os cliques da prévia são locais e não registram uso global. O campo também permite testar a ativação real quando o piloto estiver aberto.", "QuickAccentPreviewLetterBox", Demo: "accent-preview"),
        new("preferences", "PERSONALIZAR", "Atraso e posição", "Clock3", "Adapte o painel ao seu ritmo.",
            ["Escolha 100, 200 ou 500 ms, ou informe um valor de 0 a 2.000 milissegundos. Enter confirma o número.", "Abra Aparência e ordem dos caracteres para escolher a posição do painel.", "Mostrar código Unicode identifica o caractere; priorizar mais usados usa o histórico de digitação real."],
            "As preferências são salvas automaticamente. Valores inválidos mostram um aviso e preservam o último atraso válido.", "QuickAccentDelayBox", Demo: "accent-preferences"),
        new("sets", "PERSONALIZAR", "Conjuntos de caracteres", "Languages", "Selecione idiomas, moedas e símbolos.",
            ["Abra Conjuntos de caracteres e use Somente PT-BR ou Selecionar todos para os nove conjuntos.", "Marque os idiomas, moedas e símbolos que usa.", "A prévia é atualizada com as opções reais para a letra escolhida."],
            "Ao desmarcar o último conjunto, Português (Brasil) permanece selecionado. Letras sem opções mostram uma mensagem.", "QuickAccentSetsPanel", Demo: "accent-sets"),
        new("panel", "PERSONALIZAR", "Aparência e ordem dos caracteres", "SlidersHorizontal", "Posição, Unicode e prioridade de uso.",
            ["Abra Aparência e ordem dos caracteres para ajustar o painel.", "Escolha centro superior, centro da tela ou centro inferior.", "Ative o código Unicode ou priorize os caracteres mais usados conforme sua preferência."],
            "Mostrar na tela abre automaticamente a seção recolhida.", "QuickAccentPositionBox", Demo: "accent-preferences"),
        new("exclude", "PERSONALIZAR", "Aplicativos excluídos", "AppWindow", "Evite a ativação em aplicativos específicos.",
            ["Abra Aplicativos excluídos e informe os nomes dos processos, como mstsc.exe ou game.exe.", "Separe os nomes por ponto e vírgula ou por linha.", "Saia do campo para salvar a lista."],
            "Use o nome do processo, sem o caminho da pasta. As preferências são preservadas ao reiniciar o piloto.", "QuickAccentExcludedAppsBox", Demo: "accent-exclude")
    ]);
}
