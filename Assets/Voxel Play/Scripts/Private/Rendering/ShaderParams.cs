using UnityEngine;

namespace VoxelPlay
{

    static class ShaderParams
    {
        public static int TintColorArray = Shader.PropertyToID ("_TintColor");
        public static int PositionsArray = Shader.PropertyToID ("_Positions");
        public static int ColorsAndLightArray = Shader.PropertyToID ("_ColorsAndLight");
        public static int RotationsArray = Shader.PropertyToID ("_Rotations");
        public static int VoxelLight = Shader.PropertyToID ("_VoxelLight");
        public static int FlashDelay = Shader.PropertyToID ("_FlashDelay");
        public static int TexSides = Shader.PropertyToID ("_TexSides");
        public static int TexBottom = Shader.PropertyToID ("_TexBottom");
        public static int Color = Shader.PropertyToID ("_Color");
        public static int SeeThroughData = Shader.PropertyToID ("_VPSeeThroughData");
        public static int SeeThroughAlpha = Shader.PropertyToID("_VPSeeThroughAlpha");
        public static int AnimSeed = Shader.PropertyToID ("_AnimSeed");
        public static int MainTex = Shader.PropertyToID ("_MainTex");
        public static int MainTexArray = Shader.PropertyToID("_MainTexArray");
        public static int BaseMap = Shader.PropertyToID("_BaseMap");
        public static int BumpMap = Shader.PropertyToID("_BumpMap");
        public static int Width = Shader.PropertyToID("_Width");
        public static int FadeAmplitude = Shader.PropertyToID("_FadeAmplitude");
        public static int WaterLevel = Shader.PropertyToID("_WaterLevel");
        public static int WaterCausticsLevel = Shader.PropertyToID("_WaterCausticsLevel");
        public static int WaterColor = Shader.PropertyToID("_WaterColor");
        public static int WaveAmplitude = Shader.PropertyToID("_WaveAmplitude");
        public static int UnderWaterFogColor = Shader.PropertyToID("_UnderWaterFogColor");
        public static int InverseView = Shader.PropertyToID("_InverseView");
        public static int OutlineColor = Shader.PropertyToID("_OutlineColor");
        public static int OutlineThreshold = Shader.PropertyToID("_OutlineThreshold");
        public static int GhostAlpha = Shader.PropertyToID("_VPGhostAlpha");
        public static int ParallaxStrength = Shader.PropertyToID("_VPParallaxStrength");
        public static int ParallaxMaxDistanceSqr = Shader.PropertyToID("_VPParallaxMaxDistanceSqr");
        public static int ParallaxIterations = Shader.PropertyToID("_VPParallaxIterations");
        public static int ParallaxIterationsBinarySearch = Shader.PropertyToID("_VPParallaxIterationsBinarySearch");
        public static int WorldPivot = Shader.PropertyToID("_VPWorldPivot");

        // material props
        public static int MaterialProps = Shader.PropertyToID("_VPMatProps");
        public static int GradientLUT = Shader.PropertyToID("_VPGradientLUT");

        // lighting
        public static int SunLightColor = Shader.PropertyToID("_SunLightColor");
        public static int VPLightIntensity = Shader.PropertyToID("_VPLightIntensity");
        public static int DayTex = Shader.PropertyToID("_DayTex");
        public static int NightTex = Shader.PropertyToID("_NightTex");

        // realistic water
        public static int FoamColor = Shader.PropertyToID("_FoamColor");
        public static int WaveScale = Shader.PropertyToID("_WaveScale");
        public static int WaveSpeed = Shader.PropertyToID("_WaveSpeed");
        public static int SpecularIntensity = Shader.PropertyToID("_SpecularIntensity");
        public static int SpecularPower = Shader.PropertyToID("_SpecularPower");
        public static int RefractionDistortion = Shader.PropertyToID("_RefractionDistortion");
        public static int Fresnel = Shader.PropertyToID("_Fresnel");
        public static int NormalStrength = Shader.PropertyToID("_NormalStrength");
        public static int OceanWave = Shader.PropertyToID("_OceanWave");

        // globals
        public static int VPEmissionIntensity = Shader.PropertyToID("_VPEmissionIntensity");
        public static int VPFogData = Shader.PropertyToID("_VPFogData");
        public static int VPLightMaxDistSqr = Shader.PropertyToID("_VPPointMaxDistanceSqr");
        public static int VPSkyTint = Shader.PropertyToID("_VPSkyTint");
        public static int VPGroundColor = Shader.PropertyToID("_VPGroundColor");
        public static int VPFogTint = Shader.PropertyToID("_VPFogTint");
        public static int VPFogAmount = Shader.PropertyToID("_VPFogAmount");
        public static int VPExposure = Shader.PropertyToID("_VPExposure");
        public static int VPAmbientLight = Shader.PropertyToID("_VPAmbientLight");
        public static int VPDiffuseWrap = Shader.PropertyToID("_VPDiffuseWrap");
        public static int VPDaylightShadowAtten = Shader.PropertyToID("_VPDaylightShadowAtten");
        public static int VPGrassWindSpeed = Shader.PropertyToID("_VPGrassWindSpeed");
        public static int VPTreeWindSpeed = Shader.PropertyToID("_VPTreeWindSpeed");
        public static int VPObscuranceIntensity = Shader.PropertyToID("_VPObscuranceIntensity");
        public static int VPShadowTintColor = Shader.PropertyToID("_VPShadowTintColor");
        public static int VPBevelWidth = Shader.PropertyToID("_VPBevelWidth");
        public static int VPWaterDepthClip = Shader.PropertyToID("_VPWaterDepthClip");
    }

}