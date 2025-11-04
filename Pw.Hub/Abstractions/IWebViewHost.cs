using System.Threading.Tasks;
using Microsoft.Web.WebView2.Wpf;

namespace Pw.Hub.Abstractions;

/// <summary>
/// UI host abstraction for WebView2 control that can safely replace the browser control
/// in the visual tree (detach/attach handlers, preserve layout) on the UI thread.
/// </summary>
public interface IWebViewHost
{
    /// <summary>
    /// Gets the current WebView2 control instance hosted in the UI.
    /// </summary>
    WebView2 Current { get; }

    /// <summary>
    /// Replaces the current WebView2 control with the provided instance in the visual tree.
    /// Implementation must run on the UI thread and ensure safe handler rewiring and layout preservation.
    /// </summary>
    Task ReplaceAsync(WebView2 newControl);
}
