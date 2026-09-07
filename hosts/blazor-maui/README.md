# Blazor Hybrid mobile host

This is the registered .NET MAUI Blazor Hybrid desktop/iOS/Android host envelope. The reusable,
workload-independent seams are already packaged as `Harborline.App.Blazor.Hybrid`. The executable
MAUI project is intentionally not generated until the .NET MAUI workload, bundle identifiers,
signing teams, and minimum OS versions are recorded; an empty project would falsely claim a green
mobile projection. Its first gate is an unsigned iOS simulator and Android emulator build using the
same packaged `Harborline.App.Blazor` shell.
