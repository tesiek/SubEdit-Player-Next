using System.Runtime.InteropServices;
namespace SubEdit.Player.WinForms;

internal static class SystemFontCatalog
{
    private const byte DefaultCharSet = 1;
    private static readonly Lazy<IReadOnlyList<string>> FamilyNames = new(EnumerateFamilyNames);

    public static IReadOnlyList<string> GetFamilyNames() => FamilyNames.Value;

    private static IReadOnlyList<string> EnumerateFamilyNames()
    {
        var names = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        var logFont = new LogFont
        {
            CharacterSet = DefaultCharSet,
            FaceName = string.Empty
        };
        using var graphics = Graphics.FromHwnd(IntPtr.Zero);
        var deviceContext = graphics.GetHdc();
        try
        {
            EnumFontFamiliesEx(
                deviceContext,
                ref logFont,
                (fontAddress, textMetricAddress, fontType, parameter) =>
                {
                    var font = Marshal.PtrToStructure<LogFont>(fontAddress);
                    if (!string.IsNullOrWhiteSpace(font.FaceName) && !font.FaceName.StartsWith('@'))
                    {
                        names.Add(font.FaceName);
                    }

                    return 1;
                },
                IntPtr.Zero,
                0);
        }
        finally
        {
            graphics.ReleaseHdc(deviceContext);
        }

        return names
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern int EnumFontFamiliesEx(
        IntPtr deviceContext,
        ref LogFont logFont,
        EnumFontFamiliesExProc callback,
        IntPtr parameter,
        uint flags);

    private delegate int EnumFontFamiliesExProc(
        IntPtr fontAddress,
        IntPtr textMetricAddress,
        uint fontType,
        IntPtr parameter);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct LogFont
    {
        public int Height;
        public int Width;
        public int Escapement;
        public int Orientation;
        public int Weight;
        public byte Italic;
        public byte Underline;
        public byte StrikeOut;
        public byte CharacterSet;
        public byte OutputPrecision;
        public byte ClipPrecision;
        public byte Quality;
        public byte PitchAndFamily;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string FaceName;
    }
}
