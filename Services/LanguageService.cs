namespace CaptainArchitect.Services;

/// <summary>
/// Service centralisé pour la gestion de la version de l'application
/// et préparé pour l'internationalisation (i18n).
/// </summary>
public class LanguageService
{
    /// <summary>
    /// Version actuelle de l'application.
    /// </summary>
    public const string AppVersion = "1.0.2";

    /// <summary>
    /// Code de langue courante (par défaut "fr").
    /// </summary>
    public string CurrentLanguage { get; private set; } = "fr";

    /// <summary>
    /// Événement déclenché lors du changement de langue pour forcer le rafraîchissement des vues.
    /// </summary>
    public event Action? OnLanguageChanged;

    /// <summary>
    /// Modifie la langue active et notifie les composants abonnés.
    /// </summary>
    /// <param name="langCode">Code de langue (ex: "fr", "en")</param>
    public void SetLanguage(string langCode)
    {
        if (CurrentLanguage != langCode)
        {
            CurrentLanguage = langCode;
            // Déclenche l'événement pour mettre à jour l'interface
            OnLanguageChanged?.Invoke();
        }
    }
}