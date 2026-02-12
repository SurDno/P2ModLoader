namespace P2ModLoader.Helper;

public static class AppIcon {
    public static Icon Instance { get; } = Icon.ExtractAssociatedIcon(Application.ExecutablePath)!;
}