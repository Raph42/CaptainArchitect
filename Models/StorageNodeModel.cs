using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;

namespace CaptainArchitect.Models;

/// <summary>
/// Nœud de stockage/relais dédié à une ressource spécifique.
/// Sert de collecteur/distributeur pour centraliser les tuyaux/convoyeurs.
/// </summary>
public class StorageNodeModel : NodeModel
{
    /// <summary>
    /// Identifiant de la ressource contenue dans ce stockage (ex: "IronOre", "Water", etc.).
    /// </summary>
    public string ProductId { get; set; }

    /// <summary>
    /// Port d'entrée unique (gauche) où convergent les productions en amont.
    /// </summary>
    public ResourcePortModel InputPort { get; }

    /// <summary>
    /// Port de sortie unique (droite) distribuant la ressource aux consommateurs en aval.
    /// </summary>
    public ResourcePortModel OutputPort { get; }

    /// <summary>
    /// Débit total reçu en entrée (somme des flux des machines ou stockages en amont).
    /// </summary>
    public decimal TotalInputRate { get; set; } = 0m;

    /// <summary>
    /// Débit total distribué aux consommateurs en aval.
    /// </summary>
    public decimal DistributedOutputRate { get; set; } = 0m;

    /// <summary>
    /// Demande totale cumulée demandée par les machines reliées en aval.
    /// </summary>
    public decimal DownstreamDemandRate { get; set; } = 0m;

    public StorageNodeModel(string productId, Point? position = null) : base(position)
    {
        ProductId = productId;
        Title = $"Stockage ({productId})";

        // 1. Port d'entrée à gauche
        InputPort = new ResourcePortModel(productId, this, PortDirection.Input, PortAlignment.Left);
        AddPort(InputPort);

        // 2. Port de sortie à droite
        OutputPort = new ResourcePortModel(productId, this, PortDirection.Output, PortAlignment.Right);
        AddPort(OutputPort);
    }
}