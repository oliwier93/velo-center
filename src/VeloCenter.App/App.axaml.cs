using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using VeloCenter.App.ViewModels;
using VeloCenter.App.Views;
using VeloCenter.Infrastructure.Activities;
using VeloCenter.Infrastructure.Integrations.Strava;
using VeloCenter.Infrastructure.Maintenance;
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
            VeloCenterSqliteDatabase.Initialize(databasePath, seedDemoData: false);
            var activityRepository = new SqliteActivityRepository(databasePath);
            var activityImportService = new LocalFileActivityImportService(databasePath);
            var stravaIntegrationService = new StravaIntegrationService(databasePath);
            var applicationResetService = new LocalApplicationResetService(databasePath);

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(activityRepository, activityImportService, stravaIntegrationService, applicationResetService),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
