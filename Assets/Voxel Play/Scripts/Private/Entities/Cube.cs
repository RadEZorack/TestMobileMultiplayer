using UnityEngine;

namespace VoxelPlay {

    public static class Cube {

        public enum Side {
            Back,
            Right,
            Forward,
            Left,
            Top,
            Bottom
        }

        public static readonly Vector3[] faceVerticesForward = {
            new Vector3 (0.5f, -0.5f, 0.5f),
            new Vector3 (0.5f, 0.5f, 0.5f),
            new Vector3 (-0.5f, -0.5f, 0.5f),
            new Vector3 (-0.5f, 0.5f, 0.5f)
        };
        public static readonly Vector3[] faceVerticesBack ={
            new Vector3 (-0.5f, -0.5f, -0.5f),
            new Vector3 (-0.5f, 0.5f, -0.5f),
            new Vector3 (0.5f, -0.5f, -0.5f),
            new Vector3 (0.5f, 0.5f, -0.5f)
        };
        public static readonly Vector3[] faceVerticesLeft = {
            new Vector3 (-0.5f, -0.5f, 0.5f),
            new Vector3 (-0.5f, 0.5f, 0.5f),
            new Vector3 (-0.5f, -0.5f, -0.5f),
            new Vector3 (-0.5f, 0.5f, -0.5f)
        };
        public static readonly Vector3[] faceVerticesRight = {
            new Vector3 (0.5f, -0.5f, -0.5f),
            new Vector3 (0.5f, 0.5f, -0.5f),
            new Vector3 (0.5f, -0.5f, 0.5f),
            new Vector3 (0.5f, 0.5f, 0.5f)
        };
        public static readonly Vector3[] faceVerticesTop =  {
            new Vector3 (-0.5f, 0.5f, -0.5f),
            new Vector3 (-0.5f, 0.5f, 0.5f),
            new Vector3 (0.5f, 0.5f, -0.5f),
            new Vector3 (0.5f, 0.5f, 0.5f)
        };
        public static readonly Vector3[] faceVerticesTopFlipped =  {
            new Vector3 (-0.5f, 0.5f, 0.5f),
            new Vector3 (-0.5f, 0.5f, -0.5f),
            new Vector3 (0.5f, 0.5f, 0.5f),
            new Vector3 (0.5f, 0.5f, -0.5f)
        };
        public static readonly Vector3[] faceVerticesBottom = {
            new Vector3 (0.5f, -0.5f, -0.5f),
            new Vector3 (0.5f, -0.5f, 0.5f),
            new Vector3 (-0.5f, -0.5f, -0.5f),
            new Vector3 (-0.5f, -0.5f, 0.5f)
        };

        public static readonly Vector3[] normalsBack = {
            Misc.vector3back, Misc.vector3back, Misc.vector3back, Misc.vector3back
        };
        public static readonly Vector3[] normalsForward = {
            Misc.vector3forward, Misc.vector3forward, Misc.vector3forward, Misc.vector3forward
        };
        public static readonly Vector3[] normalsLeft = {
            Misc.vector3left, Misc.vector3left, Misc.vector3left, Misc.vector3left
        };
        public static readonly Vector3[] normalsRight = {
            Misc.vector3right, Misc.vector3right, Misc.vector3right, Misc.vector3right
        };
        public static readonly Vector3[] normalsUp = {
            Misc.vector3up, Misc.vector3up, Misc.vector3up, Misc.vector3up
        };
        public static readonly Vector3[] normalsDown =  {
            Misc.vector3down, Misc.vector3down, Misc.vector3down, Misc.vector3down
        };

        static Vector4 rotation0 = new Vector4(1f, 0f, 0f, 1f);
        static Vector4 rotation1 = new Vector4(0f, 1f, -1f, 0f);
        static Vector4 rotation2 = new Vector4(-1f, 0f, 0f, -1f);
        static Vector4 rotation3 = new Vector4(0f, -1f, 1f, 0f);

        public static readonly Vector4[] rotatedVertexFactors = {
            rotation0, rotation1, rotation2, rotation3
        };

        public static readonly Vector4[] counterRotatedVertexFactors = {
            rotation0, -rotation1, rotation2, -rotation3
        };
    }
}
