using System.Text;

namespace ObentoTensho.Infrastructure.Media;

// FR-IMG-09: mã hóa BlurHash 4×3 thành phần (28 ký tự) từ ảnh RGB 8 bit thu nhỏ
internal static class BlurHashEncoder
{
    private const int ComponentsX = 4;
    private const int ComponentsY = 3;
    private const string Base83 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz#$%*+,-.:;=?@[]^_{|}~";

    public static string Encode(ReadOnlySpan<byte> rgb, int width, int height)
    {
        var factors = new (double R, double G, double B)[ComponentsX * ComponentsY];
        for (var j = 0; j < ComponentsY; j++)
        {
            for (var i = 0; i < ComponentsX; i++)
            {
                factors[(j * ComponentsX) + i] = Factor(rgb, width, height, i, j);
            }
        }

        var ac = factors.AsSpan(1);
        var builder = new StringBuilder(28);
        Append(builder, (ComponentsX - 1) + ((ComponentsY - 1) * 9), 1);

        var maxValue = 1.0;
        if (ac.Length > 0)
        {
            var actualMax = 0.0;
            foreach (var (r, g, b) in ac)
            {
                actualMax = Math.Max(actualMax, Math.Max(Math.Abs(r), Math.Max(Math.Abs(g), Math.Abs(b))));
            }

            var quantised = Math.Clamp((int)Math.Floor((actualMax * 166) - 0.5), 0, 82);
            maxValue = (quantised + 1) / 166.0;
            Append(builder, quantised, 1);
        }

        var dc = factors[0];
        Append(builder, (ToSrgb(dc.R) << 16) + (ToSrgb(dc.G) << 8) + ToSrgb(dc.B), 4);
        foreach (var (r, g, b) in ac)
        {
            Append(builder, (Quantise(r, maxValue) * 361) + (Quantise(g, maxValue) * 19) + Quantise(b, maxValue), 2);
        }

        return builder.ToString();
    }

    private static (double R, double G, double B) Factor(ReadOnlySpan<byte> rgb, int width, int height, int cx, int cy)
    {
        var (r, g, b) = (0.0, 0.0, 0.0);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var basis = Math.Cos(Math.PI * cx * x / width) * Math.Cos(Math.PI * cy * y / height);
                var offset = ((y * width) + x) * 3;
                r += basis * ToLinear(rgb[offset]);
                g += basis * ToLinear(rgb[offset + 1]);
                b += basis * ToLinear(rgb[offset + 2]);
            }
        }

        var scale = (cx == 0 && cy == 0 ? 1.0 : 2.0) / (width * height);
        return (r * scale, g * scale, b * scale);
    }

    private static int Quantise(double value, double maxValue)
    {
        var scaled = value / maxValue;
        return Math.Clamp((int)Math.Floor((Math.CopySign(Math.Sqrt(Math.Abs(scaled)), scaled) * 9) + 9.5), 0, 18);
    }

    private static double ToLinear(byte value)
    {
        var v = value / 255.0;
        return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }

    private static int ToSrgb(double value)
    {
        var v = Math.Clamp(value, 0, 1);
        return (int)((v <= 0.0031308 ? v * 12.92 * 255 : ((1.055 * Math.Pow(v, 1 / 2.4)) - 0.055) * 255) + 0.5);
    }

    private static void Append(StringBuilder builder, int value, int length)
    {
        Span<char> chars = stackalloc char[length];
        for (var i = length - 1; i >= 0; i--)
        {
            chars[i] = Base83[value % 83];
            value /= 83;
        }

        builder.Append(chars);
    }
}
