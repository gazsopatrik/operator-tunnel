using OperatorTunnel.Audit;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace OperatorTunnel.App;

public partial class AuditFindingsWindow : Window
{
    private readonly IAuditFindingStore _store;
    private readonly AuditSession _session;
    private IReadOnlyList<AuditFinding> _findings = [];
    private AuditFinding? _selectedFinding;

    public AuditFindingsWindow(IAuditFindingStore store, AuditSession session)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        InitializeComponent();
        SeverityComboBox.ItemsSource = Enum.GetValues<FindingSeverity>();
        SeverityComboBox.SelectedItem = FindingSeverity.Medium;
        Loaded += AuditFindingsWindow_Loaded;
    }

    private async void AuditFindingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        SessionLabel.Text = $"// SESSION {_session.Id[..8].ToUpperInvariant()}";
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        try
        {
            _findings = await _store.ListBySessionAsync(_session.Id);
            FindingsList.ItemsSource = _findings;
            var potential = _findings.Count(item => item.Status == FindingStatus.PotentialExposure);
            var verification = _findings.Count(item => item.Status == FindingStatus.VerificationRequired);
            var verified = _findings.Count(item => item.Status == FindingStatus.Verified);
            var notAffected = _findings.Count(item => item.Status == FindingStatus.NotAffected);
            var falsePositive = _findings.Count(item => item.Status == FindingStatus.FalsePositive);
            StatusText.Text = $"{_findings.Count} findings // potential {potential} // verify {verification} // verified {verified} // not affected {notAffected} // false positive {falsePositive}";
        }
        catch (IOException)
        {
            StatusText.Text = "finding store unavailable";
        }
    }

    private void FindingsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (FindingsList.SelectedItem is not AuditFinding finding)
            return;
        _selectedFinding = finding;
        TitleTextBox.Text = finding.Title;
        AssetTextBox.Text = finding.AffectedAsset;
        EvidenceTextBox.Text = finding.EvidenceIds.FirstOrDefault() ?? string.Empty;
        NotesTextBox.Text = finding.VerificationNotes ?? finding.Description;
        SeverityComboBox.SelectedItem = finding.Severity;
        StatusText.Text = $"{finding.Status} // {finding.Title}";
    }

    private async void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var finding = AuditFinding.CreatePotentialExposure(
                _session.Id,
                TitleTextBox.Text,
                (FindingSeverity)(SeverityComboBox.SelectedItem ?? FindingSeverity.Medium),
                AssetTextBox.Text,
                NotesTextBox.Text,
                [EvidenceTextBox.Text]);
            await _store.SaveAsync(finding);
            _selectedFinding = finding;
            await ReloadAsync();
            StatusText.Text = "potential exposure created // verification required";
        }
        catch (ArgumentException ex)
        {
            StatusText.Text = $"validation failed // {ex.Message}";
        }
    }

    private async void RequireVerificationButton_Click(object sender, RoutedEventArgs e)
    {
        if (!await SaveTransitionAsync(finding => finding.RequireVerification(), "verification required"))
            return;
    }

    private async void VerifyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!await SaveTransitionAsync(finding => finding.Verify(NotesTextBox.Text), "finding verified"))
            return;
    }

    private async void NotAffectedButton_Click(object sender, RoutedEventArgs e)
    {
        if (!await SaveTransitionAsync(finding => finding.MarkNotAffected(NotesTextBox.Text), "marked not affected"))
            return;
    }

    private async void FalsePositiveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!await SaveTransitionAsync(finding => finding.MarkFalsePositive(NotesTextBox.Text), "marked false positive"))
            return;
    }

    private async Task<bool> SaveTransitionAsync(Func<AuditFinding, AuditFinding> transition, string message)
    {
        if (_selectedFinding is null)
        {
            StatusText.Text = "select a finding first";
            return false;
        }

        try
        {
            _selectedFinding = transition(_selectedFinding);
            await _store.SaveAsync(_selectedFinding);
            await ReloadAsync();
            StatusText.Text = message;
            return true;
        }
        catch (ArgumentException ex)
        {
            StatusText.Text = $"validation failed // {ex.Message}";
            return false;
        }
        catch (InvalidOperationException ex)
        {
            StatusText.Text = $"transition blocked // {ex.Message}";
            return false;
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
