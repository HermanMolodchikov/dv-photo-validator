namespace DvPhotoValidator.Core.Imaging;

/// <summary>sRGB → CIE L*a*b* (D65) and luma helpers.</summary>
public static class ColorMath
{
    private static readonly double[] Linear = Enumerable.Range(0, 256).Select(i =>
    {
        var c = i / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }).ToArray();

    public static (double L, double A, double B) ToLab(byte r, byte g, byte b)
    {
        double rl = Linear[r], gl = Linear[g], bl = Linear[b];
        var x = (0.4124 * rl + 0.3576 * gl + 0.1805 * bl) / 0.95047;
        var y = 0.2126 * rl + 0.7152 * gl + 0.0722 * bl;
        var z = (0.0193 * rl + 0.1192 * gl + 0.9505 * bl) / 1.08883;
        double fx = F(x), fy = F(y), fz = F(z);
        return (116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz));
    }

    /// <summary>Rec. 601 luma, 0..255.</summary>
    public static double Luma(byte r, byte g, byte b) => 0.299 * r + 0.587 * g + 0.114 * b;

    private static double F(double t) => t > 0.008856 ? Math.Cbrt(t) : 7.787 * t + 16.0 / 116;
}
