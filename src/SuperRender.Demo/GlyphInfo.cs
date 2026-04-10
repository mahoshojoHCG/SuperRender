namespace SuperRender.Demo;

public struct GlyphInfo
{
    public float U0, V0, U1, V1;  // texture coordinates in [0,1]
    public float Width, Height;    // glyph size at base font size
    public float AdvanceX;         // horizontal advance
    public float OffsetX, OffsetY; // bearing offset
}
