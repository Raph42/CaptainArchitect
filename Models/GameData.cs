namespace CaptainArchitect.Models;

// Conteneur racine du fichier JSON
public record CoiData(
    string GameVersion,
    List<Product> Products,
    List<Machine> Machines,
    List<Recipe> Recipes
);

// Représente une ressource / produit
public record Product(
    string Id,
    string Name,
    string? TransportType,
    string? Color,
    string? IconPath,
    string? Image
);

// Représente une machine / bâtiment
public record Machine(
    string Id,
    string Name,
    string? Description,
    decimal ElectricityConsumptionKW,
    int Workers,
    List<MaintenanceCost>? Maintenance,
    List<ConstructionCost>? ConstructionCost,
    string? IconOrPrefab,
    string? Image,
    string? NextTierId
);

public record MaintenanceCost(
    decimal Quantity,
    string ProductId
);

public record ConstructionCost(
    decimal Quantity,
    string ProductId
);

// Liaison machine/recette dans "MachineBindings"
public record MachineBinding(
    string MachineId,
    string DurationSeconde,
    decimal OutputMultiplier
);

// Ingrédients / Produits de recettes dans "Inputs" et "Outputs"
public record RecipeItem(
    decimal Quantity,
    string ProductId
);

// Entrées dans la liste "Recipes"
public record Recipe(
    string RecipeId,
    List<RecipeItem> Inputs,
    List<RecipeItem> Outputs,
    List<MachineBinding> MachineBindings
)



{
    // Méthode d'aide pour extraire la durée en secondes
    //public decimal GetDurationSeconds(string? machineId = null)
    //{
    //    var binding = MachineBindings.FirstOrDefault(b => machineId == null || b.MachineId == machineId)
    //                  ?? MachineBindings.FirstOrDefault();

    //    if (binding == null) return 20m;

    //    // Nettoyage de chaînes comme "20s" -> 20
    //    var clean = binding.Duration.TrimEnd('s');
    //    return decimal.TryParse(clean, System.Globalization.CultureInfo.InvariantCulture, out var sec) ? sec : 20m;
    //}

}