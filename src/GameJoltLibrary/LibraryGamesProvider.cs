using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
    private readonly RetryPolicy<LibraryGamesResultPayload> _retryOwnedGamesPolicy;

    public LibraryGamesProvider(IPlayniteAPI playniteAPI, ILogger logger)
    {
        _playniteAPI = playniteAPI;
        _logger = logger;
        _retryOwnedGamesPolicy = Policy
            .HandleResult<LibraryGamesResultPayload>(e => e is null)
            .Or<Exception>(ex => ex is not UserNotFoundException)
            .WaitAndRetry(5, retryAttempt => TimeSpan.FromSeconds(1));
    }

    public IEnumerable<GameMetadata> GetLibraryGames(GameJoltLibrarySettings settings, CancellationToken cancelToken)
    {
        var games = new List<GameMetadata>();
        using var httpClient = new HttpClient();

        httpClient.DefaultRequestHeaders.Add("User-Agent", $"Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:126.0) Gecko/20100101 Firefox/126.0 Playnite/{API.Instance.ApplicationInfo.ApplicationVersion.ToString(2)}");

        using var webView = _playniteAPI.WebViews.CreateOffscreenView();
        var accountClient = new GameJoltAccountClient(webView);
        var isLoggedIn = accountClient.GetIsUserLoggedIn();
      
        try
        {
            string userName = settings.UserName;
            string ownedGamesUrl = $"https://gamejolt.com/site-api/web/library/games/owned/@{userName}";
            var ownedGames = GetGamesFromApi(httpClient, webView, isLoggedIn, ownedGamesUrl, cancelToken);

            var libraryGames = ownedGames;

            if (settings.TreatFollowedGamesAsLibraryGames)
            {
                string followedGamesUrl = $"https://gamejolt.com/site-api/web/library/games/followed/@{userName}";
                var followedGames = GetGamesFromApi(httpClient, webView, isLoggedIn, followedGamesUrl, cancelToken);
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

    private IEnumerable<GameJoltGameMetadata> GetGamesFromApi(HttpClient httpClient, IWebView webView, bool isLoggedIn, string getGamesUrl, CancellationToken cancelToken)
    {
        bool isFirstRequest = true;
        int currentPage = 1;
        int totalPages = 1;
        var games = new List<GameJoltGameMetadata>();

        while (currentPage <= totalPages)
        {
            var gamesOnPage = isLoggedIn
                ? GetGamesFromApiUsingWebView(webView, getGamesUrl, currentPage, out int totalGames, out int gamesPerPage, cancelToken)
                : GetGamesFromApi(httpClient, getGamesUrl, currentPage, out totalGames, out gamesPerPage, cancelToken);

            games.AddRange(gamesOnPage); 

            if (isFirstRequest)
            {
                isFirstRequest = false;
                totalPages = (int)Math.Ceiling((double)totalGames / gamesPerPage);
            }

            currentPage++;
        }

        return games;
    }

    private IEnumerable<GameJoltGameMetadata> GetGamesFromApiUsingWebView(IWebView webView, string getGamesUrl, int pageNumber, out int totalGames, out int gamesPerPage, CancellationToken cancelToken)
    {
        var result = _retryOwnedGamesPolicy.ExecuteAndCapture(_ =>
        {
            string url = $"{getGamesUrl}?page={pageNumber}";
            webView.NavigateAndWait(url);

            var stringContent = webView.GetPageText();
            var ownedGamesResult = Serialization.FromJson<GameJoltWebResult<LibraryGamesResultPayload>>(stringContent);

            return ownedGamesResult.Payload;
        }, cancelToken);

        if (result.Outcome is OutcomeType.Failure)
        {
            totalGames = 0;
            gamesPerPage = 0;
            return [];
        }

        var payload = result.Result;

        totalGames = payload.GamesCount;
        gamesPerPage = payload.PerPage;
        return payload.Games;
    }

    private IEnumerable<GameJoltGameMetadata> GetGamesFromApi(HttpClient httpClient, string getGamesUrl, int pageNumber, out int totalGames, out int gamesPerPage, CancellationToken cancelToken)
    {
        var payload = _retryOwnedGamesPolicy.Execute(() =>
        {
            string url = $"{getGamesUrl}?page={pageNumber}";
            var result = httpClient.GetAsync(url, cancelToken).GetAwaiter().GetResult();
            if (result.StatusCode == HttpStatusCode.NotFound)
            {
                throw new UserNotFoundException();
            }

            result.EnsureSuccessStatusCode();

            var stringContent = result.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            string tt = result.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var ownedGamesResult = Serialization.FromJsonStream<GameJoltWebResult<LibraryGamesResultPayload>>(result.Content.ReadAsStreamAsync().GetAwaiter().GetResult());

            return ownedGamesResult.Payload;
        });

        totalGames = payload.GamesCount;
        gamesPerPage = payload.PerPage;
        return payload.Games;
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
