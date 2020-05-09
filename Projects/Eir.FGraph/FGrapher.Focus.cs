﻿using Eir.DevTools;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using System.Drawing;
using Hl7.Fhir.Model;
using System.Diagnostics;

namespace FGraph
{
    public partial class FGrapher
    {
        public void RenderFocusGraphs(String cssFile)
        {
            foreach (GraphNode node in this.graphNodesByAnchor.Values)
            {
                // Only render top level (profile) nodes.
                if (
                    (node.Anchor != null) &&
                    (String.IsNullOrEmpty(node.Anchor.Item))
                    )
                    this.RenderFocusGraph(cssFile,
                        node,
                        $"focus/{node.Anchor.Url.LastUriPart()}");
            }
        }

        void RenderFocusGraph(String cssFile,
            GraphNode focusGraphNode,
            String traversalName)
        {
            SvgEditor e = new SvgEditor($"FocusGraph-{focusGraphNode.Anchor.Url.LastUriPart()}");
            e.AddCssFile(cssFile);

            this.svgEditors.Add(e);
            SENodeGroup seGroupParents = new SENodeGroup("parents", true);
            SENodeGroup seGroupFocus = new SENodeGroup("focus", true);
            SENodeGroup seGroupChildren = new SENodeGroup("children", true);
            seGroupParents.AppendChild(seGroupFocus);
            seGroupFocus.AppendChild(seGroupChildren);

            SENode focusSENode = this.CreateNode(focusGraphNode);
            focusSENode.Class = "focus";
            seGroupFocus.AppendNode(focusSENode);

            seGroupParents.AppendNodes(TraverseParents(focusGraphNode, focusSENode, "focus/*", 1));
            seGroupFocus.AppendChildren(TraverseChildren(focusGraphNode, focusSENode, "focus/*", 1));

            e.Render(seGroupParents, true);
        }

        protected SENode CreateNodeBinding(ElementDefinition.ElementDefinitionBindingComponent binding)
        {
            String hRef = null;
            //$if (linkFlag)
            //$    hRef = this.HRef(mapNode);
            SENode node = new SENode
            {
                HRef = hRef
            };
            node.Class = "valueSet";

            String displayName = binding.ValueSet.LastPathPart();
            if (this.TryGetValueSet(binding.ValueSet, out ValueSet vs) == false)
            {
                displayName = vs.Name;
            }
            node.AddTextLine(displayName, hRef);
            node.AddTextLine("ValueSet", hRef);
            node.LhsAnnotation = "bind";
            return node;
        }

        protected SENode CreateNode(GraphNode graphNode)
        {
            String hRef = graphNode.HRef;
            SENode node = new SENode
            {
                HRef = hRef
            };
            node.Class = graphNode.CssClass;

            String displayName = graphNode.DisplayName;
            //Debug.Assert(displayName != "Breast/Radiology/Composition");

            foreach (String titlePart in displayName.Split('/'))
            {
                String s = titlePart.Trim();
                node.AddTextLine(s, hRef);
            }

            node.LhsAnnotation = ResolveAnnotation(graphNode, graphNode.LhsAnnotationText);
            node.RhsAnnotation = ResolveAnnotation(graphNode, graphNode.RhsAnnotationText);

            return node;
        }

        String ResolveCardinality(GraphNode node,
            String elementId)
        {
            const String fcn = "ResolveCardinality";

            GraphAnchor anchor = node.Anchor;
            if (anchor == null)
            {
                this.ConversionError("FGrapher", fcn, $"Anchor is null");
                return null;
            }

            if (this.TryGetProfile(anchor.Url, out StructureDefinition sDef) == false)
            {
                this.ConversionError("FGrapher", fcn, $"StructureDefinition {anchor.Url} not found");
                return null;
            }

            ElementDefinition e = sDef.FindSnapElement(elementId);
            if (e == null)
            {
                this.ConversionError("FGrapher", fcn, $"Element {elementId} not found");
                return null;
            }

            if (e.Min.HasValue == false)
            {
                this.ConversionError("FGrapher",
                    fcn,
                    $"element {elementId}' min cardinality is empty");
                return null;
            }

            if (String.IsNullOrWhiteSpace(e.Max) == true)
            {
                this.ConversionError("FGrapher",
                    fcn,
                    $"element {elementId}' max cardinality is empty");
                return null;
            }

            return $"{e.Min.Value}..{e.Max}";
        }


        String ResolveAnnotation(GraphNode node,
            String annotationSource)
        {
            if (String.IsNullOrEmpty(annotationSource))
                return null;
            switch (annotationSource[0])
            {
                case '^':
                    return ResolveCardinality(node, annotationSource.Substring(1));

                default:
                    return annotationSource;
            }
        }

        IEnumerable<SENode> TraverseParents(GraphNode focusNode,
            SENode seFocusNode,
            String traversalFilter,
            Int32 depth)
        {
            Regex r = new Regex(traversalFilter);

            HashSet<GraphNode> parentNodes = new HashSet<GraphNode>();
            parentNodes.Add(focusNode);
            focusNode.ParentLinks.SortByTraversalName();

            foreach (GraphNode.Link parentLink in focusNode.ParentLinks)
            {
                if (
                    (depth >= 0) &&
                    (r.IsMatch(parentLink.Traversal.TraversalName)) &&
                    (parentNodes.Contains(parentLink.Node) == false)
                )
                {
                    SENode parent = CreateNode(parentLink.Node);
                    yield return parent;

                    //SENode child = CreateNode(parentLink.Node);
                    //parentContainer.AppendNode(child);

                    //parentContainer.AppendChildren(TraverseChildren(parentLink.Node,
                    //    child,
                    //    traversalFilter,
                    //    depth - parentLink.Depth));
                    //yield return parentContainer;
                }

            }
        }

        IEnumerable<SENodeGroup> TraverseChildren(GraphNode focusNode,
            SENode seFocusNode,
            String traversalFilter,
            Int32 depth)
        {
            Regex r = new Regex(traversalFilter);

            HashSet<GraphNode> childNodes = new HashSet<GraphNode>();
            childNodes.Add(focusNode);
            focusNode.ChildLinks.SortByTraversalName();

            foreach (GraphNode.Link childLink in focusNode.ChildLinks)
            {
                if (
                    (depth > 0) &&
                    (r.IsMatch(childLink.Traversal.TraversalName)) &&
                    (childNodes.Contains(childLink.Node) == false)
                )
                {
                    SENodeGroup childContainer = new SENodeGroup("Child", true);
                    SENode child = CreateNode(childLink.Node);
                    childContainer.AppendNode(child);

                    childContainer.AppendChildren(TraverseChildren(childLink.Node,
                        child,
                        traversalFilter,
                        depth - childLink.Depth));
                    yield return childContainer;
                }
            }
        }
    }
}
