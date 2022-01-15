using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using GameJoltLibrary.Models;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace GameJoltLibrary
{
    public class GameJoltLibrary : LibraryPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        public InstalledGamesProvider InstalledGamesProvider { get; }

        private GameJoltLibrarySettingsViewModel settingsViewModel { get; set; }

        public override Guid Id { get; } = Guid.Parse("555d58fd-a000-401b-972c-9230bed81aed");

        public override string Name => "Game Jolt";

        // Implementing Client adds ability to open it via special menu in playnite.
        public override LibraryClient Client { get; } = new GameJoltClient();

        public GameJoltLibrary(IPlayniteAPI api) : base(api)
        {
            settingsViewModel = new GameJoltLibrarySettingsViewModel(this);
            Properties = new LibraryPluginProperties
            {
                HasSettings = true
            };
            logger.Info("GemeJolt library initialized.");
            InstalledGamesProvider = new InstalledGamesProvider(logger);
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            if (!GameJolt.IsInstalled || !settingsViewModel.Settings.ImportInstalledGames)
            {
                return Array.Empty<GameMetadata>();
            }

            var games = InstalledGamesProvider.GetInstalledGamesV2(args).Values.ToList();

            return games;
        }

        public override string LibraryIcon => GameJolt.Icon;

        public override LibraryMetadataProvider GetMetadataDownloader()
        {
            return new GameJoltMetadataProvider(this, logger);
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settingsViewModel;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new GameJoltLibrarySettingsView();
        }
    }
}