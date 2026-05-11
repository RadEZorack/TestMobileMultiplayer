
            #define RAY_STEPS 200
            #define BSEARCH_STEPS 8
            #define RAY_SHADOW_STEPS 100

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 rayDir : TEXCOORD1;
            };

            sampler2D _TerrainFarChunksTex;
            sampler2D _CloudsTex;
            Texture2D _WaterTex;
            SamplerState water_linear_repeat_sampler;

            float4 _SnapshotData;
            half4 _WaterColor;
            half3 _ShoreColor;
            float _WaterLevel;
            float _TerrainMaxAltitude;
            half _ShadowIntensity;
            half _WaterReflectionsIntensity;
            half _SpecularPower;
            half _SpecularIntensity;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // places the quad on the far clip
                #if defined(UNITY_REVERSED_Z)
                    o.pos.z = 1.0e-9f;
                #else
                    o.pos.z = o.pos.w - 1.0e-6f;
                #endif

                o.uv = v.uv;
                float3 wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.rayDir = wpos - _WorldSpaceCameraPos;
                return o;
            }

            inline fixed4 ReadSmoothTexel2D(sampler tex, float2 textureSize, float2 uv) {
                float2 ruv = uv.xy * textureSize - 0.5;
                float2 f = fwidth(ruv);
                uv.xy = (floor(ruv) + 0.5 + saturate( (frac(ruv) - 0.5 + f ) / f)) / textureSize; 
                return tex2D(tex, uv);
            }            

            half4 frag (v2f i) : SV_Target
            {
                // sample the terrain texture
                float3 rayDir = normalize(i.rayDir);
                half4 color = half4(0,0,0,0);
                float3 rayStart = _WorldSpaceCameraPos - _VPWorldPivot;

                float3 wpos;
                float dither = frac(dot(float2(2.4084507, 3.2535211), i.pos.xy));
                float incr = 1.015;
                float t = 16 + dither;
                for (int k = 0; k < RAY_STEPS; k++) {
                    wpos = rayStart + rayDir * t;
                    VOXELPLAY_MODIFY_WPOS_SCREENSPACE(wpos);
                    wpos = floor(wpos) + 0.5;
                    float2 tpos = (wpos.xz - _SnapshotData.xy) / _SnapshotData.z;
                    float4 terrain = tex2Dlod(_TerrainFarChunksTex, float4(tpos, 0, 0));
                    float terrainAltitude = terrain.a * _TerrainMaxAltitude;
                    if (wpos.y < terrainAltitude + 0.5) {
                        color = half4(terrain.rgb, 1.0);
                        break;
                    } 
                    t ++;
                    t *= incr;
                }

                if (color.a == 0) {
                    return 0;
                }

                // refine hit position using binary search                
                float t1 = t;
                float t0 = (t / incr) - 1;
                float3 hpos = wpos;
                half ao = 0.9999;
                for (int i = 0; i < BSEARCH_STEPS; i++) {
                    t = (t1 + t0) * 0.5;
                    wpos = rayStart + rayDir * t;
                    VOXELPLAY_MODIFY_WPOS_SCREENSPACE(wpos);
                    hpos = wpos;
                    wpos = floor(wpos) + 0.5;
                    float2 tpos = (wpos.xz - _SnapshotData.xy) / _SnapshotData.z;
                    float4 terrain = tex2Dlod(_TerrainFarChunksTex, float4(tpos, 0, 0));
                    float terrainAltitude = terrain.a * _TerrainMaxAltitude;
                    if (wpos.y < terrainAltitude + 0.5) {
                        t1 = t;
                        color = half4(terrain.rgb, 1.0);
                        ao = hpos.y;
                    } else {
                        t0 = t;
                    }
                }

                ao = frac(ao);
                ao = 0.25 + ao * 0.75;
                ao = 1.05-(1.0-ao)*(1.0-ao);
                float aoFade = max(0, (t - 256) / 32);
                ao = saturate(ao + aoFade);

                // compute if pixel is under shadow by casting a ray from pixel to the Sun
                half atten = 1.0;
                #if _SHADOWS
                    float3 rpos = hpos;
                    float v = 2; // Shadow ray starting distance
                    for (int j = 0; j < RAY_SHADOW_STEPS; j++) {
                        rpos = hpos + _WorldSpaceLightPos0.xyz * v;
                        VOXELPLAY_MODIFY_WPOS_SCREENSPACE(rpos);
                        if (rpos.y > _TerrainMaxAltitude) {
                            break; // Above terrain max altitude so in direct light
                        }
                        float2 tpos = (rpos.xz - _SnapshotData.xy) / _SnapshotData.z;
                        float4 terrain = tex2Dlod(_TerrainFarChunksTex, float4(tpos, 0, 0));
                        float terrainAltitude = terrain.a * _TerrainMaxAltitude;
                        if (rpos.y < terrainAltitude) {
                            atten = _ShadowIntensity;
                            break;
                        }
                        v ++;
                        v *= incr;
                    }
                #endif    

                // compute normal
                float3 dc = abs(hpos - wpos);
                dc.y *= 1.05; // avoid artifacts at the edges
                float3 signs = -sign(rayDir);
                float3 norm = float3(0, signs.y, 0);
                if (dc.z > dc.x && dc.z > dc.y) norm = float3(0, 0, signs.z);
                if (dc.x > dc.z && dc.x > dc.y) norm = float3(signs.x, 0, 0);
              
                // shore
                if (hpos.y < _WaterLevel + 1.03 && hpos.y > _WaterLevel + 0.03) {
                    color.rgb = lerp(_ShoreColor, color.rgb, t/1024);
                }

                // add water
                if (hpos.y < _WaterLevel + 0.8) {
                    norm = float3(0, 0.98, 0);
                    float2 waterPos = hpos.xz + _Time.xx;
                    half4 waterTexture = _WaterTex.Sample(water_linear_repeat_sampler, waterPos);
                    half4 waterColor = lerp(_WaterColor, waterTexture, 0.3);
                    color.rgb = lerp(color.rgb, waterColor.rgb, waterTexture.a);

                    #if _WATER_REFLECTIONS
                       // reflections
                       float3 rpos = hpos;
                       float3 reflDir = reflect(rayDir, norm);
                       float v = 2; // Shadow ray starting distance
                       half4 reflColor = half4(0,0,0,0);
                       for (int j = 0; j < RAY_SHADOW_STEPS; j++) {
                            rpos = hpos + reflDir * v;
                            VOXELPLAY_MODIFY_WPOS(rpos);
                            if (rpos.y > _TerrainMaxAltitude) {
                               break; // Above terrain max altitude so in direct light
                            }
                            float2 tpos = (rpos.xz - _SnapshotData.xy) / _SnapshotData.z;
                            float4 terrain = tex2Dlod(_TerrainFarChunksTex, float4(tpos, 0, 0));
                            float terrainAltitude = terrain.a * _TerrainMaxAltitude;
                            if (rpos.y < terrainAltitude) {
                               reflColor = half4(terrain.rgb, _WaterReflectionsIntensity);
                               if (rpos.y < _WaterLevel + 1.03) reflColor.rgb = 0.2;
                               break;
                            }
                            v ++;
                            v *= incr;
                        }

                        color.rgb = lerp(color.rgb, reflColor.rgb, reflColor.a * atten);
                   #endif
                   
                    // add specular
                    float3 h = normalize (_WorldSpaceLightPos0.xyz - rayDir);
                    h *= (sign(_WorldSpaceLightPos0.y) + 1.0) * 0.5; // avoid specular under the horizon
                    float nh = max (0, dot (norm, h));
                    float spec = pow (nh, _SpecularPower);
                    color.rgb += (_SpecularIntensity * atten * spec) * _LightColor0.rgb; // this should be attenuated by shadows (atten) but since real chunks are not present, they don't get shadows either so..
                }

                // lighting matching regular VP shader with spherical harmonics ambient
                half NdotL = saturate(dot(_WorldSpaceLightPos0.xyz, norm));
                half dayLight = NdotL * saturate(1.0 + _WorldSpaceLightPos0.y * 2.0);
                half lightAtten = saturate(atten * dayLight + _WorldSpaceLightPos0.y * _VPDaylightShadowAtten);
                
                // Add spherical harmonics ambient lighting to match regular voxel lighting
                fixed3 ambientLight = GetAmbientLighting(norm);
                #if defined(USES_COLORED_SHADOWS)
                    fixed3 shadowColor = lerp(_VPShadowTintColor, 1.0, atten);
                #else
                    fixed3 shadowColor = atten;
                #endif
                color.rgb *= min((shadowColor * lightAtten * ao + ambientLight) * _LightColor0.rgb, 1.2);

                // add sky and fog contribution
                #if VOXELPLAY_GLOBAL_USE_FOG
                    half3 skyColor = getSkyColor(rayDir);
                    half fogFactor = saturate( (t*t - _VPFogData.x) / _VPFogData.y);
                    color.rgb = lerp(color.rgb, skyColor, fogFactor);
                #endif

                return color;
        
            }
        