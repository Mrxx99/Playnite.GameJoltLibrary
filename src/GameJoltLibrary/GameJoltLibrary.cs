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

        private GameJoltLibrarySettingsViewModel settingsViewModel { get; set; }

        public override Guid Id { get; } = Guid.Parse("555d58fd-a000-401b-972c-9230bed81aed");

        // Change to something more appropriate
        public override string Name => "GameJolt Library";

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
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            // Return list of user's games.
            return GetInstalledGames().Values.ToList();
        }


        public Dictionary<string, GameMetadata> GetInstalledGames()
        {
            var games = new Dictionary<string, GameMetadata>();

            if (!GameJolt.IsInstalled || !settingsViewModel.Settings.ImportInstalledGames)
            {
                return games;
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var gameJoltDefaultUserData = Path.Combine(localAppData, "game-jolt-client", "User Data", "Default");
            var installedGamesInfoFile = Path.Combine(gameJoltDefaultUserData, "packages.wttf");

            if (File.Exists(installedGamesInfoFile))
            {
                logger.Info($"Found GameJolt file {installedGamesInfoFile}");
                var installedGamesInfo = Serialization.FromJsonFile<InstalledGamesInfo>(installedGamesInfoFile);

                var installedGamesMetadata = GetGamesMetadata();

                foreach (var installedGameInfo in installedGamesInfo.Objects.Values)
                {
                    InstalledGameMetadata installedGameMetadata = null;
                    installedGamesMetadata?.Objects?.TryGetValue(installedGameInfo.GameId, out installedGameMetadata);

                    var gameExecutablePath = Path.Combine(installedGameInfo.InstallDir, "data", installedGameInfo.LaunchOptions[0].ExecutablePath);

                    using var icon = Icon.ExtractAssociatedIcon(gameExecutablePath);
                    using var image = icon.ToBitmap();
                    var iconBytes = ImageToByte(image);

                    var gameInfo = new GameMetadata
                    {
                        Source = new MetadataNameProperty("Game Jolt"),
                        GameId = installedGameInfo.GameId.ToString(),
                        Name = (installedGameMetadata?.Title ?? installedGameInfo.Title),
                        IsInstalled = true,
                        Icon = new MetadataFile("icon", iconBytes),
                        BackgroundImage = new MetadataFile(installedGameMetadata?.HeaderMediaItem.ImgUrl.AbsoluteUri),
                        CoverImage = new MetadataFile(installedGameMetadata?.ThumbnailMediaItem.ImgUrl.AbsoluteUri),
                        InstallDirectory = installedGameInfo.InstallDir,
                        GameActions = new List<GameAction>()
                    };

                    if (installedGameInfo != null && installedGameInfo.LaunchOptions.Any() && installedGameInfo.LaunchOptions[0]?.ExecutablePath != null)
                    {
                        gameInfo.GameActions.Add(new GameAction
                        {
                            IsPlayAction = true,
                            Type = GameActionType.File,
                            Path = Path.Combine(installedGameInfo.InstallDir, "data", installedGameInfo.LaunchOptions[0].ExecutablePath),
                        });
                    }

                    games.Add(gameInfo.GameId, gameInfo);
                }
            }
            else
            {
                logger.Warn($"Not found GameJolt file {installedGamesInfoFile}");
            }

            return games;
        }

        public static byte[] ImageToByte(Bitmap image)
        {
            //ImageConverter converter = new ImageConverter();
            //return (byte[])converter.ConvertTo(image, typeof(byte[]));
            using MemoryStream ms = new MemoryStream();
            image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return ms.ToArray();
        }

        public static InstalledGamesMetadata GetGamesMetadata()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var gameJoltDefaultUserData = Path.Combine(localAppData, "game-jolt-client", "User Data", "Default");

            InstalledGamesMetadata installedGamesMetadata = null;

            var installedGamesMetadataFile = Path.Combine(gameJoltDefaultUserData, "games.wttf");
            if (File.Exists(installedGamesMetadataFile))
            {
                installedGamesMetadata = Serialization.FromJsonFile<InstalledGamesMetadata>(installedGamesMetadataFile);
            }

            return installedGamesMetadata;
        }

        public override string LibraryIcon => GameJolt.Icon;

        public override LibraryMetadataProvider GetMetadataDownloader()
        {
            return new GameJoltMetadataProvider(this);
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