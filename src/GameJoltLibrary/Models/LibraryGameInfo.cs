using System.Collections.Generic;
using Playnite.SDK.Data;

namespace GameJoltLibrary.Models;

internal class LibraryGamesResult
{
    [SerializationPropertyName("payload")]
    public LibraryGamesResultPayload Payload { get; set; }
}

internal class LibraryGamesResultPayload
{
    [SerializationPropertyName("games")]
    public List<GameJoltGameMetadata> Games { get; set; }
}

internal class LibraryGameInfo
{
    [SerializationPropertyName("id")]
    public long Id { get; set; }

    [SerializationPropertyName("slug")]
    public string Slug { get; set; }

    [SerializationPropertyName("title")]
    public string Title { get; set; }

    [SerializationPropertyName("img_thumbnail")]
    public string Image { get; set; }

    [SerializationPropertyName("developer")]
    public Developer Developer { get; set; }

    public string Link => $"https://gamejolt.com/games/{Slug}/{Id}";
}