using AppKit;
using Foundation;

namespace FileConverter.Mac
{
    [Register("AppDelegate")]
    public class AppDelegate : NSApplicationDelegate
    {
        private MainWindowController mainWindowController;

        public override void DidFinishLaunching(NSNotification notification)
        {
            // Insert code here to initialize your application
            mainWindowController = new MainWindowController();
            mainWindowController.Window.MakeKeyAndOrderFront(this);
        }

        public override void WillTerminate(NSNotification notification)
        {
            // Insert code here to tear down your application
        }

        // Optional: If you want your application to re-open its window when the dock icon is clicked
        // and no other windows are open.
        public override bool ApplicationShouldHandleReopen(NSApplication sender, bool hasVisibleWindows)
        {
            if (!hasVisibleWindows)
            {
                mainWindowController?.Window.MakeKeyAndOrderFront(this);
                return true;
            }
            return false;
        }
    }
}
