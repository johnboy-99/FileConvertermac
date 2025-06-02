using AppKit;
using Foundation;
using CoreGraphics;
using System;

namespace FileConverter.Mac
{
    public partial class MainWindow : NSWindow
    {
        // Constructor called when creating the window programmatically
        public MainWindow(CGRect contentRect, NSWindowStyle style, NSBackingStore backingStore, bool deferCreation)
            : base(contentRect, style, backingStore, deferCreation)
        {
            // Initialization code here if needed when creating programmatically
            // e.g., setting Title, MinSize, MaxSize, etc.
            // Title = "File Converter";
        }

        // Constructor called when the window is loaded from a .xib file
        // The IntPtr handle is passed by the Objective-C runtime.
        // The [Export("initWithCoder:")] attribute is often used if you override initWithCoder directly,
        // but for simple cases, this constructor signature is recognized by Xamarin.Mac.
        public MainWindow(IntPtr handle) : base(handle)
        {
            // Initialization code here if needed after loading from XIB
        }

        // You might also have a constructor that takes no arguments if you
        // are defining the window entirely in C# without a XIB for its basic structure.
        // public MainWindow() : base()
        // {
        //     // Define frame, style, etc.
        // }

        public override void AwakeFromNib()
        {
            base.AwakeFromNib();
            // Called after the window and all its views have been loaded from the .xib file.
            // Good place to do further setup of UI elements.
        }

        // Example: Override CanBecomeKeyWindow or CanBecomeMainWindow if needed
        // public override bool CanBecomeKeyWindow => true;
        // public override bool CanBecomeMainWindow => true;
    }
}
