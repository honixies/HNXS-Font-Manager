using System.Windows;
using Hnxs.FontManager.Services;

namespace Hnxs.FontManager;

public partial class RenameTemplateWindow : Window
{
    public RenameTemplateWindow()
    {
        InitializeComponent();
        TemplateComboBox.ItemsSource = FileNameTemplateService.Templates;
        TemplateComboBox.SelectedIndex = 0;
    }

    public string SelectedTemplate => TemplateComboBox.SelectedItem?.ToString() ?? FileNameTemplateService.Templates[0];

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
