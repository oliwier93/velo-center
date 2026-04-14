using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using VeloCenter.App.ViewModels;
using VeloCenter.App.Views;
using VeloCenter.Infrastructure.Activities;
using VeloCenter.Infrastructure.Persistence;

namespace VeloCenter.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var databasePath = VeloCenterSqliteDatabase.GetDefaultDatabasePath();
            VeloCenterSqliteDatabase.Initialize(databasePath);

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(new SqliteActivityRepository(databasePath)),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
