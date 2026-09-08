namespace Harborline.App.Blazor.ReferenceHost.Admin.Forms;

public sealed record RuntimeForm(string FormId, string Version, InternationalizedText? Title, IReadOnlyList<RuntimeSection> Sections);
public sealed record RuntimeSection(string Id, InternationalizedText Title, IReadOnlyList<RuntimeField> Fields);
public sealed record RuntimeField(string Name, InternationalizedText Label, bool IsReadable, string? Value, RuntimeRules? Rules);
public sealed record RuntimeRules(bool Visible, bool Required, bool ReadOnly);
public sealed record RuntimeReceipt(string InstanceId, string? Projection, IReadOnlyList<RuntimeSkip>? Skips);
public sealed record RuntimeSkip(string Reason, string Field);
