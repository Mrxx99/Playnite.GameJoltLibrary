using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace GameJoltLibrary
{
    public class GameJoltLibrary : LibraryPlugin
    {
        private static readonly ILogger _logger = LogManager.GetLogger();
        private readonly GameJoltLibrarySettingsViewModel _settingsViewModel;

        public InstalledGamesProvider InstalledGamesProvider { get; }
        public LibraryGamesProvider LibraryGamesProvider { get; }
        public GameJoltMetadataProvider MetadataProvider { get; }

        public override Guid Id { get; } = Guid.Parse("555d58fd-a000-401b-972c-9230bed81aed");

        public override string Name => "Game Jolt";

        // Implementing Client adds ability to open it via special menu in playnite.
        public override LibraryClient Client { get; } = new GameJoltClient();

        public GameJoltLibrary(IPlayniteAPI api) : base(api)
        {
            _settingsViewModel = new GameJoltLibrarySettingsViewModel(this);
            Properties = new LibraryPluginProperties
            {
                HasSettings = true
            };
            _logger.Info("GemeJolt library initialized.");
            InstalledGamesProvider = new InstalledGamesProvider(_logger);
            LibraryGamesProvider = new LibraryGamesProvider(_logger);
            MetadataProvider = new GameJoltMetadataProvider(this, _logger);
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            var games = new List<GameMetadata>();

            if (GameJolt.IsInstalled && _settingsViewModel.Settings.ImportInstalledGames)
            {
                var installedGames = InstalledGamesProvider.GetInstalledGamesV2(args).Values.ToList();
                games.AddRange(installedGames);
            }

            if (_settingsViewModel.Settings.ImportLibraryGames && _settingsViewModel.Settings.UserName is string userName)
            {
                var libraryGames = LibraryGamesProvider.GetLibraryGames(userName);
                var libraryGamesToAdd = libraryGames.Where(game => !games.Any(game => game.GameId == game.GameId));
                games.AddRange(libraryGames);
            }

            return games;
        }

        public override string LibraryIcon => GameJolt.Icon;

        public override LibraryMetadataProvider GetMetadataDownloader()
        {
            return MetadataProvider;
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return _settingsViewModel;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new GameJoltLibrarySettingsView();
        }
    }
}