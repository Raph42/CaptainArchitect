namespace CaptainArchitect.Models;

/// <summary>
/// Structure principale du fichier JSON de sauvegarde (Version 2.0 multi-onglets).
/// Conserve les champs racine pour la rétrocompatibilité avec les sauvegardes v1.x.
/// </summary>
public class DiagramSaveData
{
    // Version du format de sauvegarde au cas où la structure évolue
    public string Version { get; set; } = "2.0";

    // Liste des onglets de l'usine (ex: "Iron", "Steel", etc.)
    public List<SavedTabData> Tabs { get; set; } = new();


    // --- Champs hérités (v1.x) conservés pour ouvrir les anciennes sauvegardes ---
    //
    // Liste des machines posées sur le diagramme
    public List<SavedNodeData> Nodes { get; set; } = new();

    // Liste des câbles/tuyaux reliant les ports
    public List<SavedLinkData> Links { get; set; } = new();

    // Liste des stockages sauvegardés
    public List<SavedStorageData> Storages { get; set; } = new();
}


/// <summary>
/// Contenu sauvegardé d'un onglet individuel.
/// </summary>
public class SavedTabData
{
    public string TabId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    // Identifiant de la ressource utilisée comme icône de l'onglet (optionnel)
    public string? IconProductId { get; set; }

    public List<SavedNodeData> Nodes { get; set; } = new();
    public List<SavedStorageData> Storages { get; set; } = new();
    public List<SavedPortalData> Portals { get; set; } = new();
    public List<SavedLinkData> Links { get; set; } = new();
}


/// <summary>
/// Représentation sérialisée d'un portail inter-onglets (Export ou Import).
/// </summary>
public class SavedPortalData
{
    public string NodeId { get; set; } = string.Empty;
    public PortalDirection Direction { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public string? SourceExportPortalId { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
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

    // Ordre personnalisé des entrées (liste ordonnée des ProductId)
    public List<string> OrderedInputProductIds { get; set; } = new();

    // Ordre personnalisé des sorties (liste ordonnée des ProductId)
    public List<string> OrderedOutputProductIds { get; set; } = new();
}


/// <summary>
/// Représentation sérialisée d'un nœud de stockage.
/// </summary>
public class SavedStorageData
{
    // Identifiant unique du nœud de stockage
    public string NodeId { get; set; } = string.Empty;

    // Identifiant de la ressource stockée (ex: "IronOre", "Water")
    public string ProductId { get; set; } = string.Empty;

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

    // Coordonnées X, Y des points de passage éventuels
    public List<double[]> Vertices { get; set; } = new();

}

