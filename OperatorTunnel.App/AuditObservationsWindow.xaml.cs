using OperatorTunnel.Audit;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace OperatorTunnel.App;

public partial class AuditObservationsWindow : Window
{
    private readonly IAuditObservationStore _store;
    private readonly IAuditEvidenceStore _evidenceStore;
    private readonly AuditSession _session;

    public AuditObservationsWindow(IAuditObservationStore store, IAuditEvidenceStore evidenceStore, AuditSession session)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _evidenceStore = evidenceStore ?? throw new ArgumentNullException(nameof(evidenceStore));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        InitializeComponent();
        Loaded += AuditObservationsWindow_Loaded;
    }

    private async void AuditObservationsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var observations = await _store.ListBySessionAsync(_session.Id);
            var inventory = AuditInventorySnapshot.FromObservations(observations);
            ObservationsList.ItemsSource = observations;
            SessionLabel.Text = $"// SESSION {_session.Id[..8].ToUpperInvariant()}";
            StatusText.Text = $"{observations.Count} observations // {inventory.HostCount} hosts // {inventory.PortCount} ports // {inventory.ServiceCount} services // provenance preserved";
        }
        catch (IOException)
        {
            StatusText.Text = "observation store unavailable";
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private async void EvidenceButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new AuditEvidenceWindow(_evidenceStore, _session) { Owner = this };
        window.ShowDialog();
        await Task.CompletedTask;
    }
}
