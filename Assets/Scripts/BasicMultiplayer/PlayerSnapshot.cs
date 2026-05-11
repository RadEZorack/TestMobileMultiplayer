using UnityEngine;

namespace BasicMultiplayer
{
    public readonly struct PlayerSnapshot
    {
        public PlayerSnapshot(int id, Vector2 position)
        {
            Id = id;
            Position = position;
        }

        public int Id { get; }
        public Vector2 Position { get; }
    }
}
