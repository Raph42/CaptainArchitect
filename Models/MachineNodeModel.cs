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

    // État de pause de la machine
    public bool IsPaused { get; set; } = false;

    // Index de la machine actuellement sélectionnée dans Recipe.MachineBindings
    public int CurrentBindingIndex { get; private set; } = 0;

    // Durée de cycle pour la machine active
    public decimal DurationSeconds { get; private set; } = 20m;

    // Débits réels calculés
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


    // 1. CONSTRUCTEUR
    public MachineNodeModel(Recipe recipe, Point? position = null) : base(position)
    {
        //Recipe = recipe;

        // On clone la recette pour que les inversions de listes soient propres à cette machine posée
        Recipe = new Recipe(
            recipe.RecipeId,
            new List<RecipeItem>(recipe.Inputs),
            new List<RecipeItem>(recipe.Outputs),
            recipe.MachineBindings != null ? new List<MachineBinding>(recipe.MachineBindings) : new List<MachineBinding>()
        );

        Title = recipe.RecipeId;

        // Initialise la durée sur la première machine disponible
        UpdateDuration();

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



    // Récupère le MachineBinding actif
    public MachineBinding? CurrentBinding =>
        (Recipe.MachineBindings != null && Recipe.MachineBindings.Count > CurrentBindingIndex)
            ? Recipe.MachineBindings[CurrentBindingIndex]
            : Recipe.MachineBindings?.FirstOrDefault();

    // Identifiant de la machine actuelle
    public string CurrentMachineId => CurrentBinding?.MachineId ?? string.Empty;

    // Multiplicateur interne du binding (OutputMultiplier dans le JSON, vaut 1 par défaut)
    public decimal CurrentOutputMultiplier
    {
        get
        {
            var binding = CurrentBinding;

            if (binding != null && binding.OutputMultiplier > 0)
            {
                return (decimal)binding.OutputMultiplier;
            }
            return 1m;
        }
    }

    // Teste si on peut rétrograder (machine précédente)
    public bool CanDowngrade => CurrentBindingIndex > 0;

    // Teste si on peut améliorer (machine suivante)
    public bool CanUpgrade => Recipe.MachineBindings != null && CurrentBindingIndex < Recipe.MachineBindings.Count - 1;

    // Rétrograder la machine
    public bool Downgrade()
    {
        if (CanDowngrade)
        {
            CurrentBindingIndex--;
            UpdateDuration();
            Refresh();
            return true;
        }
        return false;
    }

    // Améliorer la machine
    public bool Upgrade()
    {
        if (CanUpgrade)
        {
            CurrentBindingIndex++;
            UpdateDuration();
            Refresh();
            return true;
        }
        return false;
    }

    // Met à jour la durée nominale d'après le binding actif
    private void UpdateDuration()
    {
        var binding = CurrentBinding;
        if (binding != null && decimal.TryParse(binding.DurationSeconde, NumberStyles.Any, CultureInfo.InvariantCulture, out var duration) && duration > 0)
        {
            DurationSeconds = duration;
        }
        else
        {
            DurationSeconds = 20m;
        }
    }



    // Débit unitaire nominal d'entrée (1 machine) : prend en compte DurationSeconds ET CurrentOutputMultiplier
    public decimal GetBaseInputRatePerMin(RecipeItem item) =>
        DurationSeconds > 0 ? (item.Quantity * CurrentOutputMultiplier / DurationSeconds) * 60m : 0m;

    // Débit unitaire nominal de sortie (1 machine) : prend en compte DurationSeconds ET CurrentOutputMultiplier
    public decimal GetBaseOutputRatePerMin(RecipeItem item) =>
        DurationSeconds > 0 ? (item.Quantity * CurrentOutputMultiplier / DurationSeconds) * 60m : 0m;

    // Débits théoriques totaux (avec Multiplier du bâtiment)
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



    // =========================================================================
    // GESTION DU RÉORDONNANCEMENT DES ENTRÉES (AVEC SUIVI DES LIAISONS)
    // =========================================================================

    /// <summary>
    /// Vérifie si l'entrée à l'index donné peut monter (index > 0).
    /// </summary>
    public bool CanMoveInputUp(int index) => index > 0;

    /// <summary>
    /// Vérifie si l'entrée à l'index donné peut descendre (index < Inputs.Count - 1).
    /// </summary>
    public bool CanMoveInputDown(int index) => index < Recipe.Inputs.Count - 1;


    /// <summary>
    /// Permute deux entrées consécutives dans la recette et répercute le changement sur les ports.
    /// Échange les coordonnées des ports pour que les câbles SVG soient repositionnés.
    /// </summary>
    public void SwapInputs(int indexA, int indexB)
    {
        // 1. Contrôle des bornes
        if (Recipe?.Inputs == null ||
            indexA < 0 || indexA >= Recipe.Inputs.Count ||
            indexB < 0 || indexB >= Recipe.Inputs.Count ||
            indexA == indexB)
        {
            return;
        }

        // 2. Permutation dans la liste des ingrédients de la recette
        var tempItem = Recipe.Inputs[indexA];
        Recipe.Inputs[indexA] = Recipe.Inputs[indexB];
        Recipe.Inputs[indexB] = tempItem;

        // 3. Récupération des ports d'entrée et de sortie
        var inputPorts = Ports.Where(p => p is ResourcePortModel rp && rp.Direction == PortDirection.Input).ToList();
        var outputPorts = Ports.Where(p => p is ResourcePortModel rp && rp.Direction == PortDirection.Output).ToList();

        if (indexA < inputPorts.Count && indexB < inputPorts.Count)
        {
            var portA = inputPorts[indexA];
            var portB = inputPorts[indexB];

            // Permutation dans la liste ordonnée
            inputPorts[indexA] = portB;
            inputPorts[indexB] = portA;

            // 4. Échange des positions géométriques des deux ports
            // Comme ils permutent de rangée, la position Y de A devient celle de B et inversement
            var tempPos = portA.Position;
            portA.Position = portB.Position;
            portB.Position = tempPos;

            // 5. Réorganisation de la liste interne _ports du NodeModel
            var allPortsField = typeof(NodeModel).GetField("_ports", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (allPortsField != null && allPortsField.GetValue(this) is List<PortModel> internalPortsList)
            {
                internalPortsList.Clear();
                internalPortsList.AddRange(inputPorts);
                internalPortsList.AddRange(outputPorts);
            }

            // 6. Notifier tous les liens connectés pour qu'ils se redessinent aux nouvelles coordonnées
            foreach (var link in portA.Links)
            {
                link.Refresh();
            }
            foreach (var link in portB.Links)
            {
                link.Refresh();
            }
        }

        // 7. Notifier le nœud pour mettre à jour son interface
        Refresh();
    }



    // =========================================================================
    // GESTION DU RÉORDONNANCEMENT DES SORTIES (AVEC SUIVI DES LIAISONS)
    // =========================================================================

    /// <summary>
    /// Vérifie si la sortie à l'index donné peut monter (index > 0).
    /// </summary>
    public bool CanMoveOutputUp(int index) => index > 0;

    /// <summary>
    /// Vérifie si la sortie à l'index donné peut descendre (index < Outputs.Count - 1).
    /// </summary>
    public bool CanMoveOutputDown(int index) => Recipe?.Outputs != null && index < Recipe.Outputs.Count - 1;

    /// <summary>
    /// Permute deux sorties consécutives dans la recette et répercute le changement sur les ports.
    /// Échange les coordonnées en mémoire pour que les courbes SVG suivent immédiatement.
    /// </summary>
    public void SwapOutputs(int indexA, int indexB)
    {
        // 1. Contrôle de validité des index
        if (Recipe?.Outputs == null ||
            indexA < 0 || indexA >= Recipe.Outputs.Count ||
            indexB < 0 || indexB >= Recipe.Outputs.Count ||
            indexA == indexB)
        {
            return;
        }

        // 2. Permutation dans la liste des sorties de la recette
        var tempItem = Recipe.Outputs[indexA];
        Recipe.Outputs[indexA] = Recipe.Outputs[indexB];
        Recipe.Outputs[indexB] = tempItem;

        // 3. Récupération des ports d'entrée et de sortie
        var inputPorts = Ports.Where(p => p is ResourcePortModel rp && rp.Direction == PortDirection.Input).ToList();
        var outputPorts = Ports.Where(p => p is ResourcePortModel rp && rp.Direction == PortDirection.Output).ToList();

        if (indexA < outputPorts.Count && indexB < outputPorts.Count)
        {
            var portA = outputPorts[indexA];
            var portB = outputPorts[indexB];

            // Permutation dans la liste ordonnée des sorties
            outputPorts[indexA] = portB;
            outputPorts[indexB] = portA;

            // 4. Échange des positions géométriques des deux ports
            // Les câbles Blazor.Diagrams lisent ces coordonnées pour tracer les courbes SVG
            var tempPos = portA.Position;
            portA.Position = portB.Position;
            portB.Position = tempPos;

            // 5. Réorganisation de la liste interne _ports du NodeModel (Entrées inchangées, Sorties réordonnées)
            var allPortsField = typeof(NodeModel).GetField("_ports", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (allPortsField != null && allPortsField.GetValue(this) is List<PortModel> internalPortsList)
            {
                internalPortsList.Clear();
                internalPortsList.AddRange(inputPorts);
                internalPortsList.AddRange(outputPorts);
            }

            // 6. Recalcul forcé de la trajectoire pour tous les liens connectés à ces deux ports
            foreach (var link in portA.Links)
            {
                link.Refresh();
            }
            foreach (var link in portB.Links)
            {
                link.Refresh();
            }
        }

        // 7. Notifier le nœud pour mettre à jour l'affichage Blazor
        Refresh();
    }



    // =========================================================================
    // RESTAURATION DE L'ORDRE PERSONNALISÉ DES PORTS (CHARGEMENT DE SAUVEGARDE)
    // =========================================================================

    /// <summary>
    /// Réordonne les listes d'entrées et de sorties ainsi que les ports internes
    /// pour correspondre exactement à l'ordre sauvegardé dans le fichier JSON.
    /// </summary>
    public void ApplyCustomPortOrder(List<string>? orderedInputs, List<string>? orderedOutputs)
    {
        bool changed = false;

        // 1. Réordonner les entrées si un ordre personnalisé a été sauvegardé
        if (orderedInputs != null && orderedInputs.Count == Recipe.Inputs.Count)
        {
            var newInputs = new List<RecipeItem>();
            foreach (var pid in orderedInputs)
            {
                var item = Recipe.Inputs.FirstOrDefault(i => i.ProductId == pid);
                if (item != null) newInputs.Add(item);
            }

            if (newInputs.Count == Recipe.Inputs.Count)
            {
                Recipe.Inputs.Clear();
                Recipe.Inputs.AddRange(newInputs);
                changed = true;
            }
        }

        // 2. Réordonner les sorties si un ordre personnalisé a été sauvegardé
        if (orderedOutputs != null && orderedOutputs.Count == Recipe.Outputs.Count)
        {
            var newOutputs = new List<RecipeItem>();
            foreach (var pid in orderedOutputs)
            {
                var item = Recipe.Outputs.FirstOrDefault(o => o.ProductId == pid);
                if (item != null) newOutputs.Add(item);
            }

            if (newOutputs.Count == Recipe.Outputs.Count)
            {
                Recipe.Outputs.Clear();
                Recipe.Outputs.AddRange(newOutputs);
                changed = true;
            }
        }

        // 3. Si l'ordre a changé, réaligner la liste interne _ports du NodeModel
        if (changed)
        {
            var currentInputPorts = Ports.Where(p => p is ResourcePortModel rp && rp.Direction == PortDirection.Input)
                                         .Cast<ResourcePortModel>()
                                         .ToList();

            var currentOutputPorts = Ports.Where(p => p is ResourcePortModel rp && rp.Direction == PortDirection.Output)
                                          .Cast<ResourcePortModel>()
                                          .ToList();

            // Réordonne les ports d'entrée selon Recipe.Inputs
            var sortedInputPorts = new List<PortModel>();
            foreach (var inp in Recipe.Inputs)
            {
                var port = currentInputPorts.FirstOrDefault(p => p.ProductId == inp.ProductId);
                if (port != null) sortedInputPorts.Add(port);
            }

            // Réordonne les ports de sortie selon Recipe.Outputs
            var sortedOutputPorts = new List<PortModel>();
            foreach (var outp in Recipe.Outputs)
            {
                var port = currentOutputPorts.FirstOrDefault(p => p.ProductId == outp.ProductId);
                if (port != null) sortedOutputPorts.Add(port);
            }

            // Met à jour la collection interne des ports de Blazor.Diagrams
            var allPortsField = typeof(NodeModel).GetField("_ports", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (allPortsField != null && allPortsField.GetValue(this) is List<PortModel> internalPortsList)
            {
                internalPortsList.Clear();
                internalPortsList.AddRange(sortedInputPorts);
                internalPortsList.AddRange(sortedOutputPorts);
            }

            Refresh();
        }
    }


    /// <summary>
    /// Calcule le nombre total d'ouvriers nécessaires pour cette machine
    /// en fonction de la machine sélectionnée et du multiplicateur.
    /// </summary>
    /// <param name="baseWorkers">Nombre d'ouvriers de base de la machine</param>
    public int GetTotalWorkers(int baseWorkers)
    {
        // On arrondit vers le haut si multiplicateur décimal ou produit direct
        return (int)Math.Ceiling(baseWorkers * Multiplier);
    }

    /// <summary>
    /// Calcule la consommation électrique totale en kW selon le multiplicateur.
    /// </summary>
    /// <param name="basePowerKw">Puissance unitaire de base en kW</param>
    public decimal GetTotalPowerKw(decimal basePowerKw)
    {
        return basePowerKw * Multiplier;
    }


}