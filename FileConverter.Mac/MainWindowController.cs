using AppKit;
using Foundation;
using System;

namespace FileConverter.Mac
{
    public partial class MainWindowController : NSWindowController
    {
        // Constructor loading from a XIB file named "MainWindow.xib"
        public MainWindowController() : base("MainWindow")
        {
            // Initialization code if needed
        }

        // Alternative constructor if you need to create the window programmatically
        // or pass specific parameters.
        // public MainWindowController(NSWindow window) : base(window)
        // {
        // }

        public new MainWindow Window
        {
            get { return (MainWindow)base.Window; }
        }

        public override void WindowDidLoad()
        {
            base.WindowDidLoad();

            // Implement this method to handle your window's loaded C# logic.
            // This is called automatically when the window is loaded from the .xib file.
            // For example, you can set the window's title or attach event handlers.
            // Window.Title = "File Converter for Mac";
        }
    }
}
