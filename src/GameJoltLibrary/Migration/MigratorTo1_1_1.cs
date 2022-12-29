using System;
using System.Collections.Generic;
using System.Linq;
using Playnite.SDK;

namespace GameJoltLibrary.Migration;

public static class MigratorTo1_1_1
{
    public static void Migrate(IPlayniteAPI playniteAPI, GameJoltLibrarySettings settings, InstalledGamesProvider installedGamesProvider)
    {
        using (playniteAPI.Database.BufferedUpdate())
        {
            var gameJoltGames = playniteAPI.Database.Games.Where(game => game.PluginId == GameJoltLibrary.PluginId).ToArray();

            IReadOnlyList<InstalledGameInfo> gamePackagesInfo = null;

            foreach (var installedGame in gameJoltGames)
            {
                IReadOnlyList<ExecutablePackage> packagesForGame = null;
                foreach (var gameAction in installedGame.GameActions.ToArray())
                {
                    if (gameAction.IsPlayAction && gameAction.Arguments is null && gameAction.AdditionalArguments is null)
                    {
                        gamePackagesInfo ??= installedGamesProvider.GetGamePackagesInfo();
                        packagesForGame ??= installedGamesProvider.GetExecutablePackages(gamePackagesInfo.Where(x => x.GameId == installedGame.GameId)).ToArray();

                        if (packagesForGame.Any(package => string.Equals(package.ExecutablePath, gameAction.Path, StringComparison.OrdinalIgnoreCase)))
                        {
                            installedGame.GameActions.Remove(gameAction);
                            playniteAPI.Database.Games.Update(installedGame);
                        }
                    }
                }
            }
        }
    }
}
