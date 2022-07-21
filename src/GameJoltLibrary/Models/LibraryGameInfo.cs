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