using AppKit;

namespace FileConverter.Mac
{
    static class MainClass
    {
        static void Main(string[] args)
        {
            NSApplication.Init();
            // NSApplication.SharedApplication is implicitly created by Init and used by Main.
            // No explicit Run() or other method is typically needed here before Main(args).
            NSApplication.Main(args);
        }
    }
}
