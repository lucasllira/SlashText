using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace SlashText.Views;

public sealed class OnboardingWindow : Window
{
    public OnboardingWindow()
    {
        Title = "Bem-vindo ao SlashDesk";
        Width = 720;
        Height = 560;
        MinWidth = 620;
        MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;
        Background = (Brush)Application.Current.FindResource("CanvasBrush");
        Foreground = (Brush)Application.Current.FindResource("InkBrush");

        var root = new Grid { Margin = new Thickness(34) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new StackPanel();
        heading.Children.Add(new TextBlock
        {
            Text = "Seu kit local de produtividade",
            FontSize = 28,
            FontWeight = FontWeights.SemiBold
        });
        heading.Children.Add(new TextBlock
        {
            Text = "Texto, acentos e capturas sem conta, upload ou nuvem.",
            Margin = new Thickness(0, 7, 0, 0),
            Foreground = (Brush)Application.Current.FindResource("MutedBrush")
        });
        root.Children.Add(heading);

        var features = new UniformGrid { Rows = 2, Columns = 2 };
        Grid.SetRow(features, 2);
        features.Children.Add(Card("Atalhos de texto", "Digite /atalho ou :atalho e confirme para inserir textos reutilizáveis."));
        features.Children.Add(Card("Acento Rápido", "Segure a tecla configurada após uma letra para escolher o caractere."));
        features.Children.Add(Card("Captura local", "Capture monitor, região ou janela com atalhos de teclado ou roda do mouse."));
        features.Children.Add(Card("Seus dados", "Preferências, estatísticas e capturas permanecem neste computador."));
        root.Children.Add(features);

        var finish = new Button
        {
            Content = "Começar a usar",
            Style = (Style)Application.Current.FindResource("PrimaryButton"),
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 150
        };
        finish.Click += (_, _) => DialogResult = true;
        Grid.SetRow(finish, 4);
        root.Children.Add(finish);
        Content = root;
    }

    private static Border Card(string title, string description)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 17,
            FontWeight = FontWeights.SemiBold
        });
        panel.Children.Add(new TextBlock
        {
            Text = description,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 7, 0, 0),
            Foreground = (Brush)Application.Current.FindResource("MutedBrush")
        });
        return new Border
        {
            Style = (Style)Application.Current.FindResource("SettingsCard"),
            Margin = new Thickness(0, 0, 14, 14),
            Child = panel
        };
    }
}
