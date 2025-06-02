// Documentation/ConceptualSwift/MacOSInfo.swift
//
// This file contains conceptual Swift code for a utility to get macOS information.
// It's intended to be compiled into a Swift framework (e.g., MacOSInfoKit.framework)
// and then called from C# in a Xamarin.Mac application, likely via Objective-C interop
// and Xamarin.Mac bindings or P/Invoke with Objective-C runtime calls.

import Foundation

@objc public class MacOSInfoProvider: NSObject {

    @objc public static func getOperatingSystemVersionString() -> String {
        return ProcessInfo.processInfo.operatingSystemVersionString
    }

    @objc public static func getMacOSProductName() -> String {
        let osVersion = ProcessInfo.processInfo.operatingSystemVersion
        // This is a simplified mapping. A more comprehensive one would be needed
        // for older versions or more precise naming.
        // See: https://en.wikipedia.org/wiki/MacOS_version_history for a list
        switch (osVersion.majorVersion, osVersion.minorVersion) {
            case (14, _): return "macOS Sonoma"
            case (13, _): return "macOS Ventura"
            case (12, _): return "macOS Monterey"
            case (11, _): return "macOS Big Sur"
            // Prior to macOS 11, major version was 10.
            case (10, 15): return "macOS Catalina"
            case (10, 14): return "macOS Mojave"
            case (10, 13): return "macOS High Sierra"
            case (10, 12): return "macOS Sierra"
            // Add more cases for older versions as needed
            default:
                // Fallback to generic version string if no specific name is mapped
                if osVersion.patchVersion > 0 {
                    return "macOS \(osVersion.majorVersion).\(osVersion.minorVersion).\(osVersion.patchVersion)"
                } else {
                    return "macOS \(osVersion.majorVersion).\(osVersion.minorVersion)"
                }
        }
    }

    // Example of another function that might be useful
    @objc public static func getHostName() -> String {
        return ProcessInfo.processInfo.hostName
    }

    // Example of getting CFBundleShortVersionString (App Version) from main bundle
    // This would typically be called from within the main app or its framework context
    @objc public static func getMainBundleShortVersionString() -> String? {
        return Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String
    }

    // Example of getting CFBundleIdentifier (App Bundle ID)
    @objc public static func getMainBundleIdentifier() -> String? {
        return Bundle.main.bundleIdentifier
    }
}
