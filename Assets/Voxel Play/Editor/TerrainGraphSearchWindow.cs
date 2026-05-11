using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

using UnityEditor.Experimental.GraphView;

namespace VoxelPlay {

    public class TerrainGraphSearchWindow : ScriptableObject, ISearchWindowProvider {
        const string REROUTE_ENTRY = "__reroute__";

        public TerrainGraphView graphView;
        Texture2D blankIcon;

        public void Init(TerrainGraphView view) {
            graphView = view;
            // GraphView search requires a 1px transparent icon to indent entries
            blankIcon = new Texture2D(1, 1);
            blankIcon.SetPixel(0, 0, Color.clear);
            blankIcon.Apply();
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context) {
            var tree = new List<SearchTreeEntry> {
                new SearchTreeGroupEntry(new GUIContent("Create Terrain Node"))
            };

            // Group operations by category
            AddCategory(tree, "Samplers", new[] {
                TerrainStepType.SampleHeightMapTexture,
                TerrainStepType.SampleRidgeNoiseFromTexture,
                TerrainStepType.SampleHeightMapFractal,
                TerrainStepType.SampleHeightMapUnityTerrain,
                TerrainStepType.Constant,
                TerrainStepType.Random
            });

            AddCategory(tree, "Math", new[] {
                TerrainStepType.Shift,
                TerrainStepType.Invert,
                TerrainStepType.AddAndMultiply,
                TerrainStepType.MultiplyAndAdd,
                TerrainStepType.Exponential,
                TerrainStepType.Remap,
                TerrainStepType.Abs,
                TerrainStepType.Terraces,
                TerrainStepType.FlattenOrRaise,
                TerrainStepType.Clamp,
                TerrainStepType.Min,
                TerrainStepType.Max,
                TerrainStepType.Subtract,
                TerrainStepType.Divide,
                TerrainStepType.Island
            });

            AddCategory(tree, "Blending", new[] {
                TerrainStepType.BlendAdditive,
                TerrainStepType.BlendMultiply
            });

            AddCategory(tree, "Filters", new[] {
                TerrainStepType.Threshold,
                TerrainStepType.Select,
                TerrainStepType.Fill,
                TerrainStepType.Test,
                TerrainStepType.Copy,
                TerrainStepType.BeachMask
            });

            tree.Add(new SearchTreeGroupEntry(new GUIContent("Utilities"), 1));
            tree.Add(new SearchTreeEntry(new GUIContent("Reroute", blankIcon)) {
                level = 2,
                userData = REROUTE_ENTRY
            });

            return tree;
        }

        void AddCategory(List<SearchTreeEntry> tree, string category, TerrainStepType[] ops) {
            tree.Add(new SearchTreeGroupEntry(new GUIContent(category), 1));
            foreach (var op in ops) {
                tree.Add(new SearchTreeEntry(new GUIContent(TerrainStepNode.GetOperationLabel(op), blankIcon)) {
                    level = 2,
                    userData = op
                });
            }
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context) {
            if (graphView == null) return false;
            if (entry.userData is string utility && utility == REROUTE_ENTRY) {
                graphView.CreateRerouteNode(graphView.GetPointerRerouteLocal());
                return true;
            }

            var op = (TerrainStepType)entry.userData;
            graphView.CreateNode(op, graphView.GetPointerCreationLocal(op));
            return true;
        }

        void OnDestroy() {
            if (blankIcon != null) {
                DestroyImmediate(blankIcon);
            }
        }
    }
}
