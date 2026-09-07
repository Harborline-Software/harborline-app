using Harborline.App.Abstractions;
using Harborline.App.Blazor.Hybrid;
using Harborline.App.Blazor.Shell;
using Harborline.App.Testing;

_ = typeof(HarborlineAppShell);
IHarborlineSecureStore store = new MemoryHarborlineSecureStore();
await store.SetAsync("probe", HarborlineAppRevisions.ProductInterface);
return await store.GetAsync("probe") == HarborlineAppRevisions.ProductInterface ? 0 : 1;
