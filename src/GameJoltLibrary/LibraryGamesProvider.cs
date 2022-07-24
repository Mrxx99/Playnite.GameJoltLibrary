using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using GameJoltLibrary.Models;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using Polly;
using Polly.Retry;

namespace GameJoltLibrary;

public class LibraryGamesProvider
{
    private readonly ILogger _logger;
    private readonly RetryPolicy<List<GameJoltGameMetadata>> _retryOwnedGamesPolicy;

    public LibraryGamesProvider(ILogger logger)
    {
        _logger = logger;
        _retryOwnedGamesPolicy = Policy
            .HandleResult<List<GameJoltGameMetadata>>(e => e is null)
            .Or<Exception>()
            .WaitAndRetry(5, retryAttempt => TimeSpan.FromSeconds(1));
    }

    public IEnumerable<GameMetadata> GetLibraryGames(string userName, CancellationToken cancelToken)
    {
        var games = new List<GameMetadata>();

        try
        {
            string ownedGamesUrl = $"https://gamejolt.com/site-api/web/library/games/owned/@{userName}";

            var ownedGames = _retryOwnedGamesPolicy.Execute(() =>
            {
                var http = new HttpClient();

                var result = http.GetAsync(ownedGamesUrl, cancelToken).GetAwaiter().GetResult();

                var stringContent = result.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                var ownedGamesResult = Serialization.FromJsonStream<GameJoltWebResult<LibraryGamesResultPayload>>(result.Content.ReadAsStreamAsync().GetAwaiter().GetResult());
                return ownedGamesResult.Payload.Games;
            });

            foreach (var ownedGame in ownedGames)
            {
                cancelToken.ThrowIfCancellationRequested();

                var game = new GameMetadata
                {
                    GameId = ownedGame.Id.ToString(),
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
        }

        return games;
    }
}
