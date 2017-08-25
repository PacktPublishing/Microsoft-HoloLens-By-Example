// Per-pixel color data passed through the pixel shader.
struct PixelShaderInput
{
	min16float4 pos      : SV_POSITION;
	min16float2 texCoord : TEXCOORD0;
};

Texture2D<float4>  rgbChannel     : t0;
SamplerState       defaultSampler : s0;

min16float4 main(PixelShaderInput input) : SV_TARGET
{
	return min16float4(rgbChannel.Sample(defaultSampler, input.texCoord));
	//return min16float4(1.0, 0.0, 0.0, 1.0);
}
