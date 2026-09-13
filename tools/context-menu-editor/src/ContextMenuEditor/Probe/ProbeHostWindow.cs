namespace ContextMenuEditor.Probe;

internal sealed class ProbeHostWindow : IDisposable
{
    private readonly System.Windows.Forms.Form _form;

    public ProbeHostWindow()
    {
        _form = new System.Windows.Forms.Form
        {
            ShowInTaskbar = false,
            StartPosition = System.Windows.Forms.FormStartPosition.Manual,
            Location = new System.Drawing.Point(-32000, -32000),
            Size = new System.Drawing.Size(1, 1),
        };

        _form.CreateControl();
        Handle = _form.Handle;
    }

    public IntPtr Handle { get; }

    public void Dispose() => _form.Dispose();
}
