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

        public string ImportErrorMessageId { get; } = "GameJolt_libImportError";

        public static Guid PluginId { get; set; } = Guid.Parse("555d58fd-a000-401b-972c-9230bed81aed");

        public override Guid Id { get; } = PluginId;

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
            InstalledGamesProvider = new InstalledGamesProvider(api, _logger);
            LibraryGamesProvider = new LibraryGamesProvider(_logger);
            MetadataProvider = new GameJoltMetadataProvider(api, _logger);
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            var games = new List<GameMetadata>();
            Exception importError = null;

            var installedGames = Array.Empty<GameMetadata>();

            try
            {
                installedGames = InstalledGamesProvider.GetInstalledGames(args.CancelToken).ToArray();

                if (_settingsViewModel.Settings.ImportInstalledGames)
                {
                    _logger.Debug($"Found {installedGames.Length} installed Game Jolt games.");
                    games.AddRange(installedGames);
                }

            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to import installed Game Jolt games.");
                importError = ex;
            }

            // Skip update of uninstalled games if error on import
            if (importError is null)
            {
                InstalledGamesProvider.UpdatedUninstalledGames(installedGames);
            }

            if (_settingsViewModel.Settings.ImportLibraryGames && _settingsViewModel.Settings.UserName is string userName)
            {
                try
                {
                    var libraryGames = LibraryGamesProvider.GetLibraryGames(userName, args.CancelToken);
                    var libraryGamesToAdd = libraryGames.Where(libraryGame => !games.Any(game => game.GameId == libraryGame.GameId)).ToArray();
                    _logger.Debug(message: $"Found {libraryGamesToAdd.Length} library Game Jolt games.");
                    games.AddRange(libraryGames);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to import library Game Jolt games.");
                    importError = ex;
                }
            }

            if (importError is not null)
            {
                PlayniteApi.Notifications.Add(new NotificationMessage(
                    ImportErrorMessageId,
                    string.Format(PlayniteApi.Resources.GetString("LOCLibraryImportError"), Name) +
                    Environment.NewLine + importError.Message,
                    NotificationType.Error,
                    () => OpenSettingsView()));
            }
            else
            {
                PlayniteApi.Notifications.Remove(ImportErrorMessageId);
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