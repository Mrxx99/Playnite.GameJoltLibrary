using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using GameJoltLibrary.Models;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace GameJoltLibrary;

public class InstalledGamesProvider
{
    private static string _localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static string _gameJoltUserDataPath = Path.Combine(_localAppDataPath, "game-jolt-client", "User Data", "Default");
    private readonly ILogger _logger;

    public InstalledGamesProvider(ILogger logger)
    {
        _logger = logger;
    }

    public Dictionary<string, GameMetadata> GetInstalledGamesV2(LibraryGetGamesArgs args)
    {
        var games = new Dictionary<string, GameMetadata>();

        var installedGamesMetadata = GetGamesMetadataV2();
        var gamePackagesInfo = GetGamePackagesInfo();

        foreach (var gameInfo in installedGamesMetadata)
        {
            args.CancelToken.ThrowIfCancellationRequested();

            var packagesForGame = gamePackagesInfo.Where(x => x.GameId == gameInfo.Id).ToList();

            var gameMetaData = new GameMetadata
            {
                Source = new MetadataNameProperty("Game Jolt"),
                GameId = gameInfo.Id.ToString(),
                Name = gameInfo.Title,
                IsInstalled = true,
                BackgroundImage = new MetadataFile(gameInfo.HeaderMediaItem?.ImgUrl?.AbsoluteUri),
                CoverImage = new MetadataFile(gameInfo.ThumbnailMediaItem?.ImgUrl?.AbsoluteUri),
                InstallDirectory = packagesForGame.FirstOrDefault()?.InstallDir,
                GameActions = new List<GameAction>(),

            };

            foreach (var package in packagesForGame)
            {
                var launchOption = GetFittingLaunchOption(package);

                if (launchOption != null)
                {
                    string relativeGameIExecutablePath = launchOption.ExecutablePath;
                    string gameExecutablePath = Path.Combine(package.InstallDir, "data", relativeGameIExecutablePath);

                    if (File.Exists(gameExecutablePath))
                    {
                        gameMetaData.GameActions.Add(new GameAction
                        {
                            IsPlayAction = true,
                            Type = GameActionType.File,
                            Path = gameExecutablePath,
                            Name = package.Title
                        });
                    }
                    else
                    {
                        _logger.Warn($"game executable does not exist ({gameExecutablePath})");
                    }
                }
            }

            if (gameMetaData.GameActions.FirstOrDefault() is GameAction primaryGameAction)
            {
                using var icon = Icon.ExtractAssociatedIcon(primaryGameAction.Path);
                using var image = icon.ToBitmap();
                var iconBytes = ImageToByte(image);
                gameMetaData.Icon = new MetadataFile("icon", iconBytes);
            }

            games.Add(gameMetaData.GameId, gameMetaData);
        }

        return games;
    }

    private LaunchOption GetFittingLaunchOption(InstalledGameInfo package)
    {
        var launchOptions = package.LaunchOptions.EmptyIfNull().Where(l => l.IsValid());
        var windowsLaunchOptions = launchOptions.Where(l => l.Os.Contains("windows", StringComparison.OrdinalIgnoreCase));
        var windows32BitVersions = windowsLaunchOptions.Where(l => !l.Os.Contains("64"));
        var windows64BitVersions = windowsLaunchOptions.Where(l => l.Os.Contains("64"));
        var fittingBitVersions = Utility.Is64BitOs ? windows64BitVersions.Concat(windows32BitVersions) : windows32BitVersions;

        if (fittingBitVersions.Any())
        {
            return fittingBitVersions.FirstOrDefault();
        }

        return GetLaunchOptionFromGameManifestFile(package);
    }

    private LaunchOption GetLaunchOptionFromGameManifestFile(InstalledGameInfo package)
    {
        var manifestFile = Path.Combine(package.InstallDir, ".manifest");

        if (File.Exists(manifestFile))
        {
            try
            {
                var gameManifest = Serialization.FromJsonFile<GameManifest>(manifestFile);

                if (!string.IsNullOrEmpty(gameManifest?.GameInfo?.Dir) && !string.IsNullOrEmpty(gameManifest?.LaunchOptions?.Executable)
                    && gameManifest.Os.EmptyIfNull().Contains("windows", StringComparison.OrdinalIgnoreCase)
                    && (Utility.Is64BitOs || gameManifest.Arch != "64"))
                {
                    string gameExecutablePath = Path.Combine(package.InstallDir, gameManifest.GameInfo.Dir, gameManifest.LaunchOptions.Executable);
                    return new LaunchOption
                    {
                        ExecutablePath = gameExecutablePath,
                        Os = gameManifest.Os,
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to read launch settings from game manifest file {manifestFile}");
            }
        }
        else
        {
            _logger.Warn($"Failed to find game manifest file {manifestFile}");
        }

        return null;
    }

    public Dictionary<string, GameMetadata> GetInstalledGames(LibraryGetGamesArgs args)
    {
        var games = new Dictionary<string, GameMetadata>();

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var gameJoltDefaultUserData = Path.Combine(localAppData, "game-jolt-client", "User Data", "Default");
        var installedGamesInfoFile = Path.Combine(gameJoltDefaultUserData, "packages.wttf");

        if (!File.Exists(installedGamesInfoFile))
        {
            _logger.Warn($"Not found GameJolt file {installedGamesInfoFile}");
            return games;
        }
        _logger.Info($"Found GameJolt file {installedGamesInfoFile}");
        var installedGamesInfo = Serialization.FromJsonFile<InstalledGamesInfo>(installedGamesInfoFile);

        var installedGamesMetadata = GetGamesMetadata();

        if (installedGamesMetadata?.Objects is null)
        {
            return games;
        }

        foreach (var installedGameInfo in installedGamesInfo.Objects.Values.Where(i => !string.IsNullOrEmpty(i?.InstallDir)))
        {
            if (!installedGamesMetadata.Objects.TryGetValue(installedGameInfo.GameId, out var installedGameMetadata) || installedGameMetadata is null)
            {
                continue;
            }

            var gameInfo = new GameMetadata
            {
                Source = new MetadataNameProperty("Game Jolt"),
                GameId = installedGameInfo.GameId.ToString(),
                Name = installedGameInfo.Title ?? installedGameMetadata.Title,
                IsInstalled = true,
                BackgroundImage = new MetadataFile(installedGameMetadata.HeaderMediaItem?.ImgUrl?.AbsoluteUri),
                CoverImage = new MetadataFile(installedGameMetadata.ThumbnailMediaItem?.ImgUrl?.AbsoluteUri),
                InstallDirectory = installedGameInfo.InstallDir,
                GameActions = new List<GameAction>()
            };

            foreach (var launchOption in installedGameInfo.LaunchOptions
                .Where(l => l.IsValid()
                            && l.Os.Contains("windows", StringComparison.OrdinalIgnoreCase)
                            && (Utility.Is64BitOs || !l.Os.Contains("64"))))
            {
                string relativeGameIExecutablePath = launchOption.ExecutablePath;
                string gameExecutablePath = Path.Combine(installedGameInfo.InstallDir, "data", relativeGameIExecutablePath);

                if (File.Exists(gameExecutablePath))
                {
                    gameInfo.GameActions.Add(new GameAction
                    {
                        IsPlayAction = true,
                        Type = GameActionType.File,
                        Path = gameExecutablePath,
                        Name = launchOption.Os
                    });
                }
                else
                {

                }
            }

            if (!gameInfo.GameActions.Any())
            {
                var manifestFile = Path.Combine(installedGameInfo.InstallDir, ".manifest");

                if (File.Exists(manifestFile))
                {
                    var gameManifest = Serialization.FromJsonFile<GameManifest>(manifestFile);

                    if (!string.IsNullOrEmpty(gameManifest?.GameInfo?.Dir) && !string.IsNullOrEmpty(gameManifest?.LaunchOptions?.Executable)
                        && gameManifest.Os.EmptyIfNull().Contains("windows", StringComparison.OrdinalIgnoreCase)
                        && (Utility.Is64BitOs || gameManifest.Arch != "64"))
                    {
                        string gameExecutablePath = Path.Combine(installedGameInfo.InstallDir, gameManifest.GameInfo.Dir, gameManifest.LaunchOptions.Executable);
                        if (File.Exists(gameExecutablePath))
                        {
                            gameInfo.GameActions.Add(new GameAction
                            {
                                IsPlayAction = true,
                                Type = GameActionType.File,
                                Path = gameExecutablePath
                            });
                        }
                        else
                        {

                        }
                    }
                }
            }

            if (gameInfo.GameActions.FirstOrDefault() is GameAction primaryGameAction)
            {
                using var icon = Icon.ExtractAssociatedIcon(primaryGameAction.Path);
                using var image = icon.ToBitmap();
                var iconBytes = ImageToByte(image);
                gameInfo.Icon = new MetadataFile("icon", iconBytes);
            }

            games.Add(gameInfo.GameId, gameInfo);
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

    private IEnumerable<InstalledGameMetadata> GetGamesMetadataV2()
    {
        var installedGamesMetadataFile = Path.Combine(_gameJoltUserDataPath, "games.wttf");

        InstalledGamesMetadata installedGamesMetadata = null;

        if (File.Exists(installedGamesMetadataFile))
        {
            try
            {
                installedGamesMetadata = Serialization.FromJsonFile<InstalledGamesMetadata>(installedGamesMetadataFile);
                _logger.Info($"Read GameJolt file {installedGamesMetadataFile}");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to deserialize {installedGamesMetadataFile}");
            }
        }
        else
        {
            _logger.Warn($"Not found GameJolt file {installedGamesMetadataFile}");
        }

        return installedGamesMetadata?.Objects?.Values?.Where(i => i != null) ?? Enumerable.Empty<InstalledGameMetadata>();
    }

    private IEnumerable<InstalledGameInfo> GetGamePackagesInfo()
    {
        var installedGamesInfoFile = Path.Combine(_gameJoltUserDataPath, "packages.wttf");

        InstalledGamesInfo installedGamesMetadata = null;

        if (File.Exists(installedGamesInfoFile))
        {
            try
            {
                installedGamesMetadata = Serialization.FromJsonFile<InstalledGamesInfo>(installedGamesInfoFile);
                _logger.Info($"Read GameJolt file {installedGamesInfoFile}");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to deserialize {installedGamesInfoFile}");
            }
        }
        else
        {
            _logger.Warn($"Not found GameJolt file {installedGamesInfoFile}");
        }

        return installedGamesMetadata?.Objects?.Values?.Where(i => i != null) ?? Enumerable.Empty<InstalledGameInfo>();
    }
}
