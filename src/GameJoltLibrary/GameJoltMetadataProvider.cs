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
        private readonly IPlayniteAPI _playniteAPI;
        private readonly ILogger _logger;
        private readonly RetryPolicy<IElement> _retryDescriptionPolicy;
        private readonly RetryPolicy<GameJoltGameMetadata> _retryMetadataPolicy;

        public string GetMetadataErrorMessageId { get; } = "GameJolt_libImportError";

        public GameJoltMetadataProvider(IPlayniteAPI playniteAPI, ILogger logger)
        {
            _playniteAPI = playniteAPI;
            _logger = logger;
            _retryDescriptionPolicy = Policy
                .HandleResult<IElement>(e => e is null)
                .Or<Exception>()
                .WaitAndRetry(5, retryAttempt => TimeSpan.FromSeconds(1));
            _retryMetadataPolicy = Policy
                .HandleResult<GameJoltGameMetadata>(e => e is null)
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

            GetMetadata(game, metadata, gameId);

            return metadata;
        }

        private void GetMetadata(Game game, GameMetadata metadata, int gameId)
        {
            // prefer reading meta data online because it contains more information
            try
            {
                using var webView = _playniteAPI.WebViews.CreateOffscreenView();
                var accountClient = new GameJoltAccountClient(webView);

                GameJoltGameMetadata onlineGameMetadata;

                if (accountClient.GetIsUserLoggedIn())
                {
                    onlineGameMetadata = GetOnlineMetadataUsingWebView(game.GameId, webView);
                }
                else
                {
                    onlineGameMetadata = GetOnlineMetadata(game.GameId);
                }

                ApplyMetadata(game, metadata, onlineGameMetadata);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Could not get online metadata.");

                if (game.IsInstalled)
                {
                    var installedGamesMetadata = InstalledGamesProvider.GetGamesMetadata(_logger);

                    if (installedGamesMetadata.TryGetValue(gameId, out var installedGameMetadata))
                    {
                        ApplyMetadata(game, metadata, installedGameMetadata);
                    }
                }
            }
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
            metadata.Description = GetDescription(game.Name, gameJoltMetadata.StorePageLink);

            metadata.Links.Add(new Link("Game Jolt Store Page", gameJoltMetadata.StorePageLink));
        }


        private GameJoltGameMetadata GetOnlineMetadata(string gameId)
        {
            string gameDiscoverUrl = $"https://gamejolt.com/site-api/web/discover/games/{gameId}";

            using var http = new HttpClient();

            var metaData = _retryMetadataPolicy.Execute(() =>
            {
                var result = http.GetAsync(gameDiscoverUrl).GetAwaiter().GetResult();

                var resultObj = Serialization.FromJsonStream<GameJoltWebResult<LibraryGameResultPayload>>(result.Content.ReadAsStreamAsync().GetAwaiter().GetResult());
                return resultObj.Payload.Game;
            });

            return metaData;
        }

        private GameJoltGameMetadata GetOnlineMetadataUsingWebView(string gameId, IWebView webView)
        {
            string gameDiscoverUrl = $"https://gamejolt.com/site-api/web/discover/games/{gameId}";

            var metaData = _retryMetadataPolicy.Execute(() =>
            {
                webView.NavigateAndWait(gameDiscoverUrl);
                var stringContent = webView.GetPageText();
                var resultObj = Serialization.FromJson<GameJoltWebResult<LibraryGameResultPayload>>(stringContent);
                return resultObj.Payload.Game;
            });

            return metaData;
        }

        public string GetDescription(string gameName, string storePage, IBrowsingContext browsingContext)
        {
            try
            {
                IElement descriptionElement = null;

                using (var webView = _playniteAPI.WebViews.CreateOffscreenView(new WebViewSettings { JavaScriptEnabled = true }))
                {
                    webView.NavigateAndWait(storePage);
                    descriptionElement = _retryDescriptionPolicy.Execute(() => GetDescriptionFromWebView(storePage, browsingContext, webView));
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
