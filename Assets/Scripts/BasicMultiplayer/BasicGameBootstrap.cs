using UnityEngine;

namespace BasicMultiplayer
{
    public static class BasicGameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreatePrototypeObjects()
        {
            if (Object.FindAnyObjectByType<UdpGameClient>() != null)
            {
                return;
            }

            var root = new GameObject("Basic UDP Multiplayer");
            root.AddComponent<UdpGameClient>();
            root.AddComponent<PlayerWorldView>();
            root.AddComponent<VoxelPlayMultiplayerDemo>();

            EnsureCamera();
            EnsureArena();
        }

        private static void EnsureCamera()
        {
            var camera = Camera.main;

            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            camera.transform.SetPositionAndRotation(
                new Vector3(0f, 12f, -10f),
                Quaternion.Euler(55f, 0f, 0f));
            camera.fieldOfView = 55f;
            camera.clearFlags = CameraClearFlags.Skybox;
        }

        private static void EnsureArena()
        {
            if (GameObject.Find("Basic Multiplayer Arena") != null)
            {
                return;
            }

            var arena = GameObject.CreatePrimitive(PrimitiveType.Plane);
            arena.name = "Basic Multiplayer Arena";
            arena.transform.localScale = new Vector3(2f, 1f, 1.2f);
            arena.GetComponent<Renderer>().sharedMaterial = BasicMultiplayerMaterials.Create(new Color(0.18f, 0.25f, 0.2f));

            CreateWall("Top Wall", new Vector3(0f, 0.5f, 5.5f), new Vector3(20f, 1f, 0.25f));
            CreateWall("Bottom Wall", new Vector3(0f, 0.5f, -5.5f), new Vector3(20f, 1f, 0.25f));
            CreateWall("Left Wall", new Vector3(-9.5f, 0.5f, 0f), new Vector3(0.25f, 1f, 11f));
            CreateWall("Right Wall", new Vector3(9.5f, 0.5f, 0f), new Vector3(0.25f, 1f, 11f));
        }

        private static void CreateWall(string name, Vector3 position, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetPositionAndRotation(position, Quaternion.identity);
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().sharedMaterial = BasicMultiplayerMaterials.Create(new Color(0.32f, 0.34f, 0.36f));
        }
    }
}
