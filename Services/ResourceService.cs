// Classe pour rendre la liste des ressources accessible depuis n'importe quel composant
// (évitant d'avoir à passer le dictionnaire de composant en composant)

using CaptainArchitect.Models;

namespace CaptainArchitect.Services;

public class ResourceService
{
    private readonly Dictionary<string, Product> _products = new();
    private readonly Dictionary<string, Machine> _machines = new();

    // 1. Rend la direction nullable (?): si null, c'est un clic depuis le panneau de gauche
    public event Action<string, PortDirection?>? OnResourceFilterRequested;


    // 2. direction a une valeur par défaut null
    public void SelectFilterResource(string productId, PortDirection? direction = null)
    {
        if (!string.IsNullOrWhiteSpace(productId))
        {
            OnResourceFilterRequested?.Invoke(productId, direction);
        }
    }

        
    // Chargement des produits
    public void LoadProducts(IEnumerable<Product> products)
    {
        _products.Clear();
        foreach (var p in products)
        {
            _products[p.Id] = p;
        }
    }


    // Chargement des machines
    public void LoadMachines(IEnumerable<Machine> machines)
    {
        _machines.Clear();
        foreach (var m in machines)
        {
            _machines[m.Id] = m;
        }
    }


    public Product? GetProduct(string productId) =>
        _products.TryGetValue(productId, out var prod) ? prod : null;

    public Machine? GetMachine(string machineId) =>
        _machines.TryGetValue(machineId, out var machine) ? machine : null;


    /// <summary>
    /// Total d'ouvriers requis par l'ensemble des machines posées.
    /// </summary>
    public int TotalWorkers { get; private set; } = 0;


    /// <summary>
    /// Consommation électrique totale en kW de l'ensemble des machines posées.
    /// </summary>
    public decimal TotalPowerKw { get; private set; } = 0m;


    /// <summary>
    /// Événement déclenché lors du recalcul des statistiques globales de l'usine.
    /// </summary>
    public event Action? OnGlobalStatsChanged;


    /// <summary>
    /// Met à jour les statistiques globales et avertit l'en-tête (MainLayout).
    /// </summary>
    /// <param name="workers">Total des ouvriers</param>
    /// <param name="powerKw">Total d'électricité en kW</param>
    public void UpdateGlobalStats(int workers, decimal powerKw)
    {
        TotalWorkers = workers;
        TotalPowerKw = powerKw;
        // Notifie les composants abonnés (comme MainLayout)
        OnGlobalStatsChanged?.Invoke();
    }


    /// <summary>
    /// Version de l'extracteur du JSON (_generator).
    /// </summary>
    public string? JsonGeneratorVersion { get; private set; }

    /// <summary>
    /// Version du jeu (gameVersion).
    /// </summary>
    public string? GameVersion { get; private set; }

    /// <summary>
    /// Enregistre les métadonnées de version du JSON et notifie l'interface.
    /// </summary>
    public void SetVersions(string? generator, string? gameVersion)
    {
        JsonGeneratorVersion = generator;
        GameVersion = gameVersion;
        OnGlobalStatsChanged?.Invoke(); // Notifie MainLayout pour réactualiser l'en-tête
    }


}