using System;

namespace GameJoltLibrary.Migration;

public class MigrationManager
{
    private readonly GameJoltLibrary _gameJoltLibrary;

    public MigrationManager(GameJoltLibrary gameJoltLibrary)
    {
        _gameJoltLibrary = gameJoltLibrary;
    }

    public void Migrate()
    {
        var currentVersion = typeof(GameJoltLibrary).Assembly.GetName().Version;
        var lastMigratedVesion = _gameJoltLibrary.Settings.LibraryVersion ?? new Version();

        if (lastMigratedVesion < currentVersion)
        {
            if (lastMigratedVesion < new Version("1.1.1"))
            {
                MigratorTo1_1_1.Migrate(_gameJoltLibrary.PlayniteApi, _gameJoltLibrary.Settings, _gameJoltLibrary.InstalledGamesProvider);
            }

            _gameJoltLibrary.Settings.LibraryVersion = currentVersion;
            _gameJoltLibrary.SavePluginSettings(_gameJoltLibrary.Settings);
        }
    }
}
