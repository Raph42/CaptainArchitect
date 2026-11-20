using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;

namespace CaptainArchitect.Models;

/// <summary>
/// Sens du portail inter-onglets :
/// - Export : Reçoit une ressource dans l'onglet courant et l'envoie vers le bus global.
/// - Import : Récupère une ressource depuis un portail Export d'un autre onglet et l'injecte ici.
/// </summary>
public enum PortalDirection
{
    Export,
    Import
}

/// <summary>
/// Nœud de liaison inter-onglets (Approche Bus / Catalogue).
/// </summary>
public class PortalNodeModel : NodeModel
{
    /// <summary>
    /// Sens du portail (Export ou Import).
    /// </summary>
    public PortalDirection Direction { get; }

    /// <summary>
    /// Identifiant de la ressource transportée (ex: "IronPlate", "Water").
    /// </summary>
    public string ProductId { get; set; }

    /// <summary>
    /// Nom du canal sur le bus (ex: "Fer -> Usine principale").
    /// Pertinent surtout pour les portails Export.
    /// </summary>
    public string ChannelName { get; set; }

    /// <summary>
    /// Pour un portail Import : Id du PortalNodeModel Export source auquel il est abonné.
    /// </summary>
    public string? SourceExportPortalId { get; set; }

    /// <summary>
    /// Port unique du portail :
    /// - Si Export : Port Input à gauche (reçoit depuis l'onglet producteur).
    /// - Si Import : Port Output à droite (distribue dans l'onglet consommateur).
    /// </summary>
    public ResourcePortModel Port { get; }

    /// <summary>
    /// Débit total entrant dans le portail (reçu des machines en amont pour un Export, ou reçu du bus pour un Import).
    /// </summary>
    public decimal TotalInputRate { get; set; } = 0m;

    /// <summary>
    /// Débit effectivement distribué en sortie (envoyé au bus pour un Export, ou livré aux machines en aval pour un Import).
    /// </summary>
    public decimal DistributedOutputRate { get; set; } = 0m;

    /// <summary>
    /// Demande cumulée des consommateurs en aval (somme des demandes des imports liés pour un Export, ou demande locale pour un Import).
    /// </summary>
    public decimal DownstreamDemandRate { get; set; } = 0m;

    public PortalNodeModel(
        PortalDirection direction,
        string productId,
        string channelName,
        Point? position = null,
        string? sourceExportPortalId = null) : base(position)
    {
        Direction = direction;
        ProductId = productId;
        ChannelName = channelName;
        SourceExportPortalId = sourceExportPortalId;
        Title = direction == PortalDirection.Export ? $"Export ({channelName})" : $"Import ({channelName})";

        if (direction == PortalDirection.Export)
        {
            // L'Export reçoit la ressource à gauche (Input) pour l'envoyer sur le bus
            Port = new ResourcePortModel(productId, this, PortDirection.Input, PortAlignment.Left);
        }
        else
        {
            // L'Import injecte la ressource à droite (Output) depuis le bus vers l'onglet courant
            Port = new ResourcePortModel(productId, this, PortDirection.Output, PortAlignment.Right);
        }

        AddPort(Port);
    }
}