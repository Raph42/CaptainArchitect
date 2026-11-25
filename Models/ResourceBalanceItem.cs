namespace CaptainArchitect.Models;

/// <summary>
/// Représente le bilan global de production et de consommation pour une ressource donnée.
/// </summary>
public class ResourceBalanceItem
{
    /// <summary>
    /// Identifiant unique de la ressource (ex: "IronOre", "Steel").
    /// </summary>
    public string ProductId { get; set; } = string.Empty;

    /// <summary>
    /// Nom lisible de la ressource.
    /// </summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// Production brute totale générée par les machines actives (unités / minute).
    /// </summary>
    public decimal TotalProduced { get; set; } = 0m;

    /// <summary>
    /// Consommation totale requise par les machines actives (unités / minute).
    /// </summary>
    public decimal TotalConsumed { get; set; } = 0m;

    /// <summary>
    /// Bilan net : Production - Consommation.
    /// Positif = Surplus, Négatif = Déficit, Zéro = Équilibre parfait.
    /// </summary>
    public decimal NetBalance => TotalProduced - TotalConsumed;
}