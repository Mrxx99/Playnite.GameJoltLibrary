using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using AngleSharp;
using AngleSharp.Dom.Html;
using Playnite.SDK;
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

                var webView = PlayniteApi.WebViews.CreateOffscreenView(new WebViewSettings { JavaScriptEnabled = true, WindowHeight = 480, WindowWidth = 640 });

                webView.NavigateAndWait(ownedGamesUrl);
                var config = Configuration.Default.WithDefaultLoader();
                var context = BrowsingContext.New(config);
                Thread.Sleep(10_000);
                var pageSource = webView.GetPageSource();

                var document = context.OpenAsync(req => req.Content(pageSource).Address("https://gamejolt.com")).GetAwaiter().GetResult();

                var gamesGridElement = document.QuerySelector(".game-grid-items");
                var gameLinks = gamesGridElement.QuerySelectorAll(".game-thumbnail")
                    .OfType<IHtmlAnchorElement>()
                    .Select(a => a.Href);

                webView.Dispose();

                foreach (var gameLink in gameLinks)
                {
                    using var gameWebView = PlayniteApi.WebViews.CreateOffscreenView(new WebViewSettings { JavaScriptEnabled = true, WindowHeight = 480, WindowWidth = 640 });
                    gameWebView.NavigateAndWait(gameLink);
                    Thread.Sleep(15_000);
                    var gamepageSource = gameWebView.GetPageSource();

                    if (gamepageSource.Contains("game-maturity-block"))
                    {
                        GameJoltMetadataProvider.AcceptMaturityWarning(gameWebView, gameLink, logger);
                        Thread.Sleep(15_000);
                        gamepageSource = gameWebView.GetPageSource();
                    }

                    var gameDocument = context.OpenAsync(req => req.Content(gamepageSource).Address("https://gamejolt.com")).GetAwaiter().GetResult();

                    string name = gameDocument.QuerySelector("meta[property=\"og:title\"]")?.GetAttribute("content");

                    var a = gameWebView.GetCurrentAddress();

                    var game = new GameMetadata
                    {
                        Source = new MetadataNameProperty("Game Jolt"),
                        Name = gameDocument.QuerySelector("meta[property=\"og:title\"]")?.GetAttribute("content"),
                        IsInstalled = false,
                        Links = new List<Link>()
                    };

                    if (gameDocument.QuerySelector("meta[property=\"og:image\"]")?.GetAttribute("content") is string ocverImagUrl)
                    {
                        game.CoverImage = new MetadataFile(ocverImagUrl);
                    }

                    game.Links.Add(new Link("store page web", gameLink));
                    games.Add(game);
                }
            }
            catch (Exception ex)
            {

            }

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