struct VSInput
{
    uint vertexId : SV_VertexId;
};

struct Vertex
{
    float2 position : Position;
    float2 texCoord : TexCoord;
};

struct VSOutput
{
    float4 position : SV_Position;
    float2 texCoord : TexCoord;
};

// VertexShader

StructuredBuffer<Vertex> vertexBuffer : register(t0);

VSOutput VSMain(VSInput input)
{
    VSOutput output;

    Vertex vertex = vertexBuffer.Load(input.vertexId);

    output.position = float4(vertex.position, 0.0f, 1.0f);
    output.texCoord = vertex.texCoord;
    return output;
}

// PixelShader

Texture2D texSource     : register(t1);
SamplerState texSampler : register(s0);

float4 PSMain(VSOutput input) : SV_Target
{
    return texSource.Sample(texSampler, input.texCoord);
}
