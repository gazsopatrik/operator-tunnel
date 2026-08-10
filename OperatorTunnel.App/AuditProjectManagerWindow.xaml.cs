using OperatorTunnel.Audit;
using Microsoft.Win32;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using System.IO;
using System.Windows;
using System.Windows.Input;
using WpfButton = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;

namespace OperatorTunnel.App;

public partial class AuditProjectManagerWindow : Window
{
    private readonly IAuditProjectStore _store;
    private readonly IAuditSessionStore _sessionStore;
    private readonly IAuditObservationStore _observationStore;
    private readonly IAuditEvidenceStore _evidenceStore;
    private readonly IAuditFindingStore _findingStore;
    private readonly Action<AuditProject>? _projectActivated;
    private readonly Action<AuditSession?>? _sessionChanged;
    private IReadOnlyList<AuditProject> _projects = [];
    private AuditProject? _selectedProject;

    public AuditProjectManagerWindow(
        IAuditProjectStore store,
        IAuditSessionStore sessionStore,
        IAuditObservationStore observationStore,
        IAuditEvidenceStore evidenceStore,
        IAuditFindingStore findingStore,
        Action<AuditProject>? projectActivated = null,
        Action<AuditSession?>? sessionChanged = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _observationStore = observationStore ?? throw new ArgumentNullException(nameof(observationStore));
        _evidenceStore = evidenceStore ?? throw new ArgumentNullException(nameof(evidenceStore));
        _findingStore = findingStore ?? throw new ArgumentNullException(nameof(findingStore));
        _projectActivated = projectActivated;
        _sessionChanged = sessionChanged;
        InitializeComponent();
        Loaded += AuditProjectManagerWindow_Loaded;
    }

    private async void AuditProjectManagerWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        try
        {
            _projects = await _store.ListAsync();
            ProjectsList.ItemsSource = _projects;
            ProjectsList.DisplayMemberPath = nameof(AuditProject.Name);
            if (_selectedProject is not null)
                ProjectsList.SelectedItem = _projects.FirstOrDefault(project => project.Id == _selectedProject.Id);

            StatusText.Text = _projects.Count == 0
                ? "registry empty // create the first audit project"
                : $"{_projects.Count} project(s) loaded // metadata store online";
        }
        catch (Exception)
        {
            StatusText.Text = "storage error // project registry unavailable";
        }
    }

    private void ProjectsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ProjectsList.SelectedItem is not AuditProject project)
            return;

        _selectedProject = project;
        _projectActivated?.Invoke(project);
        NameTextBox.Text = project.Name;
        ScopeTextBox.Text = project.Scope;
        StatusText.Text = $"selected // {project.Name} // {project.Scope}";
    }

    private void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        _ = CreateProjectAsync();
    }

    private async Task CreateProjectAsync()
    {
        try
        {
            var project = AuditProject.Create(NameTextBox.Text, ScopeTextBox.Text);
            await _store.SaveAsync(project);
            _selectedProject = project;
            _projectActivated?.Invoke(project);
            await ReloadAsync();
            StatusText.Text = $"created // {project.Name} // project metadata persisted";
        }
        catch (ArgumentException ex)
        {
            StatusText.Text = $"validation failed // {ex.Message}";
        }
        catch (IOException)
        {
            StatusText.Text = "create failed // project metadata was not persisted";
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _selectedProject = null;
        ProjectsList.SelectedItem = null;
        NameTextBox.Clear();
        ScopeTextBox.Clear();
        NameTextBox.Focus();
        StatusText.Text = "form cleared // enter a name and scope, then select CREATE";
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var project = _selectedProject is null
                ? AuditProject.Create(NameTextBox.Text, ScopeTextBox.Text)
                : _selectedProject with { Name = NameTextBox.Text.Trim(), Scope = ScopeTextBox.Text.Trim() };

            await _store.SaveAsync(project);
            _selectedProject = project;
            await ReloadAsync();
            StatusText.Text = $"saved // {project.Name} // project metadata persisted";
        }
        catch (ArgumentException ex)
        {
            StatusText.Text = $"validation failed // {ex.Message}";
        }
        catch (IOException)
        {
            StatusText.Text = "save failed // project metadata was not persisted";
        }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject is null)
        {
            StatusText.Text = "select a project before deleting";
            return;
        }

        var confirmation = MessageBox.Show(
            $"Delete audit project '{_selectedProject.Name}'?",
            "Delete audit project",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes)
            return;

        await _store.DeleteAsync(_selectedProject.Id);
        _selectedProject = null;
        NameTextBox.Clear();
        ScopeTextBox.Clear();
        await ReloadAsync();
        StatusText.Text = "deleted // project metadata removed";
    }

    private async void StartSessionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject is null)
        {
            StatusText.Text = "select a project before starting a session";
            return;
        }

        var sessions = await _sessionStore.ListAsync();
        if (sessions.Any(session => session.ProjectId == _selectedProject.Id && session.Status == AuditSessionStatus.Active))
        {
            StatusText.Text = "session already active // end it before starting another";
            return;
        }

        var session = AuditSession.Start(_selectedProject.Id);
        await _sessionStore.SaveAsync(session);
        _sessionChanged?.Invoke(session);
        StatusText.Text = $"session active // {session.Id[..8].ToUpperInvariant()} // observations may now be attached";
    }

    private async void EndSessionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject is null)
        {
            StatusText.Text = "select a project before ending a session";
            return;
        }

        var session = (await _sessionStore.ListAsync())
            .LastOrDefault(item => item.ProjectId == _selectedProject.Id && item.Status == AuditSessionStatus.Active);
        if (session is null)
        {
            StatusText.Text = "no active session // start one before ending it";
            return;
        }

        await _sessionStore.SaveAsync(session.Complete());
        _sessionChanged?.Invoke(null);
        StatusText.Text = $"session completed // {session.Id[..8].ToUpperInvariant()}";
    }

    private async void ImportNmapButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject is null)
        {
            StatusText.Text = "select a project before importing Nmap output";
            return;
        }

        var session = (await _sessionStore.ListAsync())
            .LastOrDefault(item => item.ProjectId == _selectedProject.Id && item.Status == AuditSessionStatus.Active);
        if (session is null)
        {
            StatusText.Text = "import blocked // start an active audit session first";
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = "Nmap XML (*.xml)|*.xml|All files (*.*)|*.*",
            Title = "Import Nmap XML output"
        };
        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var xml = await File.ReadAllTextAsync(dialog.FileName);
            var evidence = AuditEvidence.Create(session.Id, "nmap", dialog.FileName, xml);
            var result = new NmapXmlParser().Parse(xml, session.Id, evidence.Id, evidence.CapturedAt);
            if (!result.IsValid)
            {
                StatusText.Text = $"import blocked // {result.Issues[0]}";
                return;
            }

            await _evidenceStore.SaveAsync(evidence);
            await _observationStore.AddAsync(result.Observations);
            var hostCount = result.Observations.Count(item => item.Kind == AuditObservationKind.Host);
            var portCount = result.Observations.Count(item => item.Kind == AuditObservationKind.Port);
            var serviceCount = result.Observations.Count(item => item.Kind == AuditObservationKind.Service);
            StatusText.Text = $"imported // {hostCount} host(s), {portCount} port(s), {serviceCount} service(s) // evidence saved";
        }
        catch (IOException)
        {
            StatusText.Text = "import failed // Nmap XML could not be read";
        }
    }

    private async void ViewObservationsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject is null)
        {
            StatusText.Text = "select a project before viewing observations";
            return;
        }

        var session = (await _sessionStore.ListAsync())
            .LastOrDefault(item => item.ProjectId == _selectedProject.Id && item.Status == AuditSessionStatus.Active);
        if (session is null)
        {
            StatusText.Text = "no active session // start one before viewing observations";
            return;
        }

        var window = new AuditObservationsWindow(_observationStore, _evidenceStore, session) { Owner = this };
        window.ShowDialog();
    }

    private async void ViewFindingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject is null)
        {
            StatusText.Text = "select a project before viewing findings";
            return;
        }

        var session = (await _sessionStore.ListAsync())
            .LastOrDefault(item => item.ProjectId == _selectedProject.Id && item.Status == AuditSessionStatus.Active);
        if (session is null)
        {
            StatusText.Text = "no active session // start one before viewing findings";
            return;
        }

        var window = new AuditFindingsWindow(_findingStore, session) { Owner = this };
        window.ShowDialog();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
