using SuperRender.Document;
using SuperRender.Document.Css;
using SuperRender.Renderer.Rendering.Layout;
using SuperRender.Renderer.Rendering.Painting;
using SuperRender.Renderer.Rendering.Style;
using Xunit;

namespace SuperRender.Tests.Painting;

public class BackgroundPaintTests
{
    private static LayoutBox CreateBox(ComputedStyle style, float width = 200, float height = 100)
    {
        return new LayoutBox
        {
            Style = style,
            BoxType = LayoutBoxType.Block,
            Dimensions = new BoxDimensions { X = 10, Y = 10, Width = width, Height = height },
        };
    }

    [Fact]
    public void LinearGradient_EmitsDrawLinearGradientCommand()
    {
        var style = new ComputedStyle
        {
            BackgroundImage = new LinearGradient
            {
                AngleDeg = 90f,
                ColorStops = new[]
                {
                    new ColorStop(Color.FromRgb(255, 0, 0), 0f),
                    new ColorStop(Color.FromRgb(0, 0, 255), 1f),
                },
            },
        };

        var box = CreateBox(style);
        var list = Painter.Paint(box);

        Assert.Contains(list.Commands, c => c is DrawLinearGradientCommand);
        var cmd = list.Commands.OfType<DrawLinearGradientCommand>().First();
        Assert.Equal(90f, cmd.AngleDeg);
        Assert.Equal(2, cmd.ColorStops.Count);
    }

    [Fact]
    public void RadialGradient_EmitsDrawRadialGradientCommand()
    {
        var style = new ComputedStyle
        {
            BackgroundImage = new RadialGradient
            {
                Shape = RadialGradientShape.Circle,
                ColorStops = new[]
                {
                    new ColorStop(Color.FromRgb(255, 0, 0), 0f),
                    new ColorStop(Color.FromRgb(0, 0, 255), 1f),
                },
            },
        };

        var box = CreateBox(style);
        var list = Painter.Paint(box);

        Assert.Contains(list.Commands, c => c is DrawRadialGradientCommand);
    }

    [Fact]
    public void NoGradient_NoGradientCommand()
    {
        var style = new ComputedStyle
        {
            BackgroundColor = Color.FromRgb(255, 255, 255),
        };

        var box = CreateBox(style);
        var list = Painter.Paint(box);

        Assert.DoesNotContain(list.Commands, c => c is DrawLinearGradientCommand);
        Assert.DoesNotContain(list.Commands, c => c is DrawRadialGradientCommand);
    }

    [Fact]
    public void BoxShadow_EmitsDrawBoxShadowCommand()
    {
        var style = new ComputedStyle
        {
            BoxShadows = new List<BoxShadowDescriptor>
            {
                new()
                {
                    OffsetX = 5,
                    OffsetY = 5,
                    BlurRadius = 10,
                    Color = Color.FromRgb(0, 0, 0),
                },
            },
        };

        var box = CreateBox(style);
        var list = Painter.Paint(box);

        Assert.Contains(list.Commands, c => c is DrawBoxShadowCommand);
        var cmd = list.Commands.OfType<DrawBoxShadowCommand>().First();
        Assert.Equal(5f, cmd.OffsetX);
        Assert.Equal(5f, cmd.OffsetY);
        Assert.Equal(10f, cmd.BlurRadius);
    }

    [Fact]
    public void BoxShadow_InsetShadow()
    {
        var style = new ComputedStyle
        {
            BoxShadows = new List<BoxShadowDescriptor>
            {
                new()
                {
                    OffsetX = 0,
                    OffsetY = 0,
                    BlurRadius = 5,
                    Inset = true,
                    Color = Color.FromRgb(0, 0, 0),
                },
            },
        };

        var box = CreateBox(style);
        var list = Painter.Paint(box);

        var cmd = list.Commands.OfType<DrawBoxShadowCommand>().First();
        Assert.True(cmd.Inset);
    }

    [Fact]
    public void BoxShadow_PaintedBeforeBackground()
    {
        var style = new ComputedStyle
        {
            BackgroundColor = Color.FromRgb(255, 255, 255),
            BoxShadows = new List<BoxShadowDescriptor>
            {
                new()
                {
                    OffsetX = 5,
                    OffsetY = 5,
                    BlurRadius = 10,
                    Color = Color.FromRgb(0, 0, 0),
                },
            },
        };

        var box = CreateBox(style);
        var list = Painter.Paint(box);

        int shadowIdx = -1;
        int bgIdx = -1;
        for (int i = 0; i < list.Commands.Count; i++)
        {
            if (list.Commands[i] is DrawBoxShadowCommand && shadowIdx < 0) shadowIdx = i;
            if (list.Commands[i] is FillRectCommand && bgIdx < 0) bgIdx = i;
        }

        Assert.True(shadowIdx >= 0, "Shadow command should exist");
        Assert.True(bgIdx >= 0, "Background command should exist");
        Assert.True(shadowIdx < bgIdx, "Shadow should be painted before background");
    }

    [Fact]
    public void MultipleShadows_AllEmitted()
    {
        var style = new ComputedStyle
        {
            BoxShadows = new List<BoxShadowDescriptor>
            {
                new() { OffsetX = 1, OffsetY = 1, Color = Color.FromRgb(255, 0, 0) },
                new() { OffsetX = 2, OffsetY = 2, Color = Color.FromRgb(0, 0, 255) },
            },
        };

        var box = CreateBox(style);
        var list = Painter.Paint(box);

        var shadowCmds = list.Commands.OfType<DrawBoxShadowCommand>().ToList();
        Assert.Equal(2, shadowCmds.Count);
    }

    [Fact]
    public void Outline_EmitsDrawOutlineCommand()
    {
        var style = new ComputedStyle
        {
            OutlineWidth = 2f,
            OutlineStyle = "solid",
            OutlineColor = Color.FromRgb(0, 0, 255),
        };

        var box = CreateBox(style);
        var list = Painter.Paint(box);

        Assert.Contains(list.Commands, c => c is DrawOutlineCommand);
        var cmd = list.Commands.OfType<DrawOutlineCommand>().First();
        Assert.Equal(2f, cmd.Width);
    }

    [Fact]
    public void Outline_NoneStyle_NoCommand()
    {
        var style = new ComputedStyle
        {
            OutlineWidth = 2f,
            OutlineStyle = "none",
            OutlineColor = Color.FromRgb(0, 0, 255),
        };

        var box = CreateBox(style);
        var list = Painter.Paint(box);

        Assert.DoesNotContain(list.Commands, c => c is DrawOutlineCommand);
    }

    [Fact]
    public void Outline_ZeroWidth_NoCommand()
    {
        var style = new ComputedStyle
        {
            OutlineWidth = 0f,
            OutlineStyle = "solid",
            OutlineColor = Color.FromRgb(0, 0, 255),
        };

        var box = CreateBox(style);
        var list = Painter.Paint(box);

        Assert.DoesNotContain(list.Commands, c => c is DrawOutlineCommand);
    }

    [Fact]
    public void Outline_WithOffset()
    {
        var style = new ComputedStyle
        {
            OutlineWidth = 3f,
            OutlineStyle = "solid",
            OutlineColor = Color.FromRgb(0, 0, 0),
            OutlineOffset = 5f,
        };

        var box = CreateBox(style);
        var list = Painter.Paint(box);

        var cmd = list.Commands.OfType<DrawOutlineCommand>().First();
        Assert.Equal(5f, cmd.Offset);
    }

    [Fact]
    public void GradientStops_ColorOpacity()
    {
        var style = new ComputedStyle
        {
            Opacity = 0.5f,
            BackgroundImage = new LinearGradient
            {
                ColorStops = new[]
                {
                    new ColorStop(Color.FromRgb(255, 0, 0), 0f),
                    new ColorStop(Color.FromRgb(0, 0, 255), 1f),
                },
            },
        };

        var box = CreateBox(style);
        var list = Painter.Paint(box);

        var cmd = list.Commands.OfType<DrawLinearGradientCommand>().First();
        Assert.True(cmd.ColorStops[0].Color.A < 1f);
    }

    [Fact]
    public void BackgroundColor_StillRendered_WithGradient()
    {
        var style = new ComputedStyle
        {
            BackgroundColor = Color.FromRgb(255, 255, 255),
            BackgroundImage = new LinearGradient
            {
                ColorStops = new[]
                {
                    new ColorStop(Color.FromRgb(255, 0, 0), 0f),
                    new ColorStop(Color.FromRgb(0, 0, 255), 1f),
                },
            },
        };

        var box = CreateBox(style);
        var list = Painter.Paint(box);

        Assert.Contains(list.Commands, c => c is FillRectCommand);
        Assert.Contains(list.Commands, c => c is DrawLinearGradientCommand);
    }

    [Fact]
    public void DisplayNone_NoShadowOrOutline()
    {
        var style = new ComputedStyle
        {
            Display = DisplayType.None,
            BoxShadows = new List<BoxShadowDescriptor>
            {
                new() { OffsetX = 5, OffsetY = 5, Color = Color.FromRgb(0, 0, 0) },
            },
            OutlineWidth = 2f,
            OutlineStyle = "solid",
            OutlineColor = Color.FromRgb(0, 0, 255),
        };

        var box = CreateBox(style);
        var list = Painter.Paint(box);

        Assert.DoesNotContain(list.Commands, c => c is DrawBoxShadowCommand);
        Assert.DoesNotContain(list.Commands, c => c is DrawOutlineCommand);
    }

    [Fact]
    public void VisibilityHidden_NoShadowOrGradient()
    {
        var style = new ComputedStyle
        {
            Visibility = VisibilityType.Hidden,
            BoxShadows = new List<BoxShadowDescriptor>
            {
                new() { OffsetX = 5, OffsetY = 5, Color = Color.FromRgb(0, 0, 0) },
            },
            BackgroundImage = new LinearGradient
            {
                ColorStops = new[]
                {
                    new ColorStop(Color.FromRgb(255, 0, 0), 0f),
                    new ColorStop(Color.FromRgb(0, 0, 255), 1f),
                },
            },
        };

        var box = CreateBox(style);
        var list = Painter.Paint(box);

        Assert.DoesNotContain(list.Commands, c => c is DrawBoxShadowCommand);
        Assert.DoesNotContain(list.Commands, c => c is DrawLinearGradientCommand);
    }

    [Fact]
    public void GradientWithBorderRadius_PropagatesRadii()
    {
        var style = new ComputedStyle
        {
            BorderTopLeftRadius = 10f,
            BorderTopRightRadius = 5f,
            BackgroundImage = new LinearGradient
            {
                ColorStops = new[]
                {
                    new ColorStop(Color.FromRgb(255, 0, 0), 0f),
                    new ColorStop(Color.FromRgb(0, 0, 255), 1f),
                },
            },
        };

        var box = CreateBox(style);
        var list = Painter.Paint(box);

        var cmd = list.Commands.OfType<DrawLinearGradientCommand>().First();
        Assert.Equal(10f, cmd.RadiusTL);
        Assert.Equal(5f, cmd.RadiusTR);
    }

    [Fact]
    public void BoxShadow_Spread()
    {
        var style = new ComputedStyle
        {
            BoxShadows = new List<BoxShadowDescriptor>
            {
                new()
                {
                    OffsetX = 0,
                    OffsetY = 0,
                    BlurRadius = 0,
                    SpreadRadius = 10,
                    Color = Color.FromRgb(0, 0, 0),
                },
            },
        };

        var box = CreateBox(style);
        var list = Painter.Paint(box);

        var cmd = list.Commands.OfType<DrawBoxShadowCommand>().First();
        Assert.Equal(10f, cmd.SpreadRadius);
    }

    [Fact]
    public void BoxShadow_TransparentColor_Skipped()
    {
        var style = new ComputedStyle
        {
            BoxShadows = new List<BoxShadowDescriptor>
            {
                new()
                {
                    OffsetX = 5,
                    OffsetY = 5,
                    Color = Color.Transparent,
                },
            },
        };

        var box = CreateBox(style);
        var list = Painter.Paint(box);

        Assert.DoesNotContain(list.Commands, c => c is DrawBoxShadowCommand);
    }
}
