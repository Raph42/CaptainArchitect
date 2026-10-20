using Blazor.Diagrams.Core.Anchors;
using Blazor.Diagrams.Core.Models;
using Blazor.Diagrams.Core.Models.Base;

namespace CaptainArchitect.Models;

public enum PortDirection
{
    Input,
    Output
}

public class ResourcePortModel : PortModel
{
    public string ProductId { get; }
    public PortDirection Direction { get; }

    public ResourcePortModel(string productId, NodeModel parent, PortDirection direction, PortAlignment alignment)
        : base(parent, alignment)
    {
        ProductId = productId;
        Direction = direction;
    }

    // Règle de validation native appelée par Blazor.Diagrams au lâcher de la souris
    public override bool CanAttachTo(ILinkable other)
    {
        // 1. On ne peut se lier qu'à un autre port
        if (other is not ResourcePortModel targetPort)
            return false;

        // 2. Pas de lien sur la même machine
        if (targetPort.Parent == this.Parent)
            return false;

        // 3. Une sortie doit aller vers une entrée (ou inversement)
        if (this.Direction == targetPort.Direction)
            return false;

        // 4.Les deux ports doivent partager exactement la même ressource
        if (!string.Equals(this.ProductId, targetPort.ProductId, StringComparison.OrdinalIgnoreCase))
            return false;

        // Lorsqu'un utilisateur étire un câble vers un port déjà branché à cette même sortie,
        // `Blazor.Diagrams` interroge `CanAttachTo`[cite: 5].
        // La condition parcourt la collection `this.Links` :
        // si un lien relie déjà ce port au port cible, `alreadyConnected` vaut `true`
        // et la méthode renvoie `false`[cite: 5].
        // Le câble refusera alors de s'ancrer et disparaîtra au relâchement du clic.

        // 5. Interdire le doublon : vérifier si un lien existe déjà entre ces deux ports
        bool alreadyConnected = this.Links.Any(link =>
        {
            var srcPort = (link.Source as SinglePortAnchor)?.Port;
            var tgtPort = (link.Target as SinglePortAnchor)?.Port;

            return (srcPort == targetPort || tgtPort == targetPort);
        });

        if (alreadyConnected)
            return false;


        return true;
    }
}