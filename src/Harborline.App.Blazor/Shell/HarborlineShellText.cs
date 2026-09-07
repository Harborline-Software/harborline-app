namespace Harborline.App.Blazor.Shell;

public sealed record HarborlineShellText(
    string PrimaryNavigation = "Primary",
    string ExpandSidebar = "Expand sidebar",
    string CollapseSidebar = "Collapse sidebar",
    string Expand = "Expand",
    string Collapse = "Collapse",
    string Account = "Account",
    string Notifications = "Notifications",
    string Unread = "unread",
    string OpenNavigation = "Open navigation",
    string Context = "Context",
    string Close = "Close");
