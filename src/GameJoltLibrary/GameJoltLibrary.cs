using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using GameJoltLibrary.Exceptions;
using GameJoltLibrary.Migration;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace GameJoltLibrary
{
    public class GameJoltLibrary : LibraryPlugin
    {
        private static readonly ILogger _logger = LogManager.GetLogger();
        private readonly GameJoltLibrarySettingsViewModel _settingsViewModel;

        public GameJoltLibrarySettings Settings => _settingsViewModel.Settings;
        public InstalledGamesProvider InstalledGamesProvider { get; }
        public LibraryGamesProvider LibraryGamesProvider { get; }
        public GameJoltMetadataProvider MetadataProvider { get; }

        public string ImportErrorMessageId { get; } = "GameJolt_libImportError";
        public string UserNotFoundErrorMessageId { get; } = "GameJolt_UserNotFoundError";

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
            LibraryGamesProvider = new LibraryGamesProvider(api, _logger);
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

            // Skip update of installed games if error on import
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
                    LibraryGamesProvider.UpdateRemovedLibraryGames(libraryGames);
                }
                catch (UserNotFoundException ex)
                {
                    _logger.Error(ex, $"User {userName} not found.");
                    LibraryGamesProvider.RemoveLibraryGames();
                    PlayniteApi.Notifications.Add(new NotificationMessage(
                        UserNotFoundErrorMessageId,
                        string.Format(PlayniteApi.Resources.GetString("LOCLibraryImportError"), Name) + Environment.NewLine +
                        string.Format(PlayniteApi.Resources.GetString("LOCGameJoltUserNotFoundError"), userName),
                        NotificationType.Error,
                        () => OpenSettingsView()));
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to import library Game Jolt games.");
                    importError = ex;
                }
            }
            else
            {
                LibraryGamesProvider.RemoveLibraryGames();
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

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            var migrationManager = new MigrationManager(this);
            migrationManager.Migrate();
        }

        public override IEnumerable<PlayController> GetPlayActions(GetPlayActionsArgs args)
        {
            if (args.Game.PluginId != PluginId)
            {
                return Array.Empty<PlayController>();
            }

            var playActions = InstalledGamesProvider.GetPlayActions(args);

            _logger.Info($"Found {playActions.Count} play actions for game {args.Game.Name}");

            return playActions;
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