using SuperRender.Renderer.Rendering.Style;
using Xunit;

namespace SuperRender.Renderer.Tests.Style;

public class TransformTests
{
    [Fact]
    public void ParseTransform_None_ReturnsNull()
    {
        Assert.Null(StyleResolver.ParseTransformFunctions("none"));
    }

    [Fact]
    public void ParseTransform_Empty_ReturnsNull()
    {
        Assert.Null(StyleResolver.ParseTransformFunctions(""));
    }

    [Fact]
    public void ParseTransform_TranslateX()
    {
        var result = StyleResolver.ParseTransformFunctions("translateX(10px)");
        Assert.NotNull(result);
        Assert.Single(result);
        var func = Assert.IsType<TranslateXFunction>(result[0]);
        Assert.Equal(10f, func.X);
    }

    [Fact]
    public void ParseTransform_TranslateY()
    {
        var result = StyleResolver.ParseTransformFunctions("translateY(20px)");
        Assert.NotNull(result);
        Assert.IsType<TranslateYFunction>(result[0]);
    }

    [Fact]
    public void ParseTransform_Translate_TwoArgs()
    {
        var result = StyleResolver.ParseTransformFunctions("translate(10px, 20px)");
        Assert.NotNull(result);
        var func = Assert.IsType<TranslateFunction>(result[0]);
        Assert.Equal(10f, func.X);
        Assert.Equal(20f, func.Y);
    }

    [Fact]
    public void ParseTransform_Scale()
    {
        var result = StyleResolver.ParseTransformFunctions("scale(2)");
        Assert.NotNull(result);
        var func = Assert.IsType<ScaleFunction>(result[0]);
        Assert.Equal(2f, func.Sx);
    }

    [Fact]
    public void ParseTransform_Rotate_Degrees()
    {
        var result = StyleResolver.ParseTransformFunctions("rotate(90deg)");
        Assert.NotNull(result);
        var func = Assert.IsType<RotateFunction>(result[0]);
        Assert.Equal(MathF.PI / 2, func.Angle, 0.001f);
    }

    [Fact]
    public void ParseTransform_SkewX()
    {
        var result = StyleResolver.ParseTransformFunctions("skewX(30deg)");
        Assert.NotNull(result);
        Assert.IsType<SkewXFunction>(result[0]);
    }

    [Fact]
    public void ParseTransform_Matrix()
    {
        var result = StyleResolver.ParseTransformFunctions("matrix(1, 0, 0, 1, 10, 20)");
        Assert.NotNull(result);
        Assert.IsType<MatrixFunction>(result[0]);
    }

    [Fact]
    public void ParseTransform_Multiple()
    {
        var result = StyleResolver.ParseTransformFunctions("translate(10px, 20px) rotate(45deg) scale(1.5)");
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void ParseTransform_Perspective()
    {
        var result = StyleResolver.ParseTransformFunctions("perspective(500px)");
        Assert.NotNull(result);
        Assert.IsType<PerspectiveFunction>(result[0]);
    }

    [Fact]
    public void StyleResolver_Transform_Applied()
    {
        var style = StyleTestHelper.ResolveFirst("div { transform: translate(10px, 20px); }", "<div>test</div>");
        Assert.NotNull(style.Transform);
        Assert.Single(style.Transform);
    }

    [Fact]
    public void StyleResolver_TransformStyle_Preserve3D()
    {
        var style = StyleTestHelper.ResolveFirst("div { transform-style: preserve-3d; }", "<div>test</div>");
        Assert.Equal("preserve-3d", style.TransformStyle);
    }

    [Fact]
    public void StyleResolver_BackfaceVisibility_Hidden()
    {
        var style = StyleTestHelper.ResolveFirst("div { backface-visibility: hidden; }", "<div>test</div>");
        Assert.Equal("hidden", style.BackfaceVisibility);
    }

    [Fact]
    public void Matrix_Identity_DiagonalOnes()
    {
        var m = TransformMatrix.Identity();
        Assert.Equal(1f, m[0, 0]);
        Assert.Equal(1f, m[1, 1]);
    }

    [Fact]
    public void Matrix_Multiply_Identity()
    {
        var a = TransformMatrix.CreateTranslation(10, 20);
        var result = a.Multiply(TransformMatrix.Identity());
        Assert.Equal(10f, result[0, 3]);
    }

    [Fact]
    public void Matrix_Scale()
    {
        var m = TransformMatrix.CreateScale(2, 3);
        Assert.Equal(2f, m[0, 0]);
        Assert.Equal(3f, m[1, 1]);
    }

    [Fact]
    public void Matrix_RotateZ_90()
    {
        var m = TransformMatrix.CreateRotateZ(MathF.PI / 2);
        Assert.Equal(0f, m[0, 0], 0.001f);
        Assert.Equal(-1f, m[0, 1], 0.001f);
    }

    [Fact]
    public void Matrix_Perspective()
    {
        var m = TransformMatrix.CreatePerspective(500);
        Assert.Equal(-1f / 500f, m[3, 2], 0.0001f);
    }

    [Fact]
    public void AngleParser_Grad()
    {
        Assert.Equal(MathF.PI, AngleParser.ParseToRadians("200grad"), 0.001f);
    }

    [Fact]
    public void AngleParser_Turn()
    {
        Assert.Equal(MathF.PI / 2, AngleParser.ParseToRadians("0.25turn"), 0.001f);
    }
}
