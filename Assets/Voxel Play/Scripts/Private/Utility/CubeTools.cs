using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelPlay {

    [Serializable]
    public enum CubeShadingStyle {
        Color = 0,
        ColorAlpha = 1,
        Textured = 10,
        TexturedAlpha = 11
    }

    [Serializable]
    public struct CubeSideSettings {
        public Texture2D texture;
        public Color color;
        public Vector2 uvMin;
        public Vector2 uvMax;
    }

    public static class CubeTools {

        static readonly string[] sideNames = { "Top", "Bottom", "Forward", "Back", "Left", "Right" };

        public static string[] SideNames => sideNames;

        /// <summary>
        /// Generates a mesh for a cube with the specified settings
        /// </summary>
        /// <param name="sides">Side settings for each face (6 faces: top, bottom, forward, back, left, right)</param>
        /// <param name="scale">Scale to apply to vertices</param>
        /// <param name="offset">Offset to apply to vertices</param>
        /// <param name="shadingStyle">Type of shading to use</param>
        /// <returns>Generated mesh</returns>
        public static Mesh GenerateCubeMesh (CubeSideSettings[] sides, Vector3 scale, Vector3 offset, CubeShadingStyle shadingStyle) {
            if (sides == null || sides.Length < 6) {
                throw new ArgumentException("sides array must contain at least 6 elements");
            }

            List<Vector3> tempVertices = new List<Vector3>(36);
            List<Vector3> tempNormals = new List<Vector3>(36);
            List<Vector2> tempUVs = new List<Vector2>(36);
            List<Color32> tempColors = new List<Color32>(36);
            int[] tempIndices = new int[36];
            int tempIndicesPos = 0;

            Mesh mesh = new Mesh();

            // Add faces in order: top, bottom, forward, back, left, right
            AddFace(Cube.faceVerticesTop, Cube.normalsUp, sides[0].color, sides[0].uvMin, sides[0].uvMax,
                    tempVertices, tempNormals, tempUVs, tempColors, tempIndices, ref tempIndicesPos, scale, offset);
            AddFace(Cube.faceVerticesBottom, Cube.normalsDown, sides[1].color, sides[1].uvMin, sides[1].uvMax,
                    tempVertices, tempNormals, tempUVs, tempColors, tempIndices, ref tempIndicesPos, scale, offset);
            AddFace(Cube.faceVerticesForward, Cube.normalsForward, sides[2].color, sides[2].uvMin, sides[2].uvMax,
                    tempVertices, tempNormals, tempUVs, tempColors, tempIndices, ref tempIndicesPos, scale, offset);
            AddFace(Cube.faceVerticesBack, Cube.normalsBack, sides[3].color, sides[3].uvMin, sides[3].uvMax,
                    tempVertices, tempNormals, tempUVs, tempColors, tempIndices, ref tempIndicesPos, scale, offset);
            AddFace(Cube.faceVerticesLeft, Cube.normalsLeft, sides[4].color, sides[4].uvMin, sides[4].uvMax,
                    tempVertices, tempNormals, tempUVs, tempColors, tempIndices, ref tempIndicesPos, scale, offset);
            AddFace(Cube.faceVerticesRight, Cube.normalsRight, sides[5].color, sides[5].uvMin, sides[5].uvMax,
                    tempVertices, tempNormals, tempUVs, tempColors, tempIndices, ref tempIndicesPos, scale, offset);

            mesh.SetVertices(tempVertices);
            if (shadingStyle != CubeShadingStyle.Color) {
                mesh.SetUVs(0, tempUVs);
            }
            mesh.SetNormals(tempNormals);
            mesh.SetColors(tempColors);
            mesh.triangles = tempIndices;

            return mesh;
        }

        /// <summary>
        /// Packs textures from side settings into a texture atlas
        /// </summary>
        /// <param name="sides">Side settings containing textures to pack</param>
        /// <param name="textureAtlasSize">Size of the resulting atlas</param>
        /// <returns>Packed texture atlas</returns>
        public static Texture2D PackTextures (CubeSideSettings[] sides, int textureAtlasSize) {
            if (sides == null || sides.Length < 6) {
                throw new ArgumentException("sides array must contain at least 6 elements");
            }

            List<Texture2D> textures = new List<Texture2D>();
            for (int k = 0; k < sides.Length; k++) {
                if (sides[k].texture == null) {
                    textures.Add(Texture2D.whiteTexture);
                } else {
                    textures.Add(sides[k].texture);
                }
            }

            Texture2D tex = new Texture2D(textureAtlasSize, textureAtlasSize);
            Rect[] uvRects = tex.PackTextures(textures.ToArray(), 4, textureAtlasSize);

            if (uvRects == null) {
                return null;
            }

            // Update UV coordinates in side settings
            for (int k = 0; k < uvRects.Length && k < sides.Length; k++) {
                sides[k].uvMin = uvRects[k].min;
                sides[k].uvMax = uvRects[k].max;
            }

            return tex;
        }

        /// <summary>
        /// Gets the appropriate material for the specified shading style
        /// </summary>
        public static Material GetMaterialForShadingStyle (CubeShadingStyle shadingStyle) {
            return shadingStyle switch {
                CubeShadingStyle.Textured => Resources.Load<Material>("VoxelPlay/Materials/VP Model Texture"),
                CubeShadingStyle.TexturedAlpha => Resources.Load<Material>("VoxelPlay/Materials/VP Model Texture Alpha"),
                CubeShadingStyle.ColorAlpha => Resources.Load<Material>("VoxelPlay/Materials/VP Model Color Alpha"),
                _ => Resources.Load<Material>("VoxelPlay/Materials/VP Model VertexLit")
            };
        }

        /// <summary>
        /// Adds a face to the mesh data
        /// </summary>
        static void AddFace (Vector3[] faceVertices, Vector3[] normals, Color32 color, Vector2 uvMin, Vector2 uvMax,
                           List<Vector3> tempVertices, List<Vector3> tempNormals, List<Vector2> tempUVs, List<Color32> tempColors,
                           int[] tempIndices, ref int tempIndicesPos, Vector3 scale, Vector3 offset) {
            int index = tempVertices.Count;

            // Add vertices with scale and offset
            for (int i = 0; i < 4; i++) {
                Vector3 v = faceVertices[i];
                v.x = v.x * scale.x + offset.x;
                v.y = v.y * scale.y + offset.y;
                v.z = v.z * scale.z + offset.z;
                tempVertices.Add(v);
                tempNormals.Add(normals[i]);
            }

            // Add triangle indices (two triangles per face)
            tempIndices[tempIndicesPos++] = index;
            tempIndices[tempIndicesPos++] = index + 1;
            tempIndices[tempIndicesPos++] = index + 3;
            tempIndices[tempIndicesPos++] = index + 3;
            tempIndices[tempIndicesPos++] = index + 2;
            tempIndices[tempIndicesPos++] = index;

            // Add UV coordinates
            Vector2 uv = uvMin;
            tempUVs.Add(uv);
            uv.y = uvMax.y;
            tempUVs.Add(uv);
            uv.x = uvMax.x;
            uv.y = uvMin.y;
            tempUVs.Add(uv);
            uv.y = uvMax.y;
            tempUVs.Add(uv);

            // Add vertex colors
            tempColors.Add(color);
            tempColors.Add(color);
            tempColors.Add(color);
            tempColors.Add(color);
        }


    }
}