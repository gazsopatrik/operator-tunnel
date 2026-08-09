using System.Windows;
using System.Windows.Input;
using System.IO;
using System.Security.Cryptography;
using OperatorTunnel.Core.Profiles;
using OperatorTunnel.Core.Security;
using MessageBox = System.Windows.MessageBox;

namespace OperatorTunnel.App;

public partial class ProfileManagerWindow : Window
{
    private readonly EncryptedProfileStore _store;
    private readonly Action<WireGuardProfile> _profileLoaded;
    private readonly Action<string> _profileDeleted;
    private readonly Func<string, bool> _canDelete;

    public ProfileManagerWindow(EncryptedProfileStore store, Action<WireGuardProfile> profileLoaded, Action<string> profileDeleted, Func<string, bool> canDelete)
    {
        InitializeComponent();
        _store = store;
        _profileLoaded = profileLoaded;
        _profileDeleted = profileDeleted;
        _canDelete = canDelete;
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        ProfileItems.ItemsSource = await _store.ListAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async void LoadButton_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileItems.SelectedItem is not string profileName)
            return;

        try
        {
            var profile = await _store.LoadAsync(profileName);
            _profileLoaded(profile);
            Close();
        }
        catch (InvalidDataException)
        {
            MessageBox.Show("The selected profile could not be loaded or validated.", "Profile load failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (CryptographicException)
        {
            MessageBox.Show("The selected profile could not be decrypted for this Windows user.", "Profile load failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (IOException)
        {
            MessageBox.Show("The selected profile could not be loaded or validated.", "Profile load failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileItems.SelectedItem is not string profileName)
            return;

        if (!_canDelete(profileName))
            return;

        var confirmation = MessageBox.Show($"Delete encrypted profile '{profileName}'?", "Confirm profile deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes)
            return;

        if (await _store.DeleteAsync(profileName))
            _profileDeleted(profileName);
        await RefreshAsync();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
