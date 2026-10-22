namespace CaptainArchitect.Models;

/// <summary>
/// Structure principale du fichier de sauvegarde du diagramme.
/// </summary>
public class DiagramSaveData
{
    // Version du format de sauvegarde au cas où la structure évolue
    public string Version { get; set; } = "1.0";

    // Liste des machines posées sur le diagramme
    public List<SavedNodeData> Nodes { get; set; } = new();

    // Liste des câbles/tuyaux reliant les ports
    public List<SavedLinkData> Links { get; set; } = new();
}

/// <summary>
/// Représentation d'une machine posée.
/// </summary>
public class SavedNodeData
{
    // Identifiant unique du nœud dans le diagramme pour rebrancher les liens
    public string NodeId { get; set; } = string.Empty;

    // Identifiant de la recette assignée
    public string RecipeId { get; set; } = string.Empty;

    // Identifiant de la machine actuelle (ex: AssemblyRoboticT1, AssemblyRoboticT2, etc.)
    public string MachineId { get; set; } = string.Empty;

    // Nombre de machines / multiplicateur
    public decimal Multiplier { get; set; } = 1.0m;

    // Coordonnées sur le canvas
    public double X { get; set; }
    public double Y { get; set; }
}


/// <summary>
/// Liaison identifiée précisément par le produit et la direction du port
/// </summary>
public class SavedLinkData
{
    // Nœud source (la machine qui produit)
    public string SourceNodeId { get; set; } = string.Empty;
    public string SourceProductId { get; set; } = string.Empty;
    public PortDirection SourceDirection { get; set; }

    // Nœud cible (la machine qui consomme)
    public string TargetNodeId { get; set; } = string.Empty;
    public string TargetProductId { get; set; } = string.Empty;
    public PortDirection TargetDirection { get; set; }
}