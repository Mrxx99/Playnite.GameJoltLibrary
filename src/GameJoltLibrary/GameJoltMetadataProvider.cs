using System.Collections.Generic;
using System.Linq;
using AngleSharp;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace GameJoltLibrary
{
    public class GameJoltMetadataProvider : LibraryMetadataProvider
    {
        private GameJoltLibrary _gameJoltLibrary;

        public GameJoltMetadataProvider(GameJoltLibrary gameJoltLibrary)
        {
            _gameJoltLibrary = gameJoltLibrary;
        }

        public override GameMetadata GetMetadata(Game game)
        {
            var metadata = new GameMetadata
            {
                Links = new List<Link>(),
                Tags = new HashSet<MetadataProperty>(),
                Genres = new HashSet<MetadataProperty>(),
                Features = new HashSet<MetadataProperty>()
            };

            var installedGamesMetadata = GameJoltLibrary.GetGamesMetadata();

            int gameId;

            int.TryParse(game.GameId, out gameId);

            if (installedGamesMetadata.Objects.ContainsKey(gameId))
            {
                var installedGameMetadata = installedGamesMetadata.Objects[gameId];

                // Cover image
                if (!string.IsNullOrEmpty(installedGameMetadata.ThumbnailMediaItem.ImgUrl.AbsolutePath))
                {
                    metadata.CoverImage = new MetadataFile(installedGameMetadata.ThumbnailMediaItem.ImgUrl.AbsoluteUri);
                }

                // Background image
                if (!string.IsNullOrEmpty(installedGameMetadata.HeaderMediaItem.ImgUrl.AbsolutePath))
                {
                    metadata.BackgroundImage = new MetadataFile(installedGameMetadata.HeaderMediaItem.ImgUrl.AbsoluteUri);
                }

                // Description
                string storePageUrlEncoded = $"https%3A%2F%2Fgamejolt.com%2Fgames%2F{installedGameMetadata.Slug}%2F{installedGameMetadata.Id}";

                string storePageInGoogleChacheUrl = $"http://webcache.googleusercontent.com/search?q=cache%3A{storePageUrlEncoded}";

                var config = Configuration.Default.WithDefaultLoader();
                var address = storePageInGoogleChacheUrl;
                var context = BrowsingContext.New(config);
                var document = context.OpenAsync(address).GetAwaiter().GetResult();
                var descriptionElement = document.QuerySelector(".game-description-content");
                string description = descriptionElement.InnerHtml;
                metadata.Description = description;

                metadata.Links.Add(new Link("Game Jolt Store Page", $"https://gamejolt.com/games/{installedGameMetadata.Slug}/{gameId}"));
            }

            return metadata;
        }
    }
}
