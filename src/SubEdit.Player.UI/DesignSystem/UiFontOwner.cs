using System.Runtime.InteropServices;

namespace SubEdit.Player.UI.DesignSystem;

/// <summary>Owns only cloned fonts; borrowed/system fonts never become disposable dependencies.</summary>
public sealed class UiFontOwner : IDisposable
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, UiFontOwner> Owners = new();
    private readonly Control root;
    private readonly Control[] associated;
    public UiFontOwner(Control root, params Control[] associated)
    {
        this.root = root; this.associated = associated;
        Owners.Add(root, this);
    }
    /// <summary>Requested UI points, before WinForms' system-DPI-relative Font normalization.</summary>
    public static Font LogicalFont(Control control)
    {
        for (Control? owner = control; owner is not null; owner = owner.Parent)
            if (Owners.TryGetValue(owner, out var fonts)) return fonts._owned ?? owner.Font;
        return control.Font;
    }
    private Font? _owned;
    private Font? _effectiveOwned;
    private uint _referenceDpi;
    private bool _disposed;
    public Font Current => _owned ?? root.Font;

    /// <summary>UI #1: replace PMv2 copies of the previously shared font on an explicit font change.</summary>
    public bool SynchronizeDescendantFonts { get; init; }

    public void SetSize(float points)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_owned is not null && _owned.SizeInPoints == points && _owned.Style == FontStyle.Regular
            && _owned.Unit == GraphicsUnit.Point && _owned.FontFamily.Name == UiDesignTokens.Current.DialogFont.FontFamily.Name) return;
        using var requested = new Font(UiDesignTokens.Current.DialogFont.FontFamily, points, FontStyle.Regular, GraphicsUnit.Point);
        Set(requested);
    }

    public void Set(Font borrowed)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        // Control.Font uses value equality: replacing an equivalent instance can retain the old object.
        if (_owned is not null && _owned.Equals(borrowed)) return;
        var next = (Font)borrowed.Clone();
        var old = _owned;
        var oldEffective = _effectiveOwned;
        var effective = next;
        if (SynchronizeDescendantFonts)
        {
            // A fresh logical request uses the same reference as a fresh form. Font's
            // setter clears the PMv2 cache and treats its value as belonging to DeviceDpi.
            // Rebase this new request once; never scale the previous Control.Font.
            if (_referenceDpi == 0) _referenceDpi = GetDpiForSystem();
            if (_referenceDpi != 0 && root.DeviceDpi != _referenceDpi)
                effective = new Font(next.FontFamily, next.Size * root.DeviceDpi / _referenceDpi,
                    next.Style, next.Unit, next.GdiCharSet, next.GdiVerticalFont);
        }
        var targets = Consumers().ToArray();
        // Snapshot before assigning the root: PMv2 descendants may expose scaled copies,
        // not the original owned Font reference. Parent FontChanged alone does not clear
        // Control.ScaledControlFont, so native text can keep the old HFONT while UpDownBase
        // computes its PreferredHeight from the new parent font. Preserve distinct fonts.
        var previousEffective = root.Font;
        var shared = SynchronizeDescendantFonts
            ? targets.Where(control => control.Font.Equals(previousEffective) || ReferenceEquals(control.Font, old)
                || ReferenceEquals(control.Font, oldEffective)).ToArray()
            : [];
        root.Font = effective;
        foreach (var target in associated.Where(control => !control.IsDisposed)) target.Font = effective;
        foreach (var target in targets)
            if (!target.IsDisposed && (ReferenceEquals(target.Font, old) || ReferenceEquals(target.Font, oldEffective))) target.Font = effective;
        foreach (var target in shared)
            // Keep the same explicit-font policy from initial creation onward. Skipping
            // an inherited equal reference makes WinForms take a different PMv2 constants
            // path before versus after the first user font change (notably UpDown buttons).
            if (!target.IsDisposed) target.Font = effective;
        _owned = next;
        _effectiveOwned = ReferenceEquals(next, effective) ? null : effective;
        old?.Dispose();
        oldEffective?.Dispose();
    }

    private IEnumerable<Control> Consumers()
    {
        var visited = new HashSet<Control>();
        var pending = new Stack<Control>(associated.Prepend(root));
        while (pending.TryPop(out var control))
        {
            if (control.IsDisposed || !visited.Add(control)) continue;
            yield return control;
            foreach (Control child in control.Controls) pending.Push(child);
            if (control.ContextMenuStrip is { } menu) pending.Push(menu);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Owners.Remove(root);
        // Also safe if a caller disposes the scope before its form.
        foreach (var target in Consumers().ToArray())
            if (ReferenceEquals(target.Font, _owned) || ReferenceEquals(target.Font, _effectiveOwned)) target.Font = UiDesignTokens.Current.DialogFont;
        _owned?.Dispose();
        _effectiveOwned?.Dispose();
        _owned = null;
        _effectiveOwned = null;
    }
    [DllImport("user32.dll")] private static extern uint GetDpiForSystem();
}
