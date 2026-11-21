// Parcourt les liaisons du diagramme et propage les flux :

using Blazor.Diagrams;
using Blazor.Diagrams.Core;
using Blazor.Diagrams.Core.Anchors;
using CaptainArchitect.Models;

namespace CaptainArchitect.Services;

public static class FlowEngine
{
    // Référence statique vers la liste globale des onglets actifs
    // Permet aux widgets enfants (MachineNodeWidget, StorageNodeWidget) qui n'ont que "Diagram"
    // de déclencher automatiquement un recalcul global de tous les onglets.
    private static List<FactoryTab>? _registeredTabs;


    /// <summary>
    /// Enregistre la liste globale des onglets auprès du moteur de flux.
    /// </summary>
    public static void RegisterTabs(List<FactoryTab> tabs)
    {
        _registeredTabs = tabs;
    }


    /// <summary>
    /// Surcharge rétrocompatible appelée par les widgets individuels.
    /// Si des onglets sont enregistrés, recalcule toute l'usine multi-onglets.
    /// </summary>
    public static void RecalculateFlows(BlazorDiagram diagram, ResourceService resourceService)
    {
        if (_registeredTabs != null && _registeredTabs.Count > 0)
        {
            RecalculateFlows(_registeredTabs, resourceService);
            return;
        }

        // Mode repli sur un seul diagramme
        RecalculateFlowsOnDiagrams(new List<BlazorDiagram> { diagram }, resourceService);
    }


    /// <summary>
    /// Point d'entrée principal multi-onglets : calcule les flux de tous les onglets simultanément
    /// et propage les débits à travers les portails Export -> Import.
    /// </summary>
    public static void RecalculateFlows(List<FactoryTab> tabs, ResourceService resourceService)
    {
        if (tabs == null || tabs.Count == 0) return;
        _registeredTabs = tabs;

        var diagrams = tabs.Select(t => t.Diagram).Where(d => d != null).ToList();
        RecalculateFlowsOnDiagrams(diagrams, resourceService);
    }


    private static void RecalculateFlowsOnDiagrams(List<BlazorDiagram> diagrams, ResourceService resourceService)
    {
        var allMachines = diagrams.SelectMany(d => d.Nodes.OfType<MachineNodeModel>()).ToList();
        var allStorages = diagrams.SelectMany(d => d.Nodes.OfType<StorageNodeModel>()).ToList();
        var allPortals = diagrams.SelectMany(d => d.Nodes.OfType<PortalNodeModel>()).ToList();

        // 0. Réinitialiser le débit de tous les câbles sur tous les onglets
        foreach (var diagram in diagrams)
        {
            foreach (var link in diagram.Links.OfType<ResourceLinkModel>())
            {
                link.FlowRate = 0m;
                link.MaxFlowRate = 0m;
            }
        }

        // 1. Réinitialiser les débits des machines, stockages et portails

        // 1.1 Réinitialiser les débits des machines
        foreach (var node in allMachines)
        {
            node.ActualInputRates.Clear();
            node.DistributedOutputRates.Clear();
            node.DownstreamDemandRates.Clear();

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

        // 1.2 Réinitialiser les débits calculés pour les stockages
        foreach (var storage in allStorages)
        {
            storage.TotalInputRate = 0m;
            storage.DistributedOutputRate = 0m;
            storage.DownstreamDemandRate = 0m;
        }

        // 1.3 Réinitialiser les débits calculés pour les portails
        foreach (var portal in allPortals)
        {
            portal.TotalInputRate = 0m;
            portal.DistributedOutputRate = 0m;
            portal.DownstreamDemandRate = 0m;
        }

        // 2. Première passe : calcul des demandes et distribution de la production nominale
        DistributeAllFlows(diagrams, allMachines, allStorages, allPortals, useActualProduction: false);

        // 3. Calcul du taux d'activité (OperationalEfficiency) de chaque machine
        foreach (var node in allMachines)
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

        // 4. Deuxième passe : redistribution tenant compte des efficacités réelles

        // 4.1
        foreach (var node in allMachines)
        {
            foreach (var port in node.Ports.OfType<ResourcePortModel>().Where(p => p.Direction == PortDirection.Input))
            {
                node.ActualInputRates[port.Id] = 0m;
            }
        }

        // 4.2
        foreach (var storage in allStorages)
        {
            storage.TotalInputRate = 0m;
            storage.DistributedOutputRate = 0m;
            storage.DownstreamDemandRate = 0m;
        }

        // 4.3
        foreach (var portal in allPortals)
        {
            portal.TotalInputRate = 0m;
            portal.DistributedOutputRate = 0m;
            portal.DownstreamDemandRate = 0m;
        }


        DistributeAllFlows(diagrams, allMachines, allStorages, allPortals, useActualProduction: true);


        // 5. Rafraîchissement visuel de tous les nœuds sur tous les onglets
        foreach (var node in allMachines) node.Refresh();
        foreach (var storage in allStorages) storage.Refresh();
        foreach (var portal in allPortals) portal.Refresh();


        // 6. Calcul global des ouvriers et de l'électricité sur TOUTE l'usine (tous onglets cumulés)
        int totalWorkers = 0;
        decimal totalPowerKw = 0m;

        foreach (var node in allMachines)
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
        // diagram.Refresh(); // <-- Inutile lors d'un calcul de débit et très coûteux sur une grande usine

    }



    /// <summary>
    /// Propage les flux sur chaque diagramme ET transfère les ressources entre portails Export -> Import.
    /// Exécute 4 passes pour résoudre les chaînes complètes (Machine -> Stockage -> Export -> Import -> Stockage -> Machine).
    /// </summary>
    private static void DistributeAllFlows(
        List<BlazorDiagram> diagrams,
        List<MachineNodeModel> allMachines,
        List<StorageNodeModel> allStorages,
        List<PortalNodeModel> allPortals,
        bool useActualProduction)
    {
        var exportPortals = allPortals.Where(p => p.Direction == PortalDirection.Export).ToList();
        var importPortals = allPortals.Where(p => p.Direction == PortalDirection.Import).ToList();

        // 4 itérations permettent de traverser sans perte : Machine -> Stockage -> Export -> Import -> Stockage -> Machine
        for (int iter = 0; iter < 4; iter++)
        {
            // 1. Réinitialiser les entrées accumulées sur les stockages avant chaque itération
            //    pour éviter d'additionner deux fois le flux
            foreach (var storage in allStorages)
            {
                storage.TotalInputRate = 0m;
            }

            // 2. Réinitialiser UNIQUEMENT les portails Export (qui reçoivent des flux locaux à l'étape A).
            //    Les portails Import doivent conserver le TotalInputRate reçu du bus à l'étape B de l'itération précédente !
            foreach (var exportPortal in exportPortals)
            {
                exportPortal.TotalInputRate = 0m;
            }

            // 3. Réinitialiser également les entrées des machines avant chaque passage
            //    pour éviter d'additionner 2 fois le flux distribué (évite le 7.5 + 7.5 = 15)
            foreach (var node in allMachines)
            {
                foreach (var port in node.Ports.OfType<ResourcePortModel>().Where(p => p.Direction == PortDirection.Input))
                {
                    node.ActualInputRates[port.Id] = 0m;
                }
            }

            // A. Distribution locale sur chaque onglet
            foreach (var diagram in diagrams)
            {
                var tabMachines = diagram.Nodes.OfType<MachineNodeModel>().ToList();
                var tabStorages = diagram.Nodes.OfType<StorageNodeModel>().ToList();
                var tabImports = diagram.Nodes.OfType<PortalNodeModel>().Where(p => p.Direction == PortalDirection.Import).ToList();

                // 1. Distribution depuis les machines de cet onglet
                foreach (var sourceNode in tabMachines)
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


                // 2. Distribution depuis les stockages de cet onglet
                foreach (var storage in tabStorages)
                {
                    // Un stockage distribue ce qu'il a reçu en entrée
                    decimal availableInStorage = storage.TotalInputRate;

                    DistributeFromPort(diagram, storage.OutputPort, availableInStorage, out decimal downstreamDemand, out decimal deliveredTotal);
                    storage.DownstreamDemandRate = downstreamDemand;
                    storage.DistributedOutputRate = deliveredTotal;
                }


                // 3. Distribution depuis les portails Import de cet onglet (injectent ce qu'ils ont reçu du bus)
                foreach (var importPortal in tabImports)
                {
                    DistributeFromPort(diagram, importPortal.Port, importPortal.TotalInputRate, out decimal downstreamDemand, out decimal deliveredTotal);
                    importPortal.DownstreamDemandRate = downstreamDemand;
                    importPortal.DistributedOutputRate = deliveredTotal;
                }

            } // Boucle A.


            // B. Transfert inter-onglets sur le Bus : chaque portail Export répartit son débit vers ses portails Import abonnés
            foreach (var exportPortal in exportPortals)
            {
                var subscribers = importPortals
                    .Where(imp => imp.SourceExportPortalId == exportPortal.Id &&
                                  string.Equals(imp.ProductId, exportPortal.ProductId, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (subscribers.Count == 0)
                {
                    exportPortal.DownstreamDemandRate = 0m;
                    exportPortal.DistributedOutputRate = 0m;
                    continue;
                }

                // Somme des demandes de tous les onglets qui importent ce canal
                decimal totalBusDemand = 0m;
                var subscriberDemands = new Dictionary<PortalNodeModel, decimal>();

                foreach (var sub in subscribers)
                {
                    decimal demand = sub.DownstreamDemandRate > 0 ? sub.DownstreamDemandRate : 9999m;
                    subscriberDemands[sub] = demand;
                    totalBusDemand += demand;
                }

                exportPortal.DownstreamDemandRate = totalBusDemand;

                // Répartition au prorata entre les onglets consommateurs
                decimal deliveredOnBus = 0m;
                decimal availableFromExport = exportPortal.TotalInputRate;

                foreach (var sub in subscribers)
                {
                    decimal demand = subscriberDemands[sub];
                    decimal allocated = 0m;

                    if (totalBusDemand > 0)
                    {
                        allocated = availableFromExport >= totalBusDemand
                            ? demand
                            : availableFromExport * (demand / totalBusDemand);
                    }

                    sub.TotalInputRate = allocated;
                    deliveredOnBus += allocated;
                }

                exportPortal.DistributedOutputRate = deliveredOnBus;

            } // Boucle B.

        } // Boucle 4 passes
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
            else if (targetPort.Parent is PortalNodeModel { Direction: PortalDirection.Export } targetExport)
            {
                // Un portail Export réclame la somme des demandes de tous les onglets qui l'importent
                demand = targetExport.DownstreamDemandRate > 0 ? targetExport.DownstreamDemandRate : 9999m;
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
            else if (targetPort.Parent is PortalNodeModel { Direction: PortalDirection.Export } targetExport)
            {
                targetExport.TotalInputRate += allocated;
            }


            // =========================================================================
            // Affectation du débit au câble (ResourceLinkModel)
            // =========================================================================
            var targetLink = diagram.Links
                .OfType<ResourceLinkModel>()
                .FirstOrDefault(l =>
                    ((l.Source as SinglePortAnchor)?.Port == outputPort && (l.Target as SinglePortAnchor)?.Port == targetPort) ||
                    ((l.Target as SinglePortAnchor)?.Port == outputPort && (l.Source as SinglePortAnchor)?.Port == targetPort));

            if (targetLink != null)
            {
                // 1. Débit réel circulant (Ligne 2)
                targetLink.FlowRate = allocated;

                // 2. Débit max théorique : ON PREND DIRECTEMENT LA VALEUR DE LA 1ÈRE LIGNE DE LA MACHINE SOURCE !
                // (Si le port source est une machine, on prend sa 1ère ligne GetOutputRatePerMin)
                if (outputPort.Parent is MachineNodeModel sourceMachine)
                {
                    var sourceItem = sourceMachine.Recipe.Outputs.FirstOrDefault(o => o.ProductId == outputPort.ProductId);
                    targetLink.MaxFlowRate = sourceItem != null
                        ? sourceMachine.GetOutputRatePerMin(sourceItem)
                        : availableRate;
                }
                else
                {
                    // Si la source est un stockage, on prend ce qui entre dans le stockage
                    targetLink.MaxFlowRate = availableRate;
                }

                // Force le câble à redessiner sa bulle
                targetLink.Refresh();
            }

        }
    }


}