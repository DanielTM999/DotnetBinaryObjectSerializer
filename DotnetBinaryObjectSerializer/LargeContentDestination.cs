namespace DotnetBinaryObjectSerializer;

/// <summary>A caller-selected destination for decoded LARGE_CONTENT.</summary>
public sealed class LargeContentDestination : IDisposable
{
    private readonly Func<StreamContent> _completedContent;
    private readonly Action _abort;
    private StreamContent? _completed;

    private LargeContentDestination(Stream output, Func<StreamContent> completedContent, Action? abort = null)
    {
        Output = output;
        _completedContent = completedContent;
        _abort = abort ?? (() => { });
    }

    public Stream Output { get; }
    public static LargeContentDestination Of(Stream output, Func<StreamContent> completedContent) => new(output, completedContent);
    public static LargeContentDestination To(FileInfo file) => new(file.Open(FileMode.Create, FileAccess.Write, FileShare.None), () => StreamContent.From(file));
    public StreamContent CompletedContent() => _completed ??= _completedContent();
    public void Abort() { if (_completed == null) _abort(); }
    public void Dispose() => Output.Dispose();
}
