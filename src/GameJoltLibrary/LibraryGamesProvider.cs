using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using GameJoltLibrary.Exceptions;
using GameJoltLibrary.Helpers;
using GameJoltLibrary.Models;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using Polly;
using Polly.Retry;

namespace GameJoltLibrary;

public class LibraryGamesProvider
{
    private readonly IPlayniteAPI _playniteAPI;
    private readonly ILogger _logger;
    private readonly RetryPolicy<List<GameJoltGameMetadata>> _retryOwnedGamesPolicy;

    public LibraryGamesProvider(IPlayniteAPI playniteAPI, ILogger logger)
    {
        _playniteAPI = playniteAPI;
        _logger = logger;
        _retryOwnedGamesPolicy = Policy
            .HandleResult<List<GameJoltGameMetadata>>(e => e is null)
            .Or<Exception>(ex => ex is not UserNotFoundException)
            .WaitAndRetry(5, retryAttempt => TimeSpan.FromSeconds(1));
    }

    public IEnumerable<GameMetadata> GetLibraryGames(GameJoltLibrarySettings settings, CancellationToken cancelToken)
    {
        var games = new List<GameMetadata>();

        try
        {
            string userName = settings.UserName;
            string ownedGamesUrl = $"https://gamejolt.com/site-api/web/library/games/owned/@{userName}";
            var ownedGames = GetGamesFromApi(ownedGamesUrl, cancelToken);

            var libraryGames = ownedGames;

            if (settings.TreatFollowedGamesAsLibraryGames)
            {
                string followedGamesUrl = $"https://gamejolt.com/site-api/web/library/games/followed/@{userName}";
                var followedGames = GetGamesFromApi(followedGamesUrl, cancelToken);
                libraryGames = libraryGames.Concat(followedGames);
            }

            var libraryGamesArray = libraryGames
                .Distinct(SelectorComparer.Create((GameJoltGameMetadata x) => x.Id))
                .ToArray();

            foreach (var ownedGame in libraryGamesArray)
            {
                cancelToken.ThrowIfCancellationRequested();

                var game = new GameMetadata
                {
                    GameId = ownedGame.Id,
                    Source = new MetadataNameProperty("Game Jolt"),
                    Name = ownedGame.Title,
                    IsInstalled = false,
                    Links = new List<Link> { new Link("Game Jolt Store Page", ownedGame.StorePageLink) },
                    Developers = new HashSet<MetadataProperty> { new MetadataNameProperty(ownedGame.Developer.DisplayName) }
                };

                games.Add(game);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during loading library games for Game Jolt");
            throw;
        }

        return games;
    }

    private IEnumerable<GameJoltGameMetadata> GetGamesFromApi(string ownedGamesUrl, CancellationToken cancelToken)
    {
        return _retryOwnedGamesPolicy.Execute(() =>
        {
            var http = new HttpClient();

            var result = http.GetAsync(ownedGamesUrl, cancelToken).GetAwaiter().GetResult();

            if (result.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new UserNotFoundException();
            }

            var stringContent = result.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            var ownedGamesResult = Serialization.FromJsonStream<GameJoltWebResult<LibraryGamesResultPayload>>(result.Content.ReadAsStreamAsync().GetAwaiter().GetResult());
            return ownedGamesResult.Payload.Games;
        });
    }

    public void RemoveLibraryGames()
    {
        using (_playniteAPI.Database.BufferedUpdate())
        {
            // Any collection changes here don't generate any events

            var libraryGames = _playniteAPI.Database.Games.Where(game => game.PluginId == GameJoltLibrary.PluginId && !game.IsInstalled);
            foreach (var libraryGame in libraryGames)
            {
                _playniteAPI.Database.Games.Remove(libraryGame);
            }
        }
    }

    /// <summary>
    /// Removes games that are not anymore owned games, also takes care if user changed
    /// </summary>
    public void UpdateRemovedLibraryGames(IEnumerable<GameMetadata> ownedLibraryGames)
    {
        using (_playniteAPI.Database.BufferedUpdate())
        {
            // Any collection changes here don't generate any events

            var currentLibraryGames = _playniteAPI.Database.Games.Where(game => game.PluginId == GameJoltLibrary.PluginId && !game.IsInstalled);
            var gamesToRemove = currentLibraryGames.Where(game => !ownedLibraryGames.Any(i => i.GameId == game.GameId));
            foreach (var libraryGame in gamesToRemove)
            {
                _playniteAPI.Database.Games.Remove(libraryGame);
            }
        }
    }
}
