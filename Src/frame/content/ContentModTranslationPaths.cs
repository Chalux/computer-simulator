namespace KemoCard.Frame.Content;

public static class ContentModTranslationPaths
{
    public const string TranslationsFolderName = "translations";

    public static string GetDirectory(DiscoveredModEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return ContentModPath.Combine(
            ContentModPath.Combine(entry.FolderPath, entry.Manifest.ContentRoot),
            TranslationsFolderName);
    }
}