// Soft bones for a figure's picture (Bend.cs): its sprites have a grid mesh, and each vertex moves with three bones
// stacked up the figure's middle (legs, body, head), blended softly across the hips and the shoulders. On top, the
// loose parts (hair, headbands, ears; the top and the outer edges) trail by an offset and ripple with a wave.
// "Figure space": the figure's art root in world units, feet at (0, 0), facing +x. All set per renderer through a
// MaterialPropertyBlock, so the bend survives material swaps (hit flashes, ghost silhouettes). `_BendJ.w` <= 0: no bend.

float4 _BendXf;   // object -> figure space: scale xy, offset zw
float4 _BendJ;    // hip y, shoulder y, blend half-width, figure height
float4 _BendA0;   // legs: 2x2 matrix (m00, m01, m10, m11)
float4 _BendA1;   // body
float4 _BendA2;   // head
float4 _BendT01;  // legs translation xy, body translation zw
float4 _BendT2;   // head translation xy, loose parts' offset zw
float4 _BendWave; // loose parts' wave: amplitude, phase, radians per unit of height, half the figure's width

float2 BendPoint(float2 p)
{
    float b = _BendJ.z;
    float w0 = 1 - smoothstep(_BendJ.x - b, _BendJ.x + b, p.y);
    float w2 = smoothstep(_BendJ.y - b, _BendJ.y + b, p.y);
    float w1 = saturate(1 - w0 - w2);
    float2 q0 = float2(dot(_BendA0.xy, p), dot(_BendA0.zw, p)) + _BendT01.xy;
    float2 q1 = float2(dot(_BendA1.xy, p), dot(_BendA1.zw, p)) + _BendT01.zw;
    float2 q2 = float2(dot(_BendA2.xy, p), dot(_BendA2.zw, p)) + _BendT2.xy;
    float2 q = w0 * q0 + w1 * q1 + w2 * q2;
    // The face (the middle) barely trails; hair and headbands at the edges do.
    float loose = smoothstep(_BendJ.y - 2 * b, _BendJ.w, p.y) * (0.15 + 0.85 * saturate(abs(p.x) / _BendWave.w));
    q += (_BendT2.zw + float2(_BendWave.x * sin(_BendWave.y + p.y * _BendWave.z), 0)) * loose;
    return q;
}

float4 BendVertex(float4 v)
{
    if (_BendJ.w <= 0) return v;
    float2 p = BendPoint(v.xy * _BendXf.xy + _BendXf.zw);
    v.xy = (p - _BendXf.zw) / _BendXf.xy;
    return v;
}
