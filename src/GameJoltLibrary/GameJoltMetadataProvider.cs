using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Dom.Html;
using AngleSharp.Extensions;
using Playnite.SDK;
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
            };

            var installedGamesMetadata = InstalledGamesProvider.GetGamesMetadata(_logger);

            bool isIdParsable = int.TryParse(game.GameId, NumberStyles.Integer, CultureInfo.InvariantCulture, out int gameId);

            if (isIdParsable && installedGamesMetadata.TryGetValue(gameId, out var installedGameMetadata))
            {
                // Cover image
                if (!string.IsNullOrEmpty(installedGameMetadata.ThumbnailMediaItem?.ImgUrl?.AbsolutePath))
                {
                    metadata.CoverImage = new MetadataFile(installedGameMetadata.ThumbnailMediaItem.ImgUrl.AbsoluteUri);
                }

                // Background image
                if (!string.IsNullOrEmpty(installedGameMetadata.HeaderMediaItem?.ImgUrl?.AbsolutePath))
                {
                    metadata.BackgroundImage = new MetadataFile(installedGameMetadata.HeaderMediaItem.ImgUrl.AbsoluteUri);
                }

                // Developer
                if (!string.IsNullOrEmpty(installedGameMetadata.Developer?.DisplayName))
                {
                    metadata.Developers.Add(new MetadataNameProperty(installedGameMetadata.Developer.DisplayName));
                }

                string storePage = $"https://gamejolt.com/games/{installedGameMetadata.Slug}/{installedGameMetadata.Id}";

                // Description
                metadata.Description = GetDescription(game, storePage);

                metadata.Links.Add(new Link("Game Jolt Store Page", storePage));
            }

            return metadata;
        }

        private string GetDescription(Game game, string storePage)
        {
            try
            {
                var config = Configuration.Default.WithDefaultLoader();
                var context = BrowsingContext.New(config);
                IElement descriptionElement = null;

                using (var webView = _gameJoltLibrary.PlayniteApi.WebViews.CreateOffscreenView(new WebViewSettings { JavaScriptEnabled = true }))
                {
                    webView.NavigateAndWait(storePage);
                    descriptionElement = _retryPolicy.Execute(() => GetDescriptionFromWebView(storePage, context, webView));
                }

                if (descriptionElement is null)
                {
                    descriptionElement = GetDescriptionFromGoogleCache(storePage, context);
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
                _logger.Error(ex, $"Failed to get description for game {game.Name}");
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
                AcceptMaturityWarning(webView, storePage, _logger);
            }

            return descriptionElement;
        }

        public static void AcceptMaturityWarning(IWebView webView, string storePage, ILogger logger)
        {
            try
            {
                webView.EvaluateScriptAsync(@"this.document.getElementsByClassName(""-primary"")[0].click()").GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"Could not accept maturity warning for game {storePage}");
            }
        }
    }
}
