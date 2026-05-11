using System;
using UnityEngine;

namespace VoxelPlay {

    public delegate VoxelDefinition CustomVoxelDefinitionProviderDelegate (Vector3d position, VoxelDefinition vd, int rotation);
    public delegate VoxelDefinition CustomVoxelDefinitionForRenderingDelegate (Vector3d position, VoxelDefinition vd,
        int topCenterTypeIndex, int bottomCenterTypeIndex, int backLeftTypeIndex,
        int backTypeIndex, int backRightTypeIndex, int leftTypeIndex, int rightTypeIndex, int forwardLeftTypeIndex, int forwardTypeIndex, int forwardRightTypeIndex,
        int topBackLeftTypeIndex, int topBackTypeIndex, int topBackRightTypeIndex, int topLeftTypeIndex, int topRightTypeIndex, int topForwardLeftTypeIndex, int topForwardTypeIndex, int topForwardRightTypeIndex,
        int bottomBackLeftTypeIndex, int bottomBackTypeIndex, int bottomBackRightTypeIndex, int bottomLeftTypeIndex, int bottomRightTypeIndex, int bottomForwardLeftTypeIndex, int bottomForwardTypeIndex, int bottomForwardRightTypeIndex);

    public enum ConnectedVoxelConfigMatch {
        Anything,
        Equals,
        NotEquals,
        Empty,
        NotEmpty
    }

    public enum ConnectedVoxelConfigAction {
        Nothing,
        Replace,
        Random,
        Cycle
    }

    public enum ConnectedVoxelEvent {
        WhenPlacing,
        WhenRendering
    }

    [Serializable]
    public struct ConnectedVoxelConfig {
        public bool enabled;
        public ConnectedVoxelConfigMatch tl; // middle top left
        public ConnectedVoxelConfigMatch t; // middle top
        public ConnectedVoxelConfigMatch tr; // middle top right
        public ConnectedVoxelConfigMatch l; // middle left
        public ConnectedVoxelConfigMatch r; // middle right
        public ConnectedVoxelConfigMatch bl; // middle bottom left
        public ConnectedVoxelConfigMatch b; // middle bottom
        public ConnectedVoxelConfigMatch br; // middle bottom right

        public ConnectedVoxelConfigMatch tc; // top central
        public ConnectedVoxelConfigMatch bc; // bottom central

        public ConnectedVoxelConfigMatch tl2, t2, tr2, l2, r2, bl2, b2, br2; // rest of top slice
        public ConnectedVoxelConfigMatch tl0, t0, tr0, l0, r0, bl0, b0, br0; // rest of bottom slice

        public ConnectedVoxelConfigAction action;
        public VoxelDefinition replacementVoxelDefinition; // for Replace action
        public VoxelDefinition[] replacementVoxelDefinitionSet; // for Random/Cycle action
        public bool foldout;
    }

    [CreateAssetMenu(menuName = "Voxel Play/Connected Voxel", fileName = "ConnectedVoxel", order = 132)]
    public class ConnectedVoxel : ScriptableObject {

        public string description;

        [Tooltip("The voxel being placed.")]
        public VoxelDefinition voxelDefinition;

        [Tooltip("Also apply these rules to other voxel definitions.")]
        public VoxelDefinition[] additionalVoxelDefinitions;

        public ConnectedVoxelEvent ruleEvent = ConnectedVoxelEvent.WhenPlacing;

        [Tooltip("If true, vegetation will be ignored when checking if a neighbour is empty or not.")]
        public bool ignoreVegetation = true;

        [Tooltip("Rules that apply to this voxel.")]
        public ConnectedVoxelConfig[] config;

        VoxelPlayEnvironment env;
        int cycleIndex;
        VoxelIndex[] neighbours;
        int voxelDefinitionTypeIndex;
        int[] additionalVoxelDefinitionTypeIndices;
        int additionalVoxelDefinitionsCount;

        delegate bool CheckConfigRuleMatchDelegate (ConnectedVoxelConfigMatch match, int typeIndex);
        CheckConfigRuleMatchDelegate CheckConfigRuleMatch;


        public void Init (VoxelPlayEnvironment env) {
            this.env = env;
            if (voxelDefinition == null || config == null) return;

            neighbours = new VoxelIndex[27];

            ConfigureVoxelDefinition(voxelDefinition);
            // register voxel definitions just in case
            env.AddVoxelDefinition(voxelDefinition);

            if (additionalVoxelDefinitions != null) {
                additionalVoxelDefinitionsCount = additionalVoxelDefinitions.Length;
                additionalVoxelDefinitionTypeIndices = new int[additionalVoxelDefinitionsCount];
                for (int k = 0; k < additionalVoxelDefinitionsCount; k++) {
                    var vd = additionalVoxelDefinitions[k];
                    ConfigureVoxelDefinition(vd);
                    env.AddVoxelDefinition(vd);
                    additionalVoxelDefinitionTypeIndices[k] = vd.index;
                }
            } else {
                additionalVoxelDefinitionsCount = 0;
            }
            if (additionalVoxelDefinitionsCount > 0) {
                CheckConfigRuleMatch = ignoreVegetation ? CheckConfigRuleMatchIgnoreVegetationWithAdditionalVoxelDefinitions : CheckConfigRuleMatchExactWithAdditionalVoxelDefinitions;
            } else {
                CheckConfigRuleMatch = ignoreVegetation ? CheckConfigRuleMatchIgnoreVegetation : CheckConfigRuleMatchExact;
            }            

            foreach (var entry in config) {
                env.AddVoxelDefinition(entry.replacementVoxelDefinition);
                env.AddVoxelDefinitions(entry.replacementVoxelDefinitionSet);
            }

        }

        void ConfigureVoxelDefinition (VoxelDefinition vd) {

            if (vd == null) return;
            if (ruleEvent == ConnectedVoxelEvent.WhenPlacing) {
                vd.customVoxelDefinitionProvider = ResolveVoxelDefinition;
            } else {
                vd.customVoxelDefinitionForRendering = ResolveVoxelDefinitionForRendering;
            }

            vd.customVoxelDefinitionForRenderingRequiresForwardChunk = false;
            vd.customVoxelDefinitionForRenderingRequiresBackChunk = false;
            vd.customVoxelDefinitionForRenderingRequiresLeftChunk = false;
            vd.customVoxelDefinitionForRenderingRequiresRightChunk = false;
            vd.customVoxelDefinitionForRenderingRequiresTopChunk = false;
            vd.customVoxelDefinitionForRenderingRequiresBottomChunk = false;

            for (int k = 0; k < config.Length; k++) {
                // requires top chunk?
                if (config[k].tc != ConnectedVoxelConfigMatch.Anything ||
                    config[k].tl2 != ConnectedVoxelConfigMatch.Anything ||
                    config[k].t2 != ConnectedVoxelConfigMatch.Anything ||
                    config[k].tr2 != ConnectedVoxelConfigMatch.Anything ||
                    config[k].l2 != ConnectedVoxelConfigMatch.Anything ||
                    config[k].r2 != ConnectedVoxelConfigMatch.Anything ||
                    config[k].bl2 != ConnectedVoxelConfigMatch.Anything ||
                    config[k].b2 != ConnectedVoxelConfigMatch.Anything ||
                    config[k].br2 != ConnectedVoxelConfigMatch.Anything) {
                    vd.customVoxelDefinitionForRenderingRequiresTopChunk = true;
                }
                // requires bottom chunk?
                if (config[k].bc != ConnectedVoxelConfigMatch.Anything ||
                    config[k].tl0 != ConnectedVoxelConfigMatch.Anything ||
                    config[k].t0 != ConnectedVoxelConfigMatch.Anything ||
                    config[k].tr0 != ConnectedVoxelConfigMatch.Anything ||
                    config[k].l0 != ConnectedVoxelConfigMatch.Anything ||
                    config[k].r0 != ConnectedVoxelConfigMatch.Anything ||
                    config[k].bl0 != ConnectedVoxelConfigMatch.Anything ||
                    config[k].b0 != ConnectedVoxelConfigMatch.Anything ||
                    config[k].br0 != ConnectedVoxelConfigMatch.Anything) {
                    vd.customVoxelDefinitionForRenderingRequiresBottomChunk = true;
                }
                // requires back chunk?
                if (config[k].b != ConnectedVoxelConfigMatch.Anything ||
                    config[k].bl != ConnectedVoxelConfigMatch.Anything ||
                    config[k].br != ConnectedVoxelConfigMatch.Anything ||
                    config[k].bl2 != ConnectedVoxelConfigMatch.Anything ||
                    config[k].b2 != ConnectedVoxelConfigMatch.Anything ||
                    config[k].br2 != ConnectedVoxelConfigMatch.Anything ||
                    config[k].bl0 != ConnectedVoxelConfigMatch.Anything ||
                    config[k].b0 != ConnectedVoxelConfigMatch.Anything ||
                    config[k].br0 != ConnectedVoxelConfigMatch.Anything) {
                    vd.customVoxelDefinitionForRenderingRequiresBackChunk = true;
                }
                // requires left chunk?
                if (config[k].l != ConnectedVoxelConfigMatch.Anything ||
                    config[k].tl != ConnectedVoxelConfigMatch.Anything ||
                    config[k].bl != ConnectedVoxelConfigMatch.Anything ||
                    config[k].tl2 != ConnectedVoxelConfigMatch.Anything ||
                    config[k].l2 != ConnectedVoxelConfigMatch.Anything ||
                    config[k].bl2 != ConnectedVoxelConfigMatch.Anything ||
                    config[k].tl0 != ConnectedVoxelConfigMatch.Anything ||
                    config[k].l0 != ConnectedVoxelConfigMatch.Anything ||
                    config[k].bl0 != ConnectedVoxelConfigMatch.Anything) {
                    vd.customVoxelDefinitionForRenderingRequiresLeftChunk = true;
                }
                // requires right chunk?
                if (config[k].r != ConnectedVoxelConfigMatch.Anything ||
                    config[k].tr != ConnectedVoxelConfigMatch.Anything ||
                    config[k].br != ConnectedVoxelConfigMatch.Anything ||
                    config[k].tr2 != ConnectedVoxelConfigMatch.Anything ||
                    config[k].r2 != ConnectedVoxelConfigMatch.Anything ||
                    config[k].br2 != ConnectedVoxelConfigMatch.Anything ||
                    config[k].tr0 != ConnectedVoxelConfigMatch.Anything ||
                    config[k].r0 != ConnectedVoxelConfigMatch.Anything ||
                    config[k].br0 != ConnectedVoxelConfigMatch.Anything) {
                    vd.customVoxelDefinitionForRenderingRequiresRightChunk = true;
                }
                // requires forward chunk?
                if (config[k].t != ConnectedVoxelConfigMatch.Anything ||
                    config[k].tl != ConnectedVoxelConfigMatch.Anything ||
                    config[k].tr != ConnectedVoxelConfigMatch.Anything ||
                    config[k].tl2 != ConnectedVoxelConfigMatch.Anything ||
                    config[k].t2 != ConnectedVoxelConfigMatch.Anything ||
                    config[k].tr2 != ConnectedVoxelConfigMatch.Anything ||
                    config[k].tl0 != ConnectedVoxelConfigMatch.Anything ||
                    config[k].t0 != ConnectedVoxelConfigMatch.Anything ||
                    config[k].tr0 != ConnectedVoxelConfigMatch.Anything) {
                    vd.customVoxelDefinitionForRenderingRequiresForwardChunk = true;
                }
            }
        }


        public VoxelDefinition ResolveVoxelDefinition (Vector3d position, VoxelDefinition vd, int rotation) {
            if (env == null)
                return vd;

            env.GetVoxelNeighbourhood(position, ref neighbours, rotation);
            int forwardLeftTypeIndex = neighbours[15].typeIndex;
            int forwardTypeIndex = neighbours[16].typeIndex;
            int forwardRightTypeIndex = neighbours[17].typeIndex;
            int leftTypeIndex = neighbours[12].typeIndex;
            int rightTypeIndex = neighbours[14].typeIndex;
            int backLeftTypeIndex = neighbours[9].typeIndex;
            int backTypeIndex = neighbours[10].typeIndex;
            int backRightTypeIndex = neighbours[11].typeIndex;
            int topCenterTypeIndex = neighbours[22].typeIndex;
            int bottomCenterTypeIndex = neighbours[4].typeIndex;
            int topBackLeftTypeIndex = neighbours[18].typeIndex;
            int topBackTypeIndex = neighbours[19].typeIndex;
            int topBackRightTypeIndex = neighbours[20].typeIndex;
            int topLeftTypeIndex = neighbours[21].typeIndex;
            int topRightTypeIndex = neighbours[23].typeIndex;
            int topForwardLeftTypeIndex = neighbours[24].typeIndex;
            int topForwardTypeIndex = neighbours[25].typeIndex;
            int topForwardRightTypeIndex = neighbours[26].typeIndex;
            int bottomBackLeftTypeIndex = neighbours[0].typeIndex;
            int bottomBackTypeIndex = neighbours[1].typeIndex;
            int bottomBackRightTypeIndex = neighbours[2].typeIndex;
            int bottomLeftTypeIndex = neighbours[3].typeIndex;
            int bottomRightTypeIndex = neighbours[5].typeIndex;
            int bottomForwardLeftTypeIndex = neighbours[6].typeIndex;
            int bottomForwardTypeIndex = neighbours[7].typeIndex;
            int bottomForwardRightTypeIndex = neighbours[8].typeIndex;

            return ResolveVoxelDefinitionForRendering(position, vd,
                        topCenterTypeIndex, bottomCenterTypeIndex, backLeftTypeIndex,
        backTypeIndex, backRightTypeIndex, leftTypeIndex, rightTypeIndex, forwardLeftTypeIndex, forwardTypeIndex, forwardRightTypeIndex,
        topBackLeftTypeIndex, topBackTypeIndex, topBackRightTypeIndex, topLeftTypeIndex, topRightTypeIndex, topForwardLeftTypeIndex, topForwardTypeIndex, topForwardRightTypeIndex,
        bottomBackLeftTypeIndex, bottomBackTypeIndex, bottomBackRightTypeIndex, bottomLeftTypeIndex, bottomRightTypeIndex, bottomForwardLeftTypeIndex, bottomForwardTypeIndex, bottomForwardRightTypeIndex);
        }



        public VoxelDefinition ResolveVoxelDefinitionForRendering (Vector3d position, VoxelDefinition vd,
             int topCenterTypeIndex, int bottomCenterTypeIndex, int middleBackLeftTypeIndex,
        int middleBackTypeIndex, int middleBackRightTypeIndex, int middleLeftTypeIndex, int middleRightTypeIndex, int middleForwardLeftTypeIndex, int middleForwardTypeIndex, int middleForwardRightTypeIndex,
        int topBackLeftTypeIndex, int topBackTypeIndex, int topBackRightTypeIndex, int topLeftTypeIndex, int topRightTypeIndex, int topForwardLeftTypeIndex, int topForwardTypeIndex, int topForwardRightTypeIndex,
        int bottomBackLeftTypeIndex, int bottomBackTypeIndex, int bottomBackRightTypeIndex, int bottomLeftTypeIndex, int bottomRightTypeIndex, int bottomForwardLeftTypeIndex, int bottomForwardTypeIndex, int bottomForwardRightTypeIndex
            ) {
            if (config == null)
                return vd;

            voxelDefinitionTypeIndex = voxelDefinition == null ? 0 : voxelDefinition.index;
            int configLength = config.Length;
            for (int k = 0; k < configLength; k++) {
                // middle slice
                if (!CheckConfigRuleMatch(config[k].tl, middleForwardLeftTypeIndex)) continue;
                if (!CheckConfigRuleMatch(config[k].t, middleForwardTypeIndex)) continue;
                if (!CheckConfigRuleMatch(config[k].tr, middleForwardRightTypeIndex)) continue;
                if (!CheckConfigRuleMatch(config[k].l, middleLeftTypeIndex)) continue;
                if (!CheckConfigRuleMatch(config[k].r, middleRightTypeIndex)) continue;
                if (!CheckConfigRuleMatch(config[k].bl, middleBackLeftTypeIndex)) continue;
                if (!CheckConfigRuleMatch(config[k].b, middleBackTypeIndex)) continue;
                if (!CheckConfigRuleMatch(config[k].br, middleBackRightTypeIndex)) continue;
                // central top & bottom
                if (!CheckConfigRuleMatch(config[k].tc, topCenterTypeIndex)) continue;
                if (!CheckConfigRuleMatch(config[k].bc, bottomCenterTypeIndex)) continue;
                // rest of bottom slice
                if (!CheckConfigRuleMatch(config[k].tl0, bottomForwardLeftTypeIndex)) continue;
                if (!CheckConfigRuleMatch(config[k].t0, bottomForwardTypeIndex)) continue;
                if (!CheckConfigRuleMatch(config[k].tr0, bottomForwardRightTypeIndex)) continue;
                if (!CheckConfigRuleMatch(config[k].l0, bottomLeftTypeIndex)) continue;
                if (!CheckConfigRuleMatch(config[k].r0, bottomRightTypeIndex)) continue;
                if (!CheckConfigRuleMatch(config[k].bl0, bottomBackLeftTypeIndex)) continue;
                if (!CheckConfigRuleMatch(config[k].b0, bottomBackTypeIndex)) continue;
                if (!CheckConfigRuleMatch(config[k].br0, bottomRightTypeIndex)) continue;
                // rest of top slice
                if (!CheckConfigRuleMatch(config[k].tl2, topForwardLeftTypeIndex)) continue;
                if (!CheckConfigRuleMatch(config[k].t2, topForwardTypeIndex)) continue;
                if (!CheckConfigRuleMatch(config[k].tr2, topForwardRightTypeIndex)) continue;
                if (!CheckConfigRuleMatch(config[k].l2, topLeftTypeIndex)) continue;
                if (!CheckConfigRuleMatch(config[k].r2, topRightTypeIndex)) continue;
                if (!CheckConfigRuleMatch(config[k].bl2, topBackLeftTypeIndex)) continue;
                if (!CheckConfigRuleMatch(config[k].b2, topBackTypeIndex)) continue;
                if (!CheckConfigRuleMatch(config[k].br2, topBackRightTypeIndex)) continue;

                switch (config[k].action) {
                    case ConnectedVoxelConfigAction.Nothing:
                        vd = null;
                        break;
                    case ConnectedVoxelConfigAction.Replace:
                        vd = config[k].replacementVoxelDefinition;
                        break;
                    case ConnectedVoxelConfigAction.Random: {
                            VoxelDefinition[] replacementSet = config[k].replacementVoxelDefinitionSet;
                            if (replacementSet != null && replacementSet.Length > 0) {
                                int index = WorldRand.Range(0, replacementSet.Length, position);
                                vd = replacementSet[index];
                            }
                        }
                        break;
                    case ConnectedVoxelConfigAction.Cycle: {
                            VoxelDefinition[] replacementSet = config[k].replacementVoxelDefinitionSet;
                            if (replacementSet != null && replacementSet.Length > 0) {
                                cycleIndex++;
                                if (cycleIndex >= replacementSet.Length) {
                                    cycleIndex = 0;
                                }
                                vd = replacementSet[cycleIndex];
                            }
                        }
                        break;
                }
                break;
            }
            return vd; // rule executed, exit
        }


        bool CheckConfigRuleMatchExactWithAdditionalVoxelDefinitions (ConnectedVoxelConfigMatch match, int typeIndex) {
            switch (match) {
                case ConnectedVoxelConfigMatch.Empty: return typeIndex <= Voxel.HOLE_TYPE_INDEX;
                case ConnectedVoxelConfigMatch.NotEmpty: return typeIndex > Voxel.HOLE_TYPE_INDEX;
                case ConnectedVoxelConfigMatch.Equals:
                    if (voxelDefinitionTypeIndex == typeIndex) return true;
                    for (int k = 0; k < additionalVoxelDefinitionsCount; k++) {
                        if (additionalVoxelDefinitionTypeIndices[k] == typeIndex) return true;
                    }
                    return false;
                case ConnectedVoxelConfigMatch.NotEquals:
                    if (voxelDefinitionTypeIndex == typeIndex) return false;
                    for (int k = 0; k < additionalVoxelDefinitionsCount; k++) {
                        if (additionalVoxelDefinitionTypeIndices[k] == typeIndex) return false;
                    }
                    return true;
                default: return true;
            }
        }

        bool CheckConfigRuleMatchExact (ConnectedVoxelConfigMatch match, int typeIndex) {
            switch (match) {
                case ConnectedVoxelConfigMatch.Empty: return typeIndex <= Voxel.HOLE_TYPE_INDEX;
                case ConnectedVoxelConfigMatch.NotEmpty: return typeIndex > Voxel.HOLE_TYPE_INDEX;
                case ConnectedVoxelConfigMatch.Equals: return voxelDefinitionTypeIndex == typeIndex;
                case ConnectedVoxelConfigMatch.NotEquals: return voxelDefinitionTypeIndex != typeIndex;
                default: return true;
            }
        }
       bool CheckConfigRuleMatchIgnoreVegetationWithAdditionalVoxelDefinitions (ConnectedVoxelConfigMatch match, int typeIndex) {
            switch (match) {
                case ConnectedVoxelConfigMatch.Empty: return typeIndex <= Voxel.HOLE_TYPE_INDEX || env.voxelDefinitions[typeIndex].renderType == RenderType.CutoutCross;
                case ConnectedVoxelConfigMatch.NotEmpty: return typeIndex > Voxel.HOLE_TYPE_INDEX && env.voxelDefinitions[typeIndex].renderType != RenderType.CutoutCross;
                case ConnectedVoxelConfigMatch.Equals:
                    if (voxelDefinitionTypeIndex == typeIndex) return true;
                    for (int k = 0; k < additionalVoxelDefinitionsCount; k++) {
                        if (additionalVoxelDefinitionTypeIndices[k] == typeIndex) return true;
                    }
                    return false;
                case ConnectedVoxelConfigMatch.NotEquals:
                    if (voxelDefinitionTypeIndex == typeIndex) return false;
                    for (int k = 0; k < additionalVoxelDefinitionsCount; k++) {
                        if (additionalVoxelDefinitionTypeIndices[k] == typeIndex) return false;
                    }
                    return true;
                default: return true;
            }
        }

        bool CheckConfigRuleMatchIgnoreVegetation (ConnectedVoxelConfigMatch match, int typeIndex) {
            switch (match) {
                case ConnectedVoxelConfigMatch.Empty: return typeIndex <= Voxel.HOLE_TYPE_INDEX || env.voxelDefinitions[typeIndex].renderType == RenderType.CutoutCross;
                case ConnectedVoxelConfigMatch.NotEmpty: return typeIndex > Voxel.HOLE_TYPE_INDEX && env.voxelDefinitions[typeIndex].renderType != RenderType.CutoutCross;
                case ConnectedVoxelConfigMatch.Equals: return voxelDefinitionTypeIndex == typeIndex;
                case ConnectedVoxelConfigMatch.NotEquals: return voxelDefinitionTypeIndex != typeIndex;
                default: return true;
            }
        }
    }

    public partial class VoxelDefinition : ScriptableObject {

        [NonSerialized]
        public CustomVoxelDefinitionProviderDelegate customVoxelDefinitionProvider;

        [NonSerialized]
        public CustomVoxelDefinitionForRenderingDelegate customVoxelDefinitionForRendering;

        [NonSerialized]
        public bool customVoxelDefinitionForRenderingRequiresForwardChunk;

        [NonSerialized]
        public bool customVoxelDefinitionForRenderingRequiresBackChunk;

        [NonSerialized]
        public bool customVoxelDefinitionForRenderingRequiresLeftChunk;

        [NonSerialized]
        public bool customVoxelDefinitionForRenderingRequiresRightChunk;

        [NonSerialized]
        public bool customVoxelDefinitionForRenderingRequiresTopChunk;

        [NonSerialized]
        public bool customVoxelDefinitionForRenderingRequiresBottomChunk;
    }

}
