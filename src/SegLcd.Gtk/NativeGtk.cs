using System.Runtime.InteropServices;

namespace SegLcd.Gui;

/// <summary>
/// <c>gtk_init_check</c>: initialises GTK without ending the process when no display can be opened (Gir.Core's
/// <c>Module.Initialize()</c> calls <c>gtk_init()</c>, which exits). Same approach as StreamCC.
/// </summary>
internal static partial class NativeGtk
{
    [LibraryImport("libgtk-4.so.1", EntryPoint = "gtk_init_check")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GtkInitCheck();

    public static bool InitCheck() => GtkInitCheck();
}
