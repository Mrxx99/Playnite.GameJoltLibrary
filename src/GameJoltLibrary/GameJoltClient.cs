using System.IO;
using Playnite.SDK;

namespace GameJoltLibrary
{
    public class GameJoltClient : LibraryClient
    {
        public override string Icon => GameJolt.Icon;

        public override bool IsInstalled => GameJolt.IsInstalled;

        public override void Open()
        {
            GameJolt.StartClient();
        }
    }
}