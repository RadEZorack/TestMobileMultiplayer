using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;


namespace VoxelPlay.GPURendering.InstancingIndirect {

    class Batch {
        public const int MAX_INSTANCES = 65000;
        public Vector4[] positions;  // w = uniform scale
        public Vector4[] colorsAndLight;
        public Vector4[] rotations;
        public int instancesCount;
        public Bounds bounds;
        public Vector3 boundsMin, boundsMax;
        public Material[] instancedMaterials;
        public bool usesRotation;

        public ComputeBuffer[] argsBuffers;
        public ComputeBuffer positionsBuffer, colorsAndLightBuffer, rotationsBuffer;


        public void Init() {
            instancesCount = 0;
            if (positionsBuffer == null) {
                colorsAndLight = new Vector4[MAX_INSTANCES];
                colorsAndLightBuffer = new ComputeBuffer(MAX_INSTANCES, 16);
                positions = new Vector4[MAX_INSTANCES];
                positionsBuffer = new ComputeBuffer(MAX_INSTANCES, 4 * sizeof(float));
                rotations = new Vector4[MAX_INSTANCES];
                rotationsBuffer = new ComputeBuffer(MAX_INSTANCES, 4 * sizeof(float));
            }
            bounds = new Bounds();
            boundsMin = Misc.vector3max;
            boundsMax = Misc.vector3min;
            usesRotation = false;
        }

        public void EnsureArgsBuffers(int subMeshCount) {
            if (argsBuffers != null && argsBuffers.Length >= subMeshCount)
                return;
            if (argsBuffers != null) {
                for (int i = 0; i < argsBuffers.Length; i++) {
                    if (argsBuffers[i] != null) {
                        argsBuffers[i].Release();
                    }
                }
            }
            argsBuffers = new ComputeBuffer[subMeshCount];
            for (int i = 0; i < subMeshCount; i++) {
                argsBuffers[i] = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
            }
        }

        public void DisposeBuffers() {
            if (rotationsBuffer != null) {
                rotationsBuffer.Release();
                rotationsBuffer = null;
            }
            if (colorsAndLightBuffer != null) {
                colorsAndLightBuffer.Release();
                colorsAndLightBuffer = null;
            }
            if (positionsBuffer != null) {
                positionsBuffer.Release();
                positionsBuffer = null;
            }
            if (argsBuffers != null) {
                for (int i = 0; i < argsBuffers.Length; i++) {
                    if (argsBuffers[i] != null) {
                        argsBuffers[i].Release();
                        argsBuffers[i] = null;
                    }
                }
                argsBuffers = null;
            }
            positions = null;
            colorsAndLight = null;
            rotations = null;
            instancedMaterials = null;
        }

        public void UpdateBounds(Vector4 position, Vector3 size) {
            FastVector.ExpandBounds(ref boundsMin, ref boundsMax, position, size);
        }

        public void ComputeBounds() {
            bounds = new Bounds((boundsMin + boundsMax) * 0.5f, boundsMax - boundsMin);
        }
    }

}
