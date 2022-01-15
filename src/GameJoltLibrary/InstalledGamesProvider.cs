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

        var installedGamesMetadata = GetGamesMetadata(_logger);
        var gamePackagesInfo = GetGamePackagesInfo();

        foreach (var gameInfo in installedGamesMetadata.Values)
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

    public static byte[] ImageToByte(Bitmap image)
    {
        //ImageConverter converter = new ImageConverter();
        //return (byte[])converter.ConvertTo(image, typeof(byte[]));
        using MemoryStream ms = new MemoryStream();
        image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return ms.ToArray();
    }

    public static IReadOnlyDictionary<long, InstalledGameMetadata> GetGamesMetadata(ILogger logger)
    {
        var installedGamesMetadataFile = Path.Combine(_gameJoltUserDataPath, "games.wttf");

        InstalledGamesMetadata installedGamesMetadata = null;

        if (File.Exists(installedGamesMetadataFile))
        {
            try
            {
                installedGamesMetadata = Serialization.FromJsonFile<InstalledGamesMetadata>(installedGamesMetadataFile);
                logger.Info($"Read GameJolt file {installedGamesMetadataFile}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to deserialize {installedGamesMetadataFile}");
            }
        }
        else
        {
            logger.Warn($"Not found GameJolt file {installedGamesMetadataFile}");
        }

        if (installedGamesMetadata?.Objects?.Values is null)
        {
            return new Dictionary<long, InstalledGameMetadata>();
        }

        var filtered = installedGamesMetadata?.Objects?.Where(i => i.Value != null);

        return filtered.ToDictionary(x => x.Key, x => x.Value);
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
