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
        Width = 760;
        Height = 590;
        MinWidth = 620;
        MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;
        Background = (Brush)Application.Current.FindResource("CanvasBrush");
        Foreground = (Brush)Application.Current.FindResource("InkBrush");

        var root = new Grid { Margin = new Thickness(30) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new Grid();
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        heading.ColumnDefinitions.Add(new ColumnDefinition());
        var logo = new Image
        {
            Source = new System.Windows.Media.Imaging.BitmapImage(
                new Uri("pack://application:,,,/Assets/SlashDesk.png")),
            Width = 58,
            Height = 58,
            Stretch = Stretch.Uniform
        };
        heading.Children.Add(logo);
        var headingCopy = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        headingCopy.Children.Add(new TextBlock
        {
            Text = "Bem-vindo ao SlashDesk",
            FontSize = 28,
            FontWeight = FontWeights.SemiBold
        });
        headingCopy.Children.Add(new TextBlock
        {
            Text = "Texto, acentos e capturas sem conta, upload ou nuvem.",
            Margin = new Thickness(0, 4, 0, 0),
            Foreground = (Brush)Application.Current.FindResource("MutedBrush")
        });
        Grid.SetColumn(headingCopy, 2);
        heading.Children.Add(headingCopy);
        root.Children.Add(heading);

        var features = new UniformGrid { Rows = 2, Columns = 2 };
        Grid.SetRow(features, 2);
        features.Children.Add(Card("01", "Atalhos de texto", "Digite /atalho ou :atalho e confirme para inserir textos reutilizáveis."));
        features.Children.Add(Card("02", "Acento Rápido", "Segure a tecla configurada após uma letra para escolher o caractere."));
        features.Children.Add(Card("03", "Captura local", "Capture monitor, região ou janela e marque a imagem antes de salvar."));
        features.Children.Add(Card("04", "Privacidade", "Preferências, estatísticas e capturas permanecem neste computador."));
        root.Children.Add(features);

        var footer = new Grid();
        footer.ColumnDefinitions.Add(new ColumnDefinition());
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Children.Add(new TextBlock
        {
            Text = "100% local  ·  sem conta  ·  sem upload",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.FindResource("MutedBrush")
        });
        var finish = new Button
        {
            Content = "Começar a usar",
            Style = (Style)Application.Current.FindResource("PrimaryButton"),
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 150
        };
        finish.Click += (_, _) => DialogResult = true;
        Grid.SetColumn(finish, 1);
        footer.Children.Add(finish);
        Grid.SetRow(footer, 4);
        root.Children.Add(footer);
        Content = root;
    }

    private static Border Card(string number, string title, string description)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = number,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.FindResource("AccentBrush")
        });
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 7, 0, 0)
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
