using Playnite.SDK;

namespace GameJoltLibrary
{
    public class GameJoltClient : LibraryClient
    {
        public override bool IsInstalled => true;

        public override void Open()
        {
            GameJolt.StartClient();
        }
    }
}