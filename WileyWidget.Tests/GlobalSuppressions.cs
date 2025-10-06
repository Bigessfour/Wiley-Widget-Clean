// This file suppresses CA1416 (platform compatibility) warnings for unit tests
// Tests are executed on Windows agents and may call WPF APIs; suppress warnings project-wide
using System.Runtime.Versioning;

[assembly: SupportedOSPlatform("windows")]

// Additionally, if specific CA suppression attributes are desired they can be added here.
