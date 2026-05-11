using UnityEngine;

namespace BasicMultiplayer
{
    public static class BasicMultiplayerMaterials
    {
        private const string ShaderResourceName = "BasicMultiplayerColor";
        private static Shader shader;

        public static Material Create(Color color)
        {
            var material = new Material(GetShader());

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            return material;
        }

        private static Shader GetShader()
        {
            if (shader != null)
            {
                return shader;
            }

            shader = Resources.Load<Shader>(ShaderResourceName);

            if (shader == null)
            {
                shader = Shader.Find("BasicMultiplayer/Color");
            }

            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                Debug.LogError("Could not find a renderable shader for Basic Multiplayer materials.");
                shader = Shader.Find("Hidden/InternalErrorShader");
            }

            return shader;
        }
    }
}
