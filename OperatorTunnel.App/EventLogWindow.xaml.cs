using System.Windows;
using System.Windows.Input;
using OperatorTunnel.Core.Diagnostics;

namespace OperatorTunnel.App;

public partial class EventLogWindow : Window
{
    public EventLogWindow(IReadOnlyList<SecurityEvent> events)
    {
        InitializeComponent();
        EventItems.ItemsSource = events.Reverse().ToArray();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}

