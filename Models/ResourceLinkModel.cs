// Crée une classe qui hérite de LinkModel et transporte le code couleur hexadécimal

using Blazor.Diagrams.Core.Anchors;
using Blazor.Diagrams.Core.Models;

namespace CaptainArchitect.Models;

public class ResourceLinkModel : LinkModel
{
    // Couleur du câble selon la ressource transportée
    public string Color { get; set; } = "#38bdf8";

    // Débit qui transite réellement dans ce lien spécifique (ex: 24/m)
    public decimal FlowRate { get; set; } = 0m;

    // Débit maximum possible que cette liaison peut transporter (ex: 36)
    public decimal MaxFlowRate { get; set; } = 0m;

    public ResourceLinkModel(Anchor source, Anchor? target = null)
        : base(source, target)
    {
    }

}