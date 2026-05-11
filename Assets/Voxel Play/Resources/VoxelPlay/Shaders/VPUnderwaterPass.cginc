
			half4 _Color;
			float _WaterLevel, _WaterCausticsLevel, _WaveAmplitude;
			sampler2D _WaterCaustics;
			sampler3D _Noise3D;
			half4 _UnderWaterFogColor;

			struct appdata
			{
				float4 vertex : POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float4 pos     : SV_POSITION;
				fixed light    : TEXCOORD0;
				float3 wpos    : TEXCOORD1;
				float4 grabPos : TEXCOORD2;
				UNITY_VERTEX_OUTPUT_STEREO
			};


			v2f vert (appdata v)
			{
				v2f o;

				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_OUTPUT(v2f, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				o.pos     = UnityObjectToClipPos(v.vertex);
				o.wpos 	  = mul(unity_ObjectToWorld, v.vertex).xyz;
	       		o.light   = saturate(max(0, _WorldSpaceLightPos0.y * 2.0));
				o.grabPos = ComputeGrabScreenPos(o.pos);

				#if defined(UNITY_REVERSED_Z)
					o.pos.z = o.pos.w * UNITY_NEAR_CLIP_VALUE * 0.99999; //  0.99999 avoids precision issues on some Android devices causing unexpected clipping of light mesh
				#else
					o.pos.z = o.pos.w - 1.0e-6f;
				#endif

				return o;
			}

			#if defined(UNDERWATER_HQ)


			float4x4 _InverseView;

			float3 GetWorldPosFromRawDepth(float2 uv, float depth) {
				const float2 p11_22 = float2(unity_CameraProjection._11, unity_CameraProjection._22);
				const float2 p13_31 = float2(unity_CameraProjection._13, unity_CameraProjection._23);
				const float isOrtho = unity_OrthoParams.w;
				const float near = _ProjectionParams.y;
				const float far = _ProjectionParams.z;

				#if defined(UNITY_REVERSED_Z)
					depth = 1 - depth;
				#endif
				float zOrtho = lerp(near, far, depth);
				float zPers = near * far / lerp(far, near, depth);
				float vz = lerp(zPers, zOrtho, isOrtho);
				float3 vpos = float3((uv * 2 - 1 - p13_31) / p11_22 * lerp(vz, 1, isOrtho), -vz);
				float3 wpos = mul(_InverseView, float4(vpos, 1)).xyz;
				return wpos;
			}

			half3 GetSceneNormalFromDepth(float2 uv, float3 sceneWPOS) {
				float2 texelSize = 1.0.xx / _ScreenParams.xy;

				float2 uv2 = uv + float2(texelSize.x, 0);
				float depth2 = SampleSceneDepthX(uv2);
				float3 sceneWPOS2 = GetWorldPosFromRawDepth(uv2, depth2);

				float2 uv3 = uv + float2(0, texelSize.y);
				float depth3 = SampleSceneDepthX(uv3);
				float3 sceneWPOS3 = GetWorldPosFromRawDepth(uv3, depth3);

				return abs(normalize(cross(sceneWPOS2 - sceneWPOS, sceneWPOS3 - sceneWPOS)));
            }

			half3 GetSceneNormalUsingDerivatives(float3 sceneWPOS) {
				return normalize(cross(ddx(sceneWPOS), ddy(sceneWPOS)));
            }

			half3 SampleCaustics(float3 wpos, float3 triblend) {
				half3 c0 = tex2D(_WaterCaustics, wpos.xz).rgb;
				half3 c1 = tex2D(_WaterCaustics, wpos.xy).rgb;
				half3 c2 = tex2D(_WaterCaustics, wpos.yz).rgb;
				half3 c = c2 * triblend.x + c0 * triblend.y + c1 * triblend.z;
				return c;
            }

			half3 GetCaustics(float3 wpos, float3 sceneNormal) {
				half3 triblend = saturate(pow(sceneNormal, 4));
				triblend /= max(dot(triblend, half3(1,1,1)), 0.0001);
				half3 caustics1 = SampleCaustics(wpos + _Time.x, triblend);
				half3 caustics2 = SampleCaustics(wpos * -1.5 + _Time.x, triblend);
				half3 c = min(caustics1, caustics2);
				c *= c;
				return c;
            }

			#endif

			half4 frag (v2f i) : SV_Target {

				float3 wpos = i.wpos;
				float x0 = floor(wpos.x);
				float x1 = x0 + 1.0;
				float s0 = sin(x0 * 3.1415927 * 1.5 + _Time.w) * 0.025 - 0.028;
				float s1 = sin(x1 * 3.1415927 * 1.5 + _Time.w) * 0.025 - 0.028;
				float t  = wpos.x - x0;
				wpos.y  += _WaveAmplitude * lerp(s0, s1, t);

				float dy = _WaterLevel - wpos.y;
				clip(dy);

				half4 color = _Color;

				#if defined(UNDERWATER_HQ)
					float2 uv = i.grabPos.xy / i.grabPos.w;
					float depth = SampleSceneDepthX(uv);
					float3 sceneWPOS = GetWorldPosFromRawDepth(uv, depth);
					float3 sceneNormal = GetSceneNormalUsingDerivatives(sceneWPOS); //GetSceneNormalFromDepth(uv, sceneWPOS);
					float3 viewDir = normalize(sceneWPOS - _WorldSpaceCameraPos);
	
					float dist = distance(sceneWPOS, _WorldSpaceCameraPos);
					if (viewDir.y > 0) {
						float camDistToSurface = abs(_WaterCausticsLevel - _WorldSpaceCameraPos.y);
						dist = min(dist, camDistToSurface / viewDir.y);
					}
					dist = min(dist, 25);

					const int NUM_STEPS = 32;
					float rayStep = dist / NUM_STEPS;
					half4 sum = 0;
					float jitter = frac(dot(float2(2.4084507, 3.2535211), uv * _ScreenParams.xy)) - 0.5;
					float3 rayPos = _WorldSpaceCameraPos + viewDir * (jitter * 0.01);

					UNITY_UNROLL
					for(int k=0;k<NUM_STEPS ;k++) {
						float3 distort = float3(_Time.x, _SinTime.x * 0.25, _Time.x) * 1.5;
						float3 texPos =  (rayPos + distort) * 0.2;
						half opacity = tex3Dlod(_Noise3D, float4(texPos, 0)).r * _UnderWaterFogColor.a;
						half4 col = half4 (_UnderWaterFogColor.rgb, opacity);
						col.rgb *= col.a;
						sum += col * (rayStep * (1.0 - sum.a));
						if (sum.a > 0.99) break;
						rayPos += rayStep * viewDir;
                    }

					color = _Color * (1.0-sum.a) + sum;

					float verticalDepth = _WaterCausticsLevel - sceneWPOS.y;
					if (verticalDepth > 0) {
						float depthAtten = verticalDepth * verticalDepth;
						half3 caustics = GetCaustics(sceneWPOS, sceneNormal) * smoothstep(0, 0.5, verticalDepth) * 2.0 / (1.0 + depthAtten);
						color.rgb += caustics * (1.0- sum.a);
					}


				#endif

				// Sun light
				color.rgb *= i.light.x;

				return color;
			}
			