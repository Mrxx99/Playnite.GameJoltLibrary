using System;
using Playnite.SDK;
using Playnite.SDK.Plugins;

namespace GameJoltLibrary.Helpers;

public static class NotificationExtensions
{
    public static string ImportErrorMessageId { get; } = "GameJolt_libImportError";
    public static string ImportErrorMessageFromApiId { get; } = "GameJolt_libImportErrorFromApi";
    public static string UserNotFoundErrorMessageId { get; } = "GameJolt_UserNotFoundError";

    public static void NotifyUserNotFound(this INotificationsAPI notifications, string userName, LibraryPlugin plugin)
    {
        notifications.Add(new NotificationMessage(
                        UserNotFoundErrorMessageId,
                        string.Format(plugin.PlayniteApi.Resources.GetString("LOCLibraryImportError"), plugin.Name) + Environment.NewLine +
                        string.Format(plugin.PlayniteApi.Resources.GetString("LOCGameJoltUserNotFoundError"), userName),
                        NotificationType.Error,
                        () => plugin.OpenSettingsView()));
    }

    public static void RemoveUserNotFound(this INotificationsAPI notifications)
    {
        notifications.Remove(UserNotFoundErrorMessageId);
    }

    public static void NotifyImportError(this INotificationsAPI notifications, Exception importError, LibraryPlugin plugin)
    {
        notifications.Add(new NotificationMessage(
                        ImportErrorMessageId,
                        string.Format(plugin.PlayniteApi.Resources.GetString("LOCLibraryImportError"), plugin.Name) +
                        Environment.NewLine + importError.Message,
                        NotificationType.Error,
                        () => plugin.OpenSettingsView()));
    }

    public static void RemoveImportError(this INotificationsAPI notifications)
    {
        notifications.Remove(ImportErrorMessageId);
    }

    public static void NotifyImportErrorFromApi(this INotificationsAPI notifications, Exception importError, LibraryPlugin plugin)
    {
        notifications.Add(new NotificationMessage(
                        ImportErrorMessageFromApiId,
                        string.Format(plugin.PlayniteApi.Resources.GetString("LOCLibraryImportError"), plugin.Name) +
                        Environment.NewLine + importError.Message + " " + plugin.PlayniteApi.Resources.GetString("LOCGameJoltTryAuthenticate"),
                        NotificationType.Error,
                        () => plugin.OpenSettingsView()));
    }

    public static void RemoveImportErrorFromApi(this INotificationsAPI notifications)
    {
        notifications.Remove(ImportErrorMessageId);
    }
}
