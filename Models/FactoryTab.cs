using Blazor.Diagrams;

namespace CaptainArchitect.Models;

/// <summary>
/// Représente un onglet d'usine (ex: "Iron", "Steel", "Oil") avec son propre diagramme indépendant.
/// </summary>
public class FactoryTab
{
    /// <summary>
    /// Identifiant unique de l'onglet (utile pour relier les portails entre onglets).
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Nom affiché dans la barre d'onglets (ex: "Iron").
    /// </summary>
    public string Name { get; set; } = "Factory";

    /// <summary>
    /// Identifiant optionnel du produit/ressource dont l'icône est affichée sur l'onglet (ex: "Iron", "Steel").
    /// Null si l'onglet n'a pas d'icône attribuée.
    /// </summary>
    public string? IconProductId { get; set; }

    /// <summary>
    /// Instance BlazorDiagram propre à cet onglet (conserve zoom, pan, machines, stockages et liens).
    /// </summary>
    public BlazorDiagram Diagram { get; set; } = default!;
}