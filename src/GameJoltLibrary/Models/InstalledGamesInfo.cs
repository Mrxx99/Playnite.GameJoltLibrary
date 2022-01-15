using System;
using System.Collections.Generic;
using Playnite.SDK.Data;

namespace GameJoltLibrary
{
    public class InstalledGamesInfo
    {
        [SerializationPropertyName("version")]
        public long Version { get; set; }

        [SerializationPropertyName("objects")]
        public Dictionary<string, InstalledGameInfo> Objects { get; set; }

        [SerializationPropertyName("groups")]
        public Groups Groups { get; set; }
    }

    public class Groups
    {
        [SerializationPropertyName("game_id")]
        public Dictionary<string, long[]> GameId { get; set; }
    }

    public class InstalledGameInfo
    {
        [SerializationPropertyName("id")]
        public long Id { get; set; }

        [SerializationPropertyName("install_dir")]
        public string InstallDir { get; set; }

        [SerializationPropertyName("install_state")]
        public object InstallState { get; set; }

        [SerializationPropertyName("update_state")]
        public object UpdateState { get; set; }

        [SerializationPropertyName("remove_state")]
        public string RemoveState { get; set; }

        [SerializationPropertyName("update")]
        public object Update { get; set; }

        [SerializationPropertyName("download_progress")]
        public object DownloadProgress { get; set; }

        [SerializationPropertyName("unpack_progress")]
        public object UnpackProgress { get; set; }

        [SerializationPropertyName("patch_paused")]
        public object PatchPaused { get; set; }

        [SerializationPropertyName("patch_queued")]
        public object PatchQueued { get; set; }

        [SerializationPropertyName("run_state")]
        public object RunState { get; set; }

        [SerializationPropertyName("running_pid")]
        public object RunningPid { get; set; }

        [SerializationPropertyName("game_id")]
        public long GameId { get; set; }

        [SerializationPropertyName("title")]
        public string Title { get; set; }

        [SerializationPropertyName("description")]
        public string Description { get; set; }

        [SerializationPropertyName("release")]
        public Release Release { get; set; }

        [SerializationPropertyName("launch_options")]
        public LaunchOption[] LaunchOptions { get; set; }
    }

    public class LaunchOption
    {
        [SerializationPropertyName("id")]
        public long Id { get; set; }

        [SerializationPropertyName("os")]
        public string Os { get; set; }

        [SerializationPropertyName("executable_path")]
        public string ExecutablePath { get; set; }

        public bool IsValid() => !string.IsNullOrEmpty(ExecutablePath) && !string.IsNullOrEmpty(Os);
    }

    public class Release
    {
        [SerializationPropertyName("id")]
        public long Id { get; set; }

        [SerializationPropertyName("version_number")]
        public string VersionNumber { get; set; }
    }

}
