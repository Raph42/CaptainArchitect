// Gère l'extraction de la durée depuis MachineBindings,
// le débit de base et le calcul proportionnel au multiplicateur

using System.Globalization;
using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;
using Blazor.Diagrams.Core.Anchors;

namespace CaptainArchitect.Models;

public class MachineNodeModel : NodeModel
{
    public Recipe Recipe { get; }
    public decimal Multiplier { get; set; } = 1.0m;
    public decimal DurationSeconds { get; }

    // Débits réels calculés (Clé = ResourcePortModel.Id)
    public Dictionary<string, decimal> ActualInputRates { get; } = new();
    public Dictionary<string, decimal> DistributedOutputRates { get; } = new();
    public Dictionary<string, decimal> DownstreamDemandRates { get; } = new();

    //public decimal GetDownstreamDemandRate(string portId) =>
    //    DownstreamDemandRates.TryGetValue(portId, out var val) ? val : 0m;

    // Taux de fonctionnement effectif (entre 0.0 et 1.0)
    public decimal OperationalEfficiency { get; set; } = 1.0m;

    // Production réelle effective bridée par le manque d'ingrédients
    //public decimal GetActualProducedRatePerMin(RecipeItem item) =>
    //    GetOutputRatePerMin(item) * OperationalEfficiency;


    public MachineNodeModel(Recipe recipe, Point? position = null) : base(position)
    {
        Recipe = recipe;
        Title = recipe.RecipeId;

        // Extraction et conversion sécurisée de DurationSeconde
        var binding = recipe.MachineBindings?.FirstOrDefault();

        if (binding != null && decimal.TryParse(binding.DurationSeconde, NumberStyles.Any, CultureInfo.InvariantCulture, out var duration) && duration > 0)
        {
            DurationSeconds = duration;
        }
        else
        {
            DurationSeconds = 20m;
            Console.Error.WriteLine($"[Avertissement] La recette '{recipe.RecipeId}' n'a pas de durée valide définie.");
        }

        // Entrées à gauche : on passe l'ID, le parent (this), et l'alignement
        foreach (var input in recipe.Inputs)
        {
            AddPort(new ResourcePortModel(input.ProductId, this, PortDirection.Input, PortAlignment.Left));
        }

        // Sorties à droite : on passe l'ID, le parent (this), et l'alignement
        foreach (var output in recipe.Outputs)
        {
            AddPort(new ResourcePortModel(output.ProductId, this, PortDirection.Output, PortAlignment.Right));
        }

    }


    // Débit unitaire nominal (pour 1 machine)
    public decimal GetBaseInputRatePerMin(RecipeItem item) =>
        DurationSeconds > 0 ? (item.Quantity / DurationSeconds) * 60m : 0m;

    public decimal GetBaseOutputRatePerMin(RecipeItem item) =>
        DurationSeconds > 0 ? (item.Quantity / DurationSeconds) * 60m : 0m;

    // Débit théorique total (selon Multiplier)
    public decimal GetInputRatePerMin(RecipeItem item) =>
        GetBaseInputRatePerMin(item) * Multiplier;

    public decimal GetOutputRatePerMin(RecipeItem item) =>
        GetBaseOutputRatePerMin(item) * Multiplier;

    // Débit réellement produit (bridé par l'efficacité d'approvisionnement)
    public decimal GetActualProducedRatePerMin(RecipeItem item) =>
        GetOutputRatePerMin(item) * OperationalEfficiency;

    public decimal GetActualInputRate(string portId) =>
        ActualInputRates.TryGetValue(portId, out var val) ? val : 0m;

    public decimal GetDistributedOutputRate(string portId) =>
        DistributedOutputRates.TryGetValue(portId, out var val) ? val : 0m;

    public decimal GetDownstreamDemandRate(string portId) =>
        DownstreamDemandRates.TryGetValue(portId, out var val) ? val : 0m;


}