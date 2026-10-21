// Classe pour rendre la liste des ressources accessible depuis n'importe quel composant
// (évitant d'avoir à passer le dictionnaire de composant en composant)

using CaptainArchitect.Models;

namespace CaptainArchitect.Services;

public class ResourceService
{
    private readonly Dictionary<string, Product> _products = new();
    private readonly Dictionary<string, Machine> _machines = new();

    // Événement recevant l'Id du produit et la direction du port cliqué
    public event Action<string, PortDirection>? OnResourceFilterRequested;


    // Déclenché au clic : transmet le ProductId et sa direction (Input / Output)
    public void SelectFilterResource(string productId, PortDirection direction)
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

}