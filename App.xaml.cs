using BrickRigsModManagerWPF;
using System.Windows;

namespace BrickRigsModManager
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize theme manager
            ThemeManager.Initialize();
        }

    }
}
