using OperatorTunnel.Audit;
using System.IO;
using System.Windows;
using System.Windows.Input;
using WpfButton = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;

namespace OperatorTunnel.App;

public partial class AuditProjectManagerWindow : Window
{
    private readonly IAuditProjectStore _store;
    private IReadOnlyList<AuditProject> _projects = [];
    private AuditProject? _selectedProject;

    public AuditProjectManagerWindow(IAuditProjectStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
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

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
