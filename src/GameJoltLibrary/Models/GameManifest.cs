using Playnite.SDK.Data;

namespace GameJoltLibrary.Models;

public class GameManifest
{
    [SerializationPropertyName("version")]
    public string Version { get; set; }

    [SerializationPropertyName("gameInfo")]
    public GameManifestGameInfo GameInfo { get; set; }

    [SerializationPropertyName("launchOptions")]
    public GameManifestLaunchOptions LaunchOptions { get; set; }

    [SerializationPropertyName("os")]
    public string Os { get; set; }

    [SerializationPropertyName("arch")]
    public string Arch { get; set; }

    [SerializationPropertyName("isFirstInstall")]
    public bool? IsFirstInstall { get; set; }
}

public class GameManifestGameInfo
{
    [SerializationPropertyName("dir")]
    public string Dir { get; set; }

    [SerializationPropertyName("uid")]
    public string Uid { get; set; }

    [SerializationPropertyName("archiveFiles")]
    public string[] ArchiveFiles { get; set; }

    [SerializationPropertyName("platformUrl")]
    public string PlatformUrl { get; set; }
}

public class GameManifestLaunchOptions
{
    [SerializationPropertyName("executable")]
    public string Executable { get; set; }

}