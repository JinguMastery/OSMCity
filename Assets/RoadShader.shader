Shader "Unlit/RoadShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog
            #pragma target 4.5

            #include "UnityCG.cginc"

            StructuredBuffer<float3> _PathPoints;
            StructuredBuffer<float> _CumulativeDistances;
            int _PointCount;
            float _RoadWidth;
            float _RoadLength;

            struct SegmentCoordinate
            {
                float x;
                float y;
            };

            float3 ComputeNormal(float3 direction)
            {
                // Perpendicular in XZ plane (Y stays 0 for flat roads)
                return normalize(float3(-direction.z, 0, direction.x));
            }

            SegmentCoordinate WorldToSegmentCoordinate(float3 worldPos, StructuredBuffer<float3> pathPoints, StructuredBuffer<float> cumDist, int pointCount, float width)
            {
                // Find closest point on entire path (not just points, but segments)
                int closestSegment = 0;
                float closestDist = 1e10;
                float closestT = 0.0;  // ← Position along segment (0 to 1)
    
                for (int i = 0; i < pointCount - 1; i++)
                {
                    float3 a = pathPoints[i];
                    float3 b = pathPoints[i + 1];
                    float3 ab = b - a;
                    float3 aw = worldPos - a;
        
                    float t = dot(aw, ab) / dot(ab, ab);
                    t = clamp(t, 0.0, 1.0);  // ← Clamp to segment bounds
        
                    float3 closest = a + t * ab;
                    float d = distance(worldPos, closest);
        
                    if (d < closestDist)
                    {
                        closestDist = d;
                        closestSegment = i;
                        closestT = t;
                    }
                }

                // Now use closestSegment directly (no ambiguity)
                float3 a = pathPoints[closestSegment];
                float3 b = pathPoints[closestSegment + 1];
                float3 segDir = normalize(b - a);
                float3 segNormal = ComputeNormal(segDir);
                float segLength = distance(a, b);

                float3 localVec = worldPos - a;
    
                // Cumulative distance + position within segment
                float x = cumDist[closestSegment] + dot(localVec, segDir);
                float y = dot(localVec, segNormal) + width * 0.5;

                SegmentCoordinate result;
                result.x = x;
                result.y = y;
                return result;
            }

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                // Convert vertex to world position
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
    
                // Get segment coordinates
                SegmentCoordinate segCoord = WorldToSegmentCoordinate(
                    worldPos, 
                    _PathPoints,
                    _CumulativeDistances,
                    _PointCount, 
                    _RoadWidth
                );
    
                float L = _RoadWidth * _MainTex_ST.y / _MainTex_ST.x;
                // Compute new UVs from segment coordinates
                float2 procUV = float2(
                    segCoord.y / _RoadWidth,    // V across width
                    segCoord.x / L              // U along entire path
                );

                // Apply tiling and offset from material inspector
                o.uv = TRANSFORM_TEX(procUV, _MainTex);

                // Standard clip position (for screen rendering)
                o.vertex = UnityObjectToClipPos(v.vertex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Plain tex2D lets the GPU auto-pick a mip level from screen-space UV derivatives, but our UV
                // is reconstructed per-vertex via a branching "closest path segment" search
                fixed4 col = tex2Dlod(_MainTex, float4(i.uv, 0, 0));
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
