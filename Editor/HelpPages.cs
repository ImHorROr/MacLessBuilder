namespace HorrorTools.MacLessBuilder
{
    // Screenshots are optional: drop "<ImagePrefix>-<step>.png" into the Editor/HelpImages folder
    // and the Help tab shows it under that step.
    internal static class HelpPages
    {
        public static readonly Page[] Pages =
        {
            new Page(
                "Connect your iPhone",
                "connect",
                "Plug the iPhone into the PC with a data cable and unlock it.",
                "Tap Trust on \"Trust This Computer?\" and enter the passcode.",
                "The Setup tab shows Apple device support in green once iTunes or Apple Devices is running. If the phone still is not found, try another cable and USB port: many cables only charge."),
            new Page(
                "Install on your iPhone",
                "install",
                "On the Setup tab, click Enter Apple ID, type the email and password and click Save & verify. A free Apple ID works; the password goes only to Apple.",
                "If Apple asks for a two-factor code, a yellow box appears at the top of the window. Type the code from your other Apple device and press Submit.",
                "Plug the iPhone in, unlock it, and pick it in the iPhone list on the Build tab (click Refresh if it is missing).",
                "Click Install on iPhone and wait for Installing 100%.",
                "Keep the same bundle identifier between builds: a free Apple ID can only create a few new App IDs per week."),
            new Page(
                "Turn on Developer Mode (iOS 16 and later)",
                "devmode",
                "Install the app once; the Developer Mode option only appears after a development app is on the device.",
                "Open Settings > Privacy & Security, scroll to the bottom, tap Developer Mode and turn it on.",
                "Tap Restart. After the restart, tap Turn On and enter the passcode."),
            new Page(
                "Trust the app the first time",
                "trust",
                "Opening the app shows \"Untrusted Developer\". Tap Cancel.",
                "Open Settings > General > VPN & Device Management.",
                "Under Developer App, tap your Apple ID email, then Trust, and Trust again.",
                "Open the app. This is needed once per Apple ID, not per build."),
            new Page(
                "The app stopped opening after a week",
                "expired",
                "Apps signed with a free Apple ID stop opening after 7 days.",
                "Plug the phone in and click Reinstall next to the build in the History list. No new cloud build is needed."),
        };

        public static readonly Row[] Troubleshooting =
        {
            new Row("\"Untrusted Developer\" alert", "First install from this Apple ID", "Help: Trust the app the first time"),
            new Row("\"Developer Mode Required\"", "Developer Mode is off", "Help: Turn on Developer Mode"),
            new Row("App icon greys out or says unavailable after days", "The 7-day free signature expired", "Reinstall from the History list"),
            new Row("iPhone not listed", "Charge-only cable, phone locked, or computer not trusted", "Help: Connect your iPhone; try another cable and port"),
            new Row("Certificate limit reached", "An Apple ID allows only a few development certificates, and other tools or PCs already made them", "Accept Revoke and install; apps those certificates signed elsewhere need reinstalling"),
            new Row("Sign-in fails or keeps asking for a code", "Wrong password, or the anisette server is down", "Forget and re-enter the Apple ID; set another anisette server under Build > Settings"),
            new Row("Apple device support not found", "iTunes or Apple Devices is not installed, or its service stopped", "Install it from the Setup tab, then restart the PC"),
            new Row("\"Maximum number of apps\" on install", "A free Apple ID allows about 3 sideloaded apps per device", "Delete an older sideloaded app from the phone"),
            new Row("App ID limit reached", "Too many new bundle IDs this week", "Keep the same bundle ID between builds; wait for the weekly reset"),
            new Row("Workflow file could not be created", "The GitHub token lacks the workflow scope", "Sign out on the Setup tab and sign in again"),
            new Row("Build stuck in Queued", "macOS runners are busy, or Actions are disabled on the repository", "Wait, or enable Actions in the repository settings"),
            new Row("Build failed at pod install or linking", "A plugin needs CocoaPods or a specific Xcode version", "Open the run log from the Build tab"),
            new Row("Build blocked with a billing error", "Free minutes are used up and no payment method is set", "Wait for the monthly reset or add a payment method"),
        };

        internal class Page
        {
            public string ImagePrefix { get; }

            public string[] Steps { get; }

            public string Title { get; }

            public Page(string title, string imagePrefix, params string[] steps)
            {
                Title = title;
                ImagePrefix = imagePrefix;
                Steps = steps;
            }
        }

        internal class Row
        {
            public string Cause { get; }

            public string Fix { get; }

            public string Symptom { get; }

            public Row(string symptom, string cause, string fix)
            {
                Symptom = symptom;
                Cause = cause;
                Fix = fix;
            }
        }
    }
}
