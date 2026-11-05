using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Fenêtre cible pour les tests : placée HORS de l'écran, sans bouton de barre des tâches, sans prendre le focus.
class TargetForm : Form
{
    protected override bool ShowWithoutActivation { get { return true; } }
}

static class Target
{
    [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr v);

    [STAThread]
    static void Main()
    {
        try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }
        var f = new TargetForm { Text = "dwaTopTestTarget", BackColor = Color.White, StartPosition = FormStartPosition.Manual, ShowInTaskbar = false };
        var vs = SystemInformation.VirtualScreen;
        f.Bounds = new Rectangle(vs.Right + 600, vs.Top + 100, 520, 360);
        f.Controls.Add(new Label { Text = "dwaTop test target", ForeColor = Color.Black, Location = new Point(20, 20), AutoSize = true, Font = new Font("Segoe UI", 14f) });
        Application.Run(f);
    }
}
