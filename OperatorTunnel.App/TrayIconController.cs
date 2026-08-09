using System.Drawing;
using Forms = System.Windows.Forms;

namespace OperatorTunnel.App;

public sealed class TrayIconController : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Action _showRequested;
    private readonly Action _exitRequested;

    public TrayIconController(Action showRequested, Action exitRequested)
    {
        _showRequested = showRequested;
        _exitRequested = exitRequested;
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Operator Tunnel",
            Visible = true
        };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Operator Tunnel", null, (_, _) => _showRequested());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => _exitRequested());
        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => _showRequested();
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}

