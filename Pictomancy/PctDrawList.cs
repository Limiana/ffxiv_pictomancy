using Dalamud.Interface.Utility;
using ImGuiNET;
using Pictomancy.DXDraw;
using Pictomancy.ImGuiDraw;
using System.Drawing;
using System.Numerics;

namespace Pictomancy;
public class PctDrawList : IDisposable
{
    internal readonly ImDrawListPtr _drawList;
    internal readonly List<Vector3> _path;
    internal readonly DXRenderer _renderer;
    internal readonly ImGuiRenderer _fallbackRenderer;
    internal readonly AddonClipper _addonClipper;
    private PctTexture? _texture;
    internal bool Finalized => _texture != null;

    internal PctDrawList(ImDrawListPtr drawlist, DXRenderer renderer, AddonClipper addonClipper)
    {
        _drawList = drawlist;
        _path = new();
        _renderer = renderer;
        _addonClipper = addonClipper;
        _texture = null;
        _renderer.BeginFrame();
        _fallbackRenderer = new(drawlist);
    }

    public PctTexture DrawToTexture()
    {
        if (_texture == null)
        {
            if (PictoService.Hints.ClipNativeUI)
                _addonClipper.Clip(_renderer);
            var target = _renderer.EndFrame();
            _texture = target.Texture;
        }
        return _texture.Value;
    }

    public void Dispose()
    {
        if (PictoService.DrawList == this) PictoService.DrawList = null;
        if (!PictoService.Hints.AutoDraw) return;

        PctTexture texture = DrawToTexture();
        _drawList.AddImage(
            texture.TextureId,
            ImGuiHelpers.MainViewport.Pos,
            ImGuiHelpers.MainViewport.Pos + texture.Size);
    }

    /// <summary>
    /// Add text to a position in world space using default font and size.
    /// Currently uses Imgui to draw thus is not clipped.
    /// </summary>
    /// <param name="position">The position in world space</param>
    /// <param name="color">The color to draw</param>
    /// <param name="text">The text to draw</param>
    public void AddText(Vector3 position, uint color, string text, float scale)
    {
        throw new NotImplementedException("try again later");
        if (!PictoService.GameGui.WorldToScreen(position, out var position2D))
        {
            return;
        }
        var textPosition = position2D - (ImGui.CalcTextSize(text) / 2f);
        _drawList.AddText(textPosition, color, text);
    }

    /// <summary>
    /// Add dot to a position in world space.
    /// Currently uses Imgui to draw thus is not clipped.
    /// </summary>
    /// <param name="position">The position in world space</param>
    /// <param name="radiusPixels"></param>
    /// <param name="color">The color to draw</param>
    /// <param name="numSegments"></param>
    public void AddDot(Vector3 position, float radiusPixels, uint color, uint numSegments = 0)
    {
        throw new NotImplementedException("try again later");
        if (!PictoService.GameGui.WorldToScreen(position, out var position2D))
        {
            return;
        }
        _drawList.AddCircleFilled(position2D, radiusPixels, color, (int)numSegments);
    }

    public void PathLineTo(Vector3 point)
    {
        _path.Add(point);
    }

    public void PathArcTo(Vector3 point, float radius, float startAngle, float stopAngle, uint numSegments = 0)
    {
        float totalAngle = stopAngle - startAngle;
        if (numSegments == 0) numSegments = (uint)(MathF.Abs(totalAngle) * 180);

        float angleStep = totalAngle / numSegments;

        for (int step = 0; step <= numSegments; step++)
        {
            float angle = MathF.PI / 2 + startAngle + step * angleStep;
            Vector3 offset = new(MathF.Cos(angle), 0, MathF.Sin(angle));
            _path.Add(point + radius * offset);
        }
    }

    public void PathStroke(uint color, PctStrokeFlags flags, float thickness = 2f)
    {
        if (_renderer.StrokeDegraded)
        {
            _fallbackRenderer.DrawStroke(_path, thickness, color, (flags & PctStrokeFlags.Closed) > 0);
        }
        else
        {
            _renderer.DrawStroke(_path, thickness, color, (flags & PctStrokeFlags.Closed) > 0);
        }
        _path.Clear();
    }

    public void AddTriangleFilled(Vector3 a, Vector3 b, Vector3 c, uint color)
    {
        AddTriangleFilled(a, b, c, color);
    }

    public void AddTriangleFilled(Vector3 a, Vector3 b, Vector3 c, uint colorA, uint colorB, uint colorC)
    {
        _renderer.DrawTriangle(a, b, c, colorA, colorB, colorC);
    }

    public void AddLine(Vector3 start, Vector3 stop, float halfWidth, uint color, float thickness = 2)
    {
        Vector3 direction = stop - start;
        Vector3 perpendicular = halfWidth * Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
        AddQuad(start - perpendicular, stop - perpendicular, stop + perpendicular, start + perpendicular, color, thickness);
    }

    public void AddLineFilled(Vector3 start, Vector3 stop, float halfWidth, uint color, uint? outerColor = null)
    {
        Vector3 direction = stop - start;
        Vector3 perpendicular = halfWidth * Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
        AddQuadFilled(start - perpendicular, stop - perpendicular, stop + perpendicular, start + perpendicular, color, outerColor ?? color, outerColor ?? color, color);
    }

    public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, uint color, float thickness = 2)
    {
        PathLineTo(a);
        PathLineTo(b);
        PathLineTo(c);
        PathLineTo(d);
        PathStroke(color, PctStrokeFlags.Closed, thickness);
    }

    public void AddQuadFilled(Vector3 a, Vector3 b, Vector3 c, Vector3 d, uint color)
    {
        AddQuadFilled(a, b, c, d, color, color, color, color);
    }

    public void AddQuadFilled(Vector3 a, Vector3 b, Vector3 c, Vector3 d, uint colorA, uint colorB, uint colorC, uint colorD)
    {
        AddTriangleFilled(a, b, c, colorA, colorB, colorC);
        AddTriangleFilled(a, c, d, colorA, colorC, colorD);
    }

    public void AddCircle(Vector3 origin, float radius, uint color, uint numSegments = 0, float thickness = 2)
    {
        PathArcTo(origin, radius, 0, 2 * MathF.PI);
        PathStroke(color, PctStrokeFlags.Closed, thickness);
    }

    public void AddCircleFilled(Vector3 origin, float radius, uint color, uint? outerColor = null, uint numSegments = 0)
    {
        AddFanFilled(origin, 0, radius, 0, 2 * MathF.PI, color, outerColor, numSegments);
    }

    public void AddArc(Vector3 origin, float radius, float minAngle, float maxAngle, uint color, uint numSegments = 0, float thickness = 2)
    {
        PathArcTo(origin, radius, minAngle, maxAngle);
        PathStroke(color, PctStrokeFlags.None, thickness);
    }

    public void AddArcFilled(Vector3 origin, float radius, float minAngle, float maxAngle, uint color, uint? outerColor = null, uint numSegments = 0)
    {
        AddFanFilled(origin, 0, radius, minAngle, maxAngle, color, outerColor, numSegments);
    }

    public void AddConeFilled(Vector3 origin, float radius, float rotation, float angle, uint color, uint? outerColor = null, uint numSegments = 0)
    {
        var halfAngle = angle / 2;
        AddFanFilled(origin, 0, radius, rotation - halfAngle, rotation + halfAngle, color, outerColor, numSegments);
    }

    public void AddFan(Vector3 origin, float innerRadius, float outerRadius, float minAngle, float maxAngle, uint color, uint numSegments = 0, float thickness = 2)
    {
        bool isCircle = maxAngle - minAngle >= 2 * MathF.PI - 0.000001;
        PathArcTo(origin, outerRadius, minAngle, maxAngle);
        if (innerRadius > 0)
        {
            if (isCircle)
            {
                PathStroke(color, PctStrokeFlags.Closed, thickness);
            }
            PathArcTo(origin, innerRadius, maxAngle, minAngle);
        }
        else if (!isCircle)
        {
            PathLineTo(origin);
        }
        PathStroke(color, PctStrokeFlags.Closed, thickness);
    }

    public void AddFanFilled(Vector3 origin, float innerRadius, float outerRadius, float minAngle, float maxAngle, uint color, uint? outerColor = null, uint numSegments = 0)
    {
        _renderer.DrawFan(origin, innerRadius, outerRadius, minAngle, maxAngle, color, outerColor ?? color);
    }

    public void AddClipZone(Rectangle rectangle, float alpha = 0)
    {
        _renderer.AddClipRect(new(rectangle.Left, rectangle.Top), new(rectangle.Width, rectangle.Height), alpha);
    }
    /*
    public void AddClipZoneTri(Vector2 a, Vector2 b, Vector2 c, float alpha = 0)
    {
        _renderer.AddClipTri(a, b, c, alpha);
    }
    */
}
