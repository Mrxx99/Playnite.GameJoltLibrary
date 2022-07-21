using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Web;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Dom.Html;
using AngleSharp.Extensions;
using GameJoltLibrary.Models;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using Polly;
using Polly.Retry;

namespace GameJoltLibrary
{
    public class GameJoltMetadataProvider : LibraryMetadataProvider
    {
        private readonly GameJoltLibrary _gameJoltLibrary;
        private readonly ILogger _logger;
        private readonly RetryPolicy<IElement> _retryPolicy;
        private IReadOnlyDictionary<long, GameJoltGameMetadata> _onlineGamesMetadata;

        public GameJoltMetadataProvider(GameJoltLibrary gameJoltLibrary, ILogger logger)
        {
            _gameJoltLibrary = gameJoltLibrary;
            _logger = logger;
            _retryPolicy = Policy
                .HandleResult<IElement>(e => e is null)
                .Or<Exception>()
                .WaitAndRetry(5, retryAttempt => TimeSpan.FromSeconds(1));
        }

        public override GameMetadata GetMetadata(Game game)
        {
            var metadata = new GameMetadata
            {
                Links = new List<Link>(),
                Tags = new HashSet<MetadataProperty>(),
                Genres = new HashSet<MetadataProperty>(),
                Features = new HashSet<MetadataProperty>(),
                Developers = new HashSet<MetadataProperty>(),
                Categories = new HashSet<MetadataProperty>(),
            };

            bool isIdParsable = int.TryParse(game.GameId, NumberStyles.Integer, CultureInfo.InvariantCulture, out int gameId);

            if (!isIdParsable)
            {
                return metadata;
            }

            // TODO why is Playnite in OfflineMode in Debug??
            // prever reading metadata online because it contains more information
            if (game.IsInstalled/* && _gameJoltLibrary.PlayniteApi.ApplicationInfo.InOfflineMode*/)
            {
                var installedGamesMetadata = InstalledGamesProvider.GetGamesMetadata(_logger);

                if (installedGamesMetadata.TryGetValue(gameId, out var installedGameMetadata))
                {
                    ApplyMetadata(game, metadata, installedGameMetadata);
                }
            }
            else
            {
                var onlineGameMetadata = GetOnlineMetadata(game.GameId);

                if (onlineGameMetadata is not null)
                {
                    ApplyMetadata(game, metadata, onlineGameMetadata);
                }
            }

            return metadata;
        }

        private void ApplyMetadata(Game game, GameMetadata metadata, GameJoltGameMetadata gameJoltMetadata)
        {
            // Cover image
            if (!string.IsNullOrEmpty(gameJoltMetadata.ThumbnailMediaItem?.ImgUrl?.AbsolutePath))
            {
                metadata.CoverImage = new MetadataFile(gameJoltMetadata.ThumbnailMediaItem.ImgUrl.AbsoluteUri);
            }
            else if (!string.IsNullOrEmpty(gameJoltMetadata.ImageThumbnail?.AbsolutePath))
            {
                metadata.CoverImage = new MetadataFile(gameJoltMetadata.ImageThumbnail.AbsolutePath);
            }

            // Background image
            if (!string.IsNullOrEmpty(gameJoltMetadata.HeaderMediaItem?.ImgUrl?.AbsolutePath))
            {
                metadata.BackgroundImage = new MetadataFile(gameJoltMetadata.HeaderMediaItem.ImgUrl.AbsoluteUri);
            }

            // Developer
            if (!string.IsNullOrEmpty(gameJoltMetadata.Developer?.DisplayName))
            {
                metadata.Developers.Add(new MetadataNameProperty(gameJoltMetadata.Developer.DisplayName));
                metadata.Links.Add(new Link("Game Jolt Developer Page", gameJoltMetadata.Developer.DeveloperLink));
            }

            // Category
            if (!string.IsNullOrEmpty(gameJoltMetadata.Category))
            {
                metadata.Genres.Add(new MetadataNameProperty(gameJoltMetadata.Category));
            }

            // Description
            if (!_gameJoltLibrary.PlayniteApi.ApplicationInfo.InOfflineMode)
            {
                metadata.Description = GetDescription(game.Name, gameJoltMetadata.StorePageLink);
            }

            metadata.Links.Add(new Link("Game Jolt Store Page", gameJoltMetadata.StorePageLink));
        }


        private GameJoltGameMetadata GetOnlineMetadata(string gameId)
        {
            string gameDiscoverUrl = $"https://gamejolt.com/site-api/web/discover/games/{gameId}";

            var http = new HttpClient();
            var result = http.GetAsync(gameDiscoverUrl).GetAwaiter().GetResult();

            var metaData = Serialization.FromJsonStream<GameJoltWebResult<LibraryGameResultPayload>>(result.Content.ReadAsStreamAsync().GetAwaiter().GetResult());

            return metaData.Payload.Game;
        }

        private IReadOnlyDictionary<long, GameJoltGameMetadata> GetOnlineMetadata()
        {
            var http = new HttpClient();
            http.BaseAddress = new Uri("https://gamejolt.com/site-api/");

            string userName = _gameJoltLibrary.LoadPluginSettings<GameJoltLibrarySettings>().UserName;

            var result = http.GetAsync($"web/library/games/owned/@{userName}").GetAwaiter().GetResult();

            var ownedGames = Serialization.FromJsonStream<GameJoltWebResult<LibraryGamesResultPayload>>(result.Content.ReadAsStreamAsync().GetAwaiter().GetResult());

            return ownedGames.Payload.Games.ToDictionary(kv => kv.Id);
        }

        public string GetDescription(string gameName, string storePage, IBrowsingContext browsingContext)
        {
            try
            {
                IElement descriptionElement = null;

                using (var webView = _gameJoltLibrary.PlayniteApi.WebViews.CreateOffscreenView(new WebViewSettings { JavaScriptEnabled = true }))
                {
                    webView.NavigateAndWait(storePage);
                    descriptionElement = _retryPolicy.Execute(() => GetDescriptionFromWebView(storePage, browsingContext, webView));
                }

                if (descriptionElement is null)
                {
                    descriptionElement = GetDescriptionFromGoogleCache(storePage, browsingContext);
                }

                if (descriptionElement == null)
                {
                    return null;
                }

                FixRelativeLinks(descriptionElement);
                string description = descriptionElement?.InnerHtml;
                return description;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to get description for game {gameName}");
                return null;
            }
        }

        private string GetDescription(string gameName, string storePage)
        {
            try
            {
                var config = Configuration.Default.WithDefaultLoader();
                var context = BrowsingContext.New(config);

                return GetDescription(gameName, storePage, context);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to get description for game {gameName}");
                return null;
            }
        }

        private static void FixRelativeLinks(IElement descriptionElement)
        {
            var links = descriptionElement.Descendents<IHtmlAnchorElement>();
            foreach (var link in links.OfType<IHtmlAnchorElement>())
            {
                var href = link.GetAttribute("href");

                if (href.StartsWith("////"))
                {
                    link.SetAttribute("href", link.Href);
                }
                else if (Uri.TryCreate(href, UriKind.RelativeOrAbsolute, out var url) && !url.IsAbsoluteUri)
                {
                    link.SetAttribute("href", link.Href);
                }
            }
        }

        private static IElement GetDescriptionFromGoogleCache(string storePage, IBrowsingContext context)
        {
            IElement descriptionElement;
            string storePageUrlEncoded = HttpUtility.UrlEncode(storePage);
            string storePageInGoogleChacheUrl = $"http://webcache.googleusercontent.com/search?q=cache%3A{storePageUrlEncoded}";
            var document = context.OpenAsync(storePageInGoogleChacheUrl).GetAwaiter().GetResult();
            descriptionElement = document.QuerySelector(".game-description-content");
            return descriptionElement;
        }

        private IElement GetDescriptionFromWebView(string storePage, IBrowsingContext context, IWebView webView)
        {
            string pageSource = webView.GetPageSource();
            var document = context.OpenAsync(req => req.Content(pageSource).Address("https://gamejolt.com")).GetAwaiter().GetResult();
            var descriptionElement = document.QuerySelector(".game-description-content");
            if (descriptionElement is null && pageSource.Contains("game-maturity-block"))
            {
                AcceptMaturityWarning(webView, storePage);
            }

            return descriptionElement;
        }

        private void AcceptMaturityWarning(IWebView webView, string storePage)
        {
            try
            {
                webView.EvaluateScriptAsync(@"this.document.getElementsByClassName(""-primary"")[0].click()").GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, $"Could not accept maturity warning for game {storePage}");
            }
        }
    }
}
