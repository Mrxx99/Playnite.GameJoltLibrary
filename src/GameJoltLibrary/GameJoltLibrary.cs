using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Windows.Controls;
using AngleSharp;
using AngleSharp.Dom.Html;
using GameJoltLibrary.Models;
using Newtonsoft.Json;
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

        public GameJoltMetadataProvider MetadataProvider { get; }

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
            MetadataProvider = new GameJoltMetadataProvider(this, logger);
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            if (!GameJolt.IsInstalled || !settingsViewModel.Settings.ImportInstalledGames)
            {
                return Array.Empty<GameMetadata>();
            }

            var games = InstalledGamesProvider.GetInstalledGamesV2(args).Values.ToList();
            var libraryGames = GetLibraryGames();

            games.AddRange(libraryGames);
            return games;
        }

        public IEnumerable<GameMetadata> GetLibraryGames()
        {
            var games = new List<GameMetadata>();

            try
            {
                var settings = settingsViewModel.Settings;
                string ownedGamesUrl = $"https://gamejolt.com/@{settings.UserName}/owned";

                var http = new HttpClient();
                http.BaseAddress = new Uri("https://gamejolt.com/site-api/");

                var result = http.GetAsync("web/library/games/owned/@mrxx99").GetAwaiter().GetResult();

                var stringContent = result.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                var ownedGames = Serialization.FromJsonStream<LibraryGamesResult>(result.Content.ReadAsStreamAsync().GetAwaiter().GetResult());

                var config = Configuration.Default.WithDefaultLoader();
                var context = BrowsingContext.New(config);

                foreach (var ownedGame in ownedGames.Payload.Games)
                {
                    var game = new GameMetadata
                    {
                        GameId = ownedGame.Id.ToString(),
                        Source = new MetadataNameProperty("Game Jolt"),
                        Name = ownedGame.Title,
                        IsInstalled = false,
                        Links = new List<Link> { new Link("store page web", ownedGame.Link) },
                        Developers = new HashSet<MetadataProperty> { new MetadataNameProperty(ownedGame.Developer.DisplayName) }
                    };

                    games.Add(game);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error during loading library games for Game Jolt");
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
            return settingsViewModel;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new GameJoltLibrarySettingsView();
        }
    }
}