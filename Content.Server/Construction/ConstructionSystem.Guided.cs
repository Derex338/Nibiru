using Content.Server.Construction.Components;
using Content.Shared.Construction;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Construction.Steps;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.Construction
{
    public sealed partial class ConstructionSystem
    {
        [Dependency] private readonly SharedPopupSystem _popup = default!;

        private readonly Dictionary<ConstructionPrototype, ConstructionGuide> _guideCache = new();

        private void InitializeGuided()
        {
            SubscribeNetworkEvent<RequestConstructionGuide>(OnGuideRequested);
            SubscribeLocalEvent<ConstructionComponent, GetVerbsEvent<Verb>>(AddDeconstructVerb);
            SubscribeLocalEvent<ConstructionComponent, ExaminedEvent>(HandleConstructionExamined);
        }

        private void OnGuideRequested(RequestConstructionGuide msg, EntitySessionEventArgs args)
        {
            if (!PrototypeManager.TryIndex(msg.ConstructionId, out ConstructionPrototype? prototype))
                return;

            if(GetGuide(prototype) is {} guide)
                RaiseNetworkEvent(new ResponseConstructionGuide(msg.ConstructionId, guide), args.SenderSession.Channel);
        }

        private void AddDeconstructVerb(EntityUid uid, ConstructionComponent component, GetVerbsEvent<Verb> args)
        {
            if (!args.CanAccess || !args.CanInteract || args.Hands == null)
                return;

            if (component.TargetNode == component.DeconstructionNode ||
                component.Node == component.DeconstructionNode)
                return;

            if (!PrototypeManager.TryIndex(component.Graph, out ConstructionGraphPrototype? graph))
                return;

            if (component.DeconstructionNode == null)
                return;

            if (GetCurrentNode(uid, component) is not {} currentNode)
                return;

            if (graph.Path(currentNode.Name, component.DeconstructionNode) is not {} path || path.Length == 0)
                return;

            Verb verb = new();
            //verb.Category = VerbCategories.Construction;
            //TODO VERBS add more construction verbs? Until then, removing construction category
            verb.Text = Loc.GetString("deconstructible-verb-begin-deconstruct");
            verb.Icon = new SpriteSpecifier.Texture(
                new ("/Textures/Interface/hammer_scaled.svg.192dpi.png"));

            verb.Act = () =>
            {
                SetPathfindingTarget(uid, component.DeconstructionNode, component);
                if (component.TargetNode == null)
                {
                    // Maybe check, but on the flip-side a better solution might be to not make it undeconstructible in the first place, no?
                    _popup.PopupEntity(Loc.GetString("deconstructible-verb-activate-no-target-text"), uid, uid);
                }
                else
                {
                    _popup.PopupEntity(Loc.GetString("deconstructible-verb-activate-text"), args.User, args.User);
                }
            };

            args.Verbs.Add(verb);
        }

        private void HandleConstructionExamined(EntityUid uid, ConstructionComponent component, ExaminedEvent args)
        {
            using (args.PushGroup(nameof(ConstructionComponent)))
            {
                if (GetTargetNode(uid, component) is {} target)
                {
                    if (target.Name == component.DeconstructionNode)
                    {
                        args.PushMarkup(Loc.GetString("deconstruction-header-text") + "\n");
                    }
                    else
                    {
                        args.PushMarkup(Loc.GetString(
                            "construction-component-to-create-header",
                            ("targetName", target.Name)) + "\n");
                    }
                }

                if (component.EdgeIndex == null && GetTargetEdge(uid, component) is {} targetEdge)
                {
                    if (GetCurrentNode(uid, component) is {} currentNode)
                    {
                        var targetNodeName = targetEdge.Target;
                        var alternativeEdges = new List<ConstructionGraphEdge>();
                        foreach (var e in currentNode.Edges)
                        {
                            if (e.Target == targetNodeName)
                                alternativeEdges.Add(e);
                        }

                        if (alternativeEdges.Count > 0)
                        {
                            for (var i = 0; i < alternativeEdges.Count; i++)
                            {
                                var altEdge = alternativeEdges[i];
                                if (i > 0)
                                    args.PushMarkup(Loc.GetString("construction-presenter-alternative") + "\n");

                                var preventStepExamine = false;
                                foreach (var condition in altEdge.Conditions)
                                {
                                    preventStepExamine |= condition.DoExamine(args);
                                }

                                if (!preventStepExamine && altEdge.Steps.Count > 0)
                                    altEdge.Steps[0].DoExamine(args);
                            }
                            return;
                        }
                    }

                    var preventStepExamineFallback = false;

                    foreach (var condition in targetEdge.Conditions)
                    {
                        preventStepExamineFallback |= condition.DoExamine(args);
                    }

                    if (!preventStepExamineFallback && targetEdge.Steps.Count > 0)
                        targetEdge.Steps[0].DoExamine(args);
                    return;
                }

                if (GetCurrentEdge(uid, component) is {} edge)
                {
                    var preventStepExamine = false;

                    foreach (var condition in edge.Conditions)
                    {
                        preventStepExamine |= condition.DoExamine(args);
                    }

                    if (!preventStepExamine && component.StepIndex < edge.Steps.Count)
                        edge.Steps[component.StepIndex].DoExamine(args);
                }
            }

        }


        /// <summary>
        ///     Returns a <see cref="ConstructionGuide"/> for a given <see cref="ConstructionPrototype"/>,
        ///     generating and caching it as needed.
        /// </summary>
        /// <param name="construction">The construction prototype to generate the guide for. We must be able to pathfind
        ///                            from its starting node to its ending node to be able to generate a guide for it.</param>
        /// <returns>The guide for the given construction, or null if we can't pathfind from the start node to the
        ///          end node on that construction.</returns>
        private ConstructionGuide? GetGuide(ConstructionPrototype construction)
        {
            // NOTE: This method might be allocate a fair bit, but do not worry!
            // This method is specifically designed to generate guides once and cache the results,
            // therefore we don't need to worry *too much* about the performance of this.

            // If we've generated and cached this guide before, return it.
            if (_guideCache.TryGetValue(construction, out var guide))
                return guide;

            // If the graph doesn't actually exist, do nothing.
            if (!PrototypeManager.Resolve(construction.Graph, out ConstructionGraphPrototype? graph))
                return null;

            // If either the start node or the target node are missing, do nothing.
            if (GetNodeFromGraph(graph, construction.StartNode) is not {} startNode
                || GetNodeFromGraph(graph, construction.TargetNode) is not {} targetNode)
                return null;

            // If there's no path from start to target, do nothing.
            if (graph.Path(construction.StartNode, construction.TargetNode) is not {} path
                || path.Length == 0)
                return null;

            var step = 1;

            var entries = new List<ConstructionGuideEntry>()
            {
                // Initial construction header.
                new()
                {
                    Localization = construction.Type == ConstructionType.Structure
                        ? "construction-presenter-to-build" : "construction-presenter-to-craft",
                    EntryNumber = step,
                }
            };

            var conditions = new HashSet<string>();

            // Iterate until the penultimate node.
            var node = startNode;
            var index = 0;
            while(node != targetNode)
            {
                var targetEdgeName = path[index].Name;
                var edges = new List<ConstructionGraphEdge>();
                foreach (var e in node.Edges)
                {
                    if (e.Target == targetEdgeName)
                        edges.Add(e);
                }

                if (edges.Count == 0)
                    return null;

                var initialStep = step;
                var maxStep = step;
                var old = conditions;
                var newConditionsAccumulator = new HashSet<string>();

                for (var edgeIdx = 0; edgeIdx < edges.Count; edgeIdx++)
                {
                    var edge = edges[edgeIdx];
                    var currentStep = initialStep;

                    if (edgeIdx > 0)
                    {
                        entries.Add(new ConstructionGuideEntry()
                        {
                            Localization = "construction-presenter-alternative"
                        });
                    }

                    // First steps are handled specially.
                    if (initialStep == 1)
                    {
                        foreach (var graphStep in edge.Steps)
                        {
                            entries.Add(graphStep.GenerateGuideEntry());
                        }

                        // Now actually list the construction conditions.
                        if (edgeIdx == 0)
                        {
                            foreach (var condition in construction.Conditions)
                            {
                                if (condition.GenerateGuideEntry() is not {} conditionEntry)
                                    continue;

                                conditionEntry.Padding += 4;
                                entries.Add(conditionEntry);
                            }
                        }

                        currentStep++;
                        if (currentStep > maxStep) maxStep = currentStep;
                        continue;
                    }

                    var edgeConditions = new HashSet<string>();

                    foreach (var condition in edge.Conditions)
                    {
                        foreach (var conditionEntry in condition.GenerateGuideEntry())
                        {
                            edgeConditions.Add(conditionEntry.Localization);

                            // Okay so if the condition entry had a non-null value here, we take it as a numbered step.
                            // This is for cases where there is a lot of snowflake behavior, such as machine frames...
                            // So that the step of inserting a machine board and inserting all of its parts is numbered.
                            if (conditionEntry.EntryNumber != null)
                                conditionEntry.EntryNumber = currentStep++;

                            // To prevent spamming the same stuff over and over again. This is a bit naive, but..ye.
                            // Also we will only hide this condition *if* it isn't numbered.
                            else
                            {
                                if (old.Contains(conditionEntry.Localization))
                                    continue;

                                // We only add padding for non-numbered entries.
                                conditionEntry.Padding += 4;
                            }

                            entries.Add(conditionEntry);
                        }
                    }

                    foreach (var graphStep in edge.Steps)
                    {
                        var entry = graphStep.GenerateGuideEntry();
                        entry.EntryNumber = currentStep++;
                        entries.Add(entry);
                    }

                    foreach (var c in edgeConditions)
                        newConditionsAccumulator.Add(c);

                    if (currentStep > maxStep) maxStep = currentStep;
                }

                step = maxStep;
                conditions = newConditionsAccumulator;
                node = path[index++];

                if (initialStep == 1 && node != targetNode)
                {
                    // Add a bit of padding if there will be more steps after this.
                    entries.Add(new ConstructionGuideEntry());
                }
            }

            var researchPoints = 0;
            var currentNode2 = startNode;
            foreach (var nextNode2 in path)
            {
                if (currentNode2.TryGetEdge(nextNode2.Name, out var edge))
                {
                    foreach (var action in edge.Completed)
                    {
                        if (action is Content.Shared._Nibiru.Construction.Completions.PointsFromCraft pointsAction)
                        {
                            var actionPoints = pointsAction.Points;
                            if (pointsAction.Decreasing && targetNode.Entity.GetId(null, null, new(EntityManager)) is { } entityId)
                            {
                                var count = 0;
                                var query = EntityQueryEnumerator<MetaDataComponent>();
                                while (query.MoveNext(out _, out var meta))
                                {
                                    if (meta.EntityPrototype?.ID == entityId)
                                        count++;
                                }
                                actionPoints /= (count + 1);
                            }
                            researchPoints += actionPoints;
                        }
                    }
                }
                currentNode2 = nextNode2;
            }

            guide = new ConstructionGuide(entries.ToArray(), researchPoints);
            _guideCache[construction] = guide;
            return guide;
        }
    }
}
