// Parcourt les liaisons du diagramme et propage les flux :

using Blazor.Diagrams;
using Blazor.Diagrams.Core;
using Blazor.Diagrams.Core.Anchors;
using CaptainArchitect.Models;

namespace CaptainArchitect.Services;

public static class FlowEngine
{
    public static void RecalculateFlows(BlazorDiagram diagram)
    {
        var machineNodes = diagram.Nodes.OfType<MachineNodeModel>().ToList();

        // 1. Réinitialiser les débits entrants et sortants distribués
        foreach (var node in machineNodes)
        {
            node.ActualInputRates.Clear();
            node.DistributedOutputRates.Clear();
            node.DownstreamDemandRates.Clear();
            //node.OperationalEfficiency = 1.0m;

            foreach (var port in node.Ports.OfType<ResourcePortModel>())
            {
                if (port.Direction == PortDirection.Input)
                    node.ActualInputRates[port.Id] = 0m;
                else
                {
                    node.DistributedOutputRates[port.Id] = 0m;
                    node.DownstreamDemandRates[port.Id] = 0m;
                }
            }
        }


        // 2. Première passe : calcul des demandes et distribution de la production nominale
        DistributeFlows(diagram, machineNodes, useActualProduction: false);

        // 3. Calcul du taux d'activité (Efficiency) selon les entrées connectées
        foreach (var node in machineNodes)
        {
            var connectedInputPorts = node.Ports.OfType<ResourcePortModel>()
                .Where(p => p.Direction == PortDirection.Input && p.Links.Count > 0)
                .ToList();

            if (connectedInputPorts.Count == 0)
            {
                node.OperationalEfficiency = 1.0m;
                continue;
            }

            decimal minRatio = 1.0m;
            foreach (var port in connectedInputPorts)
            {
                var inputItem = node.Recipe.Inputs.FirstOrDefault(i => i.ProductId == port.ProductId);
                if (inputItem != null)
                {
                    decimal required = node.GetInputRatePerMin(inputItem);
                    decimal actual = node.GetActualInputRate(port.Id);

                    decimal ratio = required > 0 ? (actual / required) : 1.0m;
                    if (ratio < minRatio) minRatio = ratio;
                }
            }

            node.OperationalEfficiency = Math.Clamp(minRatio, 0m, 1m);
        }

        // 4. Deuxième passe : redistribution tenant compte de la production réelle
        foreach (var node in machineNodes)
        {
            foreach (var port in node.Ports.OfType<ResourcePortModel>().Where(p => p.Direction == PortDirection.Input))
            {
                node.ActualInputRates[port.Id] = 0m;
            }
        }

        DistributeFlows(diagram, machineNodes, useActualProduction: true);

        foreach (var node in machineNodes)
        {
            // OPTIMISATION : Ne rafraîchir que les nœuds qui possèdent au moins un lien,
            // ou dont les flux ont été calculés. Les machines isolées n'ont pas besoin d'être redessinées.
            bool hasLinks = node.Ports.Any(p => p.Links.Count > 0);

            if (hasLinks)
            {
                node.Refresh();
            }
        }

        diagram.Refresh();

    }



    private static void DistributeFlows(BlazorDiagram diagram, List<MachineNodeModel> machineNodes, bool useActualProduction)
    {
        foreach (var sourceNode in machineNodes)
        {
            foreach (var outputPort in sourceNode.Ports.OfType<ResourcePortModel>().Where(p => p.Direction == PortDirection.Output))
            {
                var outputItem = sourceNode.Recipe.Outputs.FirstOrDefault(o => o.ProductId == outputPort.ProductId);
                if (outputItem == null) continue;

                decimal availableRate = useActualProduction
                    ? sourceNode.GetActualProducedRatePerMin(outputItem)
                    : sourceNode.GetOutputRatePerMin(outputItem);

                // Recherche des ports cibles connectés via diagram.Links
                var connectedTargetPorts = diagram.Links
                    .Where(l => (l.Source as SinglePortAnchor)?.Port == outputPort && (l.Target as SinglePortAnchor)?.Port != null)
                    .Select(l => ((SinglePortAnchor)l.Target).Port as ResourcePortModel)
                    .Where(p => p != null && p.Direction == PortDirection.Input)
                    .Cast<ResourcePortModel>()
                    .ToList();

                if (connectedTargetPorts.Count == 0) continue;

                // Total de la demande en aval
                decimal totalDemand = 0m;
                var targetDemands = new Dictionary<ResourcePortModel, decimal>();

                foreach (var targetPort in connectedTargetPorts)
                {
                    if (targetPort.Parent is MachineNodeModel targetNode)
                    {
                        var reqItem = targetNode.Recipe.Inputs.FirstOrDefault(inp => inp.ProductId == targetPort.ProductId);
                        decimal demand = reqItem != null ? targetNode.GetInputRatePerMin(reqItem) : 0m;
                        targetDemands[targetPort] = demand;
                        totalDemand += demand;
                    }
                }

                sourceNode.DownstreamDemandRates[outputPort.Id] = totalDemand;

                // Répartition proportionnelle
                decimal deliveredTotal = 0m;
                foreach (var targetPort in connectedTargetPorts)
                {
                    decimal demand = targetDemands[targetPort];
                    decimal allocated = 0m;

                    if (totalDemand > 0)
                    {
                        allocated = Math.Min(demand, availableRate * (demand / totalDemand));
                    }

                    deliveredTotal += allocated;

                    if (targetPort.Parent is MachineNodeModel targetNode)
                    {
                        targetNode.ActualInputRates[targetPort.Id] = targetNode.GetActualInputRate(targetPort.Id) + allocated;
                    }
                }

                sourceNode.DistributedOutputRates[outputPort.Id] = deliveredTotal;
            }
        }
    }


}