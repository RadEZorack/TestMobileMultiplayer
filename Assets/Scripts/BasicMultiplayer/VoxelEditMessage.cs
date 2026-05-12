using UnityEngine;

namespace BasicMultiplayer
{
    public enum VoxelEditAction
    {
        Place,
        Remove
    }

    public readonly struct VoxelEditMessage
    {
        public VoxelEditMessage(long sequence, int playerId, VoxelEditAction action, Vector3Int cell, string voxelType)
        {
            Sequence = sequence;
            PlayerId = playerId;
            Action = action;
            Cell = cell;
            VoxelType = voxelType;
        }

        public long Sequence { get; }
        public int PlayerId { get; }
        public VoxelEditAction Action { get; }
        public Vector3Int Cell { get; }
        public string VoxelType { get; }
    }
}
