using OperatorTunnel.Audit;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace OperatorTunnel.App;

public partial class AuditEvidenceWindow : Window
{
    private readonly IAuditEvidenceStore _store;
    private readonly AuditSession _session;

    public AuditEvidenceWindow(IAuditEvidenceStore store, AuditSession session)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        InitializeComponent();
        Loaded += AuditEvidenceWindow_Loaded;
    }

    private async void AuditEvidenceWindow_Loaded(object sender, RoutedEventArgs e)
    {
        SessionLabel.Text = $"// SESSION {_session.Id[..8].ToUpperInvariant()}";
        try
        {
            EvidenceList.ItemsSource = await _store.ListBySessionAsync(_session.Id);
        }
        catch (IOException)
        {
            MetadataText.Text = "evidence store unavailable";
        }
    }

    private void EvidenceList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (EvidenceList.SelectedItem is not AuditEvidence evidence)
            return;

        ContentTextBox.Text = evidence.Content;
        MetadataText.Text = $"{evidence.Id} // {evidence.Source} // {evidence.FileName}\nSHA-256: {evidence.ContentHash}";
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
