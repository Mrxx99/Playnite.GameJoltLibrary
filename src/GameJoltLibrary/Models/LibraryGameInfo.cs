using System.Collections.Generic;
using Playnite.SDK.Data;

namespace GameJoltLibrary.Models;

internal class GameJoltWebResult<TPayload>
{
    [SerializationPropertyName("payload")]
    public TPayload Payload { get; set; }
}

internal class LibraryGamesResultPayload
{
    [SerializationPropertyName("games")]
    public List<GameJoltGameMetadata> Games { get; set; }
}

internal class LibraryGameResultPayload
{
    [SerializationPropertyName("game")]
    public GameJoltGameMetadata Game { get; set; }
}

internal class LibraryPlaylistsPayload
{
    [SerializationPropertyName("collections")]
    public List<GameJoltPlaylist> Playlists { get; set; }
}

public class GameJoltPlaylist
{
    
    [SerializationPropertyName("id")]
    public string Id { get; set; }
    
    [SerializationPropertyName("name")]
    public string Name { get; set; }
    
    [SerializationPropertyName("slug")]
    public string Slug { get; set; }
}