namespace MirrorCast.Annotations;

public sealed class AnnotationDocument
{
    private readonly List<IReadOnlyList<AnnotationItem>> _history = [Array.Empty<AnnotationItem>()];
    private int _historyIndex;

    public event Action? Changed;

    public IReadOnlyList<AnnotationItem> Items => _history[_historyIndex];
    public bool CanUndo => _historyIndex > 0;
    public bool CanRedo => _historyIndex < _history.Count - 1;
    public bool IsEmpty => Items.Count == 0;

    public void Add(AnnotationItem item)
    {
        var next = Items.ToList();
        next.Add(item);
        Commit(next);
    }

    public void Remove(AnnotationItem item)
    {
        var next = Items.ToList();
        if (!next.Remove(item)) return;
        Commit(next);
    }

    public void RemoveRange(IEnumerable<AnnotationItem> items)
    {
        var removed = items.ToHashSet();
        if (removed.Count == 0) return;

        var next = Items.Where(item => !removed.Contains(item)).ToList();
        if (next.Count == Items.Count) return;
        Commit(next);
    }

    public void Clear()
    {
        if (IsEmpty) return;
        Commit([]);
    }

    public void Reset()
    {
        _history.Clear();
        _history.Add(Array.Empty<AnnotationItem>());
        _historyIndex = 0;
        Changed?.Invoke();
    }

    public void Undo()
    {
        if (!CanUndo) return;
        _historyIndex--;
        Changed?.Invoke();
    }

    public void Redo()
    {
        if (!CanRedo) return;
        _historyIndex++;
        Changed?.Invoke();
    }

    private void Commit(IReadOnlyList<AnnotationItem> items)
    {
        if (_historyIndex < _history.Count - 1)
            _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);

        _history.Add(items);
        _historyIndex++;
        Changed?.Invoke();
    }
}
