using System;
using System.Collections.Generic;
using Playnite.SDK.Data;

namespace GameJoltLibrary.Models
{

    public class InstalledGamesMetadata
    {
        [SerializationPropertyName("version")]
        public long Version { get; set; }

        [SerializationPropertyName("objects")]
        public Dictionary<long, GameJoltGameMetadata> Objects { get; set; }
    }

    public class GameJoltGameMetadata
    {
        [SerializationPropertyName("id")]
        public string Id { get; set; }

        [SerializationPropertyName("title")]
        public string Title { get; set; }

        [SerializationPropertyName("slug")]
        public string Slug { get; set; }

        [SerializationPropertyName("modified_on")]
        public long ModifiedOn { get; set; }

        [SerializationPropertyName("published_on")]
        public long? PublishedOn { get; set; }

        [SerializationPropertyName("compatibility")]
        public Compatibility Compatibility { get; set; }

        [SerializationPropertyName("header_media_item")]
        public MediaItem HeaderMediaItem { get; set; }

        [SerializationPropertyName("thumbnail_media_item")]
        public MediaItem ThumbnailMediaItem { get; set; }

        [SerializationPropertyName("img_thumbnail")]
        public Uri ImageThumbnail { get; set; }

        [SerializationPropertyName("developer")]
        public Developer Developer { get; set; }

        [SerializationPropertyName("category")]
        public string Category { get; set; }

        [SerializationPropertyName("description_content")]
        public string DescriptionJson { get; set; }

        public string StorePageLink => $"https://gamejolt.com/games/{Slug}/{Id}";
    }

    public partial class Compatibility
    {
        [SerializationPropertyName("id")]
        public long Id { get; set; }

        [SerializationPropertyName("game_id")]
        public long GameId { get; set; }

        [SerializationPropertyName("os_windows")]
        public bool OsWindows { get; set; }

        [SerializationPropertyName("os_windows_64")]
        public bool? OsWindows64 { get; set; }

        [SerializationPropertyName("os_mac")]
        public bool? OsMac { get; set; }

        [SerializationPropertyName("os_mac_64")]
        public bool? OsMac64 { get; set; }

        [SerializationPropertyName("os_linux")]
        public bool? OsLinux { get; set; }

        [SerializationPropertyName("os_linux_64")]
        public bool? OsLinux64 { get; set; }

        [SerializationPropertyName("os_other")]
        public bool? OsOther { get; set; }
    }

    public class Developer
    {
        [SerializationPropertyName("id")]
        public long Id { get; set; }

        [SerializationPropertyName("username")]
        public string UserName { get; set; }

        [SerializationPropertyName("name")]
        public string Name { get; set; }

        [SerializationPropertyName("display_name")]
        public string DisplayName { get; set; }

        [SerializationPropertyName("slug")]
        public string Slug { get; set; }

        [SerializationPropertyName("img_avatar")]
        public Uri ImgAvatar { get; set; }

        public string DeveloperLink => $"https://gamejolt.com/@{Slug}";
    }

    public class MediaItem
    {
        [SerializationPropertyName("file")]
        public object File { get; set; }

        [SerializationPropertyName("_removed")]
        public bool Removed { get; set; }

        [SerializationPropertyName("_progress")]
        public object Progress { get; set; }

        [SerializationPropertyName("id")]
        public long Id { get; set; }

        [SerializationPropertyName("type")]
        public string Type { get; set; }

        [SerializationPropertyName("parent_id")]
        public long ParentId { get; set; }

        [SerializationPropertyName("hash")]
        public string Hash { get; set; }

        [SerializationPropertyName("filename")]
        public string Filename { get; set; }

        [SerializationPropertyName("filetype")]
        public string Filetype { get; set; }

        [SerializationPropertyName("is_animated")]
        public bool IsAnimated { get; set; }

        [SerializationPropertyName("width")]
        public long Width { get; set; }

        [SerializationPropertyName("height")]
        public long Height { get; set; }

        [SerializationPropertyName("filesize")]
        public long Filesize { get; set; }

        [SerializationPropertyName("crop_start_x")]
        public long? CropStartX { get; set; }

        [SerializationPropertyName("crop_start_y")]
        public long? CropStartY { get; set; }

        [SerializationPropertyName("crop_end_x")]
        public long? CropEndX { get; set; }

        [SerializationPropertyName("crop_end_y")]
        public long? CropEndY { get; set; }

        [SerializationPropertyName("avg_img_color")]
        public string AvgImgColor { get; set; }

        [SerializationPropertyName("img_has_transparency")]
        public bool ImgHasTransparency { get; set; }

        [SerializationPropertyName("added_on")]
        public long AddedOn { get; set; }

        [SerializationPropertyName("status")]
        public string Status { get; set; }

        [SerializationPropertyName("img_url")]
        public Uri ImgUrl { get; set; }

        [SerializationPropertyName("mediaserver_url")]
        public Uri MediaserverUrl { get; set; }

        [SerializationPropertyName("mediaserver_url_webm")]
        public Uri MediaserverUrlWebm { get; set; }

        [SerializationPropertyName("mediaserver_url_mp4")]
        public Uri MediaserverUrlMp4 { get; set; }
    }
}
