namespace Harborline.App.Blazor.Hybrid;

public sealed record HarborlineNativeHostCapabilities(
    bool Camera,
    bool SecureStorage,
    bool Connectivity,
    bool DeepLinks,
    bool Notifications);

public sealed record HarborlineCapturedMedia(
    Stream Content,
    string ContentType,
    string FileName,
    DateTimeOffset CapturedAt);

public interface IHarborlineSecureStore
{
    ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken = default);
    ValueTask SetAsync(string key, string value, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);
}

public interface IHarborlineMediaCapture
{
    ValueTask<HarborlineCapturedMedia?> CapturePhotoAsync(CancellationToken cancellationToken = default);
}

public interface IHarborlineNativeHost
{
    HarborlineNativeHostCapabilities Capabilities { get; }
    event EventHandler<Uri>? DeepLinkReceived;
    event EventHandler<bool>? ConnectivityChanged;
}
