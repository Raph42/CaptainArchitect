// Parcourt les liaisons du diagramme et propage les flux :

using Blazor.Diagrams;
using Blazor.Diagrams.Core;
using Blazor.Diagrams.Core.Anchors;
using CaptainArchitect.Models;

namespace CaptainArchitect.Services;

public static class FlowEngine
{
    public static void RecalculateFlows(BlazorDiagram diagram, ResourceService resourceService)
    {
        var machineNodes = diagram.Nodes.OfType<MachineNodeModel>().ToList();
        var storageNodes = diagram.Nodes.OfType<StorageNodeModel>().ToList();

        // 0. Réinitialiser le débit de tous les câbles
        foreach (var link in diagram.Links.OfType<ResourceLinkModel>())
        {
            link.FlowRate = 0m;
        }

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


        // Réinitialiser les débits calculés pour les stockages
        foreach (var storage in storageNodes)
        {
            storage.TotalInputRate = 0m;
            storage.DistributedOutputRate = 0m;
            storage.DownstreamDemandRate = 0m;
        }


        // 2. Première passe : calcul des demandes et distribution de la production nominale
        DistributeAllFlows(diagram, machineNodes, storageNodes, useActualProduction: false);

        // 3. Calcul du taux d'activité (Efficiency) selon les entrées connectées
        foreach (var node in machineNodes)
        {
            // Si en pause, l'efficacité opérationnelle passe à 0
            if (node.IsPaused)
            {
                node.OperationalEfficiency = 0m;
                continue;
            }

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

        foreach (var storage in storageNodes)
        {
            storage.TotalInputRate = 0m;
            storage.DistributedOutputRate = 0m;
            storage.DownstreamDemandRate = 0m;
        }

        DistributeAllFlows(diagram, machineNodes, storageNodes, useActualProduction: true);


        // 5. Rafraîchissement des nœuds graphiques
        foreach (var node in machineNodes)
        {
            // OPTIMISATION : Ne rafraîchir que les nœuds qui possèdent au moins un lien,
            // ou dont les flux ont été calculés. Les machines isolées n'ont pas besoin d'être redessinées.
            //bool hasLinks = node.Ports.Any(p => p.Links.Count > 0);

            //if (hasLinks)
            //{
                node.Refresh();
            //}
        }

        foreach (var storage in storageNodes)
        {
            storage.Refresh();
        }


        // =========================================================================
        // Calcul automatique des totaux ouvriers et électricité
        // =========================================================================
        int totalWorkers = 0;
        decimal totalPowerKw = 0m;

        foreach (var node in machineNodes)
        {
            // Récupération des données réelles de la machine (Workers, Power)
            var machine = resourceService.GetMachine(node.CurrentMachineId);
            if (machine != null)
            {
                // Multiplié par le nombre de machines (Multiplier)
                totalWorkers += (int)Math.Ceiling(machine.Workers * node.Multiplier);
                totalPowerKw += machine.ElectricityConsumptionKW * node.Multiplier;
            }
        }

        // Notifie l'AppBar (MainLayout) via l'événement dans ResourceService
        resourceService.UpdateGlobalStats(totalWorkers, totalPowerKw);

        // Rafraîchit le canevas SVG
        diagram.Refresh();

    }



    /// <summary>
    /// Distribue les flux depuis les machines productrices ET depuis les stockages.
    /// Exécute 2 passes pour assurer la transmission à travers les stockages intermédiaires.
    /// </summary>
    private static void DistributeAllFlows(BlazorDiagram diagram, List<MachineNodeModel> machineNodes, List<StorageNodeModel> storageNodes, bool useActualProduction)
    {
        // 2 itérations suffisent pour résoudre la chaîne Machine -> Stockage -> Machine
        for (int iter = 0; iter < 2; iter++)
        {
            // 1. Réinitialiser les entrées accumulées sur les stockages avant chaque itération
            // pour éviter d'additionner deux fois le flux
            foreach (var storage in storageNodes)
            {
                storage.TotalInputRate = 0m;
            }


            // 2. Réinitialiser également les entrées des machines avant chaque passage
            // pour éviter d'additionner 2 fois le flux distribué (évite le 7.5 + 7.5 = 15)
            foreach (var node in machineNodes)
            {
                foreach (var port in node.Ports.OfType<ResourcePortModel>().Where(p => p.Direction == PortDirection.Input))
                {
                    node.ActualInputRates[port.Id] = 0m;
                }
            }


            // Distribution depuis les machines
            foreach (var sourceNode in machineNodes)
            {
                foreach (var outputPort in sourceNode.Ports.OfType<ResourcePortModel>().Where(p => p.Direction == PortDirection.Output))
                {
                    var outputItem = sourceNode.Recipe.Outputs.FirstOrDefault(o => o.ProductId == outputPort.ProductId);
                    if (outputItem == null) continue;

                    decimal availableRate = 0m;
                    if (!sourceNode.IsPaused)
                    {
                        availableRate = useActualProduction
                            ? sourceNode.GetActualProducedRatePerMin(outputItem)
                            : sourceNode.GetOutputRatePerMin(outputItem);
                    }

                    DistributeFromPort(diagram, outputPort, availableRate, out decimal totalDemand, out decimal deliveredTotal);
                    sourceNode.DownstreamDemandRates[outputPort.Id] = totalDemand;
                    sourceNode.DistributedOutputRates[outputPort.Id] = deliveredTotal;
                }
            }

            // Distribution depuis les stockages
            foreach (var storage in storageNodes)
            {
                // Un stockage distribue ce qu'il a reçu en entrée
                decimal availableInStorage = storage.TotalInputRate;

                DistributeFromPort(diagram, storage.OutputPort, availableInStorage, out decimal downstreamDemand, out decimal deliveredTotal);
                storage.DownstreamDemandRate = downstreamDemand;
                storage.DistributedOutputRate = deliveredTotal;
            }
        }
    }



    /// <summary>
    /// Trouve toutes les cibles connectées à un port de sortie et leur distribue le débit au prorata.
    /// </summary>
    private static void DistributeFromPort(BlazorDiagram diagram, ResourcePortModel outputPort, decimal availableRate, out decimal totalDemand, out decimal deliveredTotal)
    {
        totalDemand = 0m;
        deliveredTotal = 0m;

        // Trouver les ports d'entrée cibles via les liens du diagramme
        var connectedTargetPorts = new List<ResourcePortModel>();
        foreach (var link in diagram.Links)
        {
            ResourcePortModel? src = (link.Source as SinglePortAnchor)?.Port as ResourcePortModel;
            ResourcePortModel? tgt = (link.Target as SinglePortAnchor)?.Port as ResourcePortModel;

            if (src == outputPort && tgt is { Direction: PortDirection.Input })
                connectedTargetPorts.Add(tgt);
            else if (tgt == outputPort && src is { Direction: PortDirection.Input })
                connectedTargetPorts.Add(src);
        }

        if (connectedTargetPorts.Count == 0) return;

        var targetDemands = new Dictionary<ResourcePortModel, decimal>();

        // Calcul de la demande de chaque port cible
        foreach (var targetPort in connectedTargetPorts)
        {
            decimal demand = 0m;

            if (targetPort.Parent is MachineNodeModel targetMachine)
            {
                var reqItem = targetMachine.Recipe.Inputs.FirstOrDefault(i => i.ProductId == targetPort.ProductId);
                demand = (reqItem != null && !targetMachine.IsPaused) ? targetMachine.GetInputRatePerMin(reqItem) : 0m;
            }
            else if (targetPort.Parent is StorageNodeModel targetStorage)
            {
                // Si la cible est un stockage, sa demande correspond à ce que ses consommateurs aval lui réclament
                demand = targetStorage.DownstreamDemandRate > 0 ? targetStorage.DownstreamDemandRate : 9999m;
            }

            targetDemands[targetPort] = demand;
            totalDemand += demand;
        }

        // Répartition de la production disponible
        foreach (var targetPort in connectedTargetPorts)
        {
            decimal demand = targetDemands[targetPort];
            decimal allocated = 0m;

            if (totalDemand > 0)
            {
                allocated = availableRate >= totalDemand
                    ? demand
                    : availableRate * (demand / totalDemand);
            }

            deliveredTotal += allocated;

            if (targetPort.Parent is MachineNodeModel targetMachine)
            {
                targetMachine.ActualInputRates[targetPort.Id] = targetMachine.GetActualInputRate(targetPort.Id) + allocated;
            }
            else if (targetPort.Parent is StorageNodeModel targetStorage)
            {
                targetStorage.TotalInputRate += allocated;
            }


            // =========================================================================
            // AJOUT POUR LE LINK LABEL : Assigner le débit réel circulant dans ce câble
            // =========================================================================
            var targetLink = diagram.Links
                .OfType<ResourceLinkModel>()
                .FirstOrDefault(l =>
                    ((l.Source as SinglePortAnchor)?.Port == outputPort && (l.Target as SinglePortAnchor)?.Port == targetPort) ||
                    ((l.Target as SinglePortAnchor)?.Port == outputPort && (l.Source as SinglePortAnchor)?.Port == targetPort));

            if (targetLink != null)
            {
                // Stocke le débit effectif calculé (ex: 24/m) sur le câble
                targetLink.FlowRate = allocated;

                // Demande au câble de redessiner sa pastille flottante
                targetLink.Refresh();
            }

        }
    }


}