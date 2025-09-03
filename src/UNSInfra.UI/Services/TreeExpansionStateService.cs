using System.Collections.Concurrent;

namespace UNSInfra.UI.Services;

/// <summary>
/// Service to manage tree node expansion states across component refreshes
/// </summary>
public class TreeExpansionStateService
{
    private readonly ConcurrentDictionary<string, bool> _expansionStates = new();

    /// <summary>
    /// Gets the expansion state for a node path, or null if not tracked
    /// </summary>
    /// <param name="nodePath">The full path of the node</param>
    /// <returns>True if expanded, false if collapsed, null if not tracked</returns>
    public bool? GetExpansionState(string nodePath)
    {
        if (string.IsNullOrEmpty(nodePath))
            return null;
            
        return _expansionStates.TryGetValue(nodePath, out var state) ? state : null;
    }

    /// <summary>
    /// Sets the expansion state for a node path
    /// </summary>
    /// <param name="nodePath">The full path of the node</param>
    /// <param name="isExpanded">True if expanded, false if collapsed</param>
    public void SetExpansionState(string nodePath, bool isExpanded)
    {
        if (string.IsNullOrEmpty(nodePath))
            return;
            
        _expansionStates.AddOrUpdate(nodePath, isExpanded, (key, oldValue) => isExpanded);
    }

    /// <summary>
    /// Removes the expansion state for a node path
    /// </summary>
    /// <param name="nodePath">The full path of the node</param>
    public void RemoveExpansionState(string nodePath)
    {
        if (string.IsNullOrEmpty(nodePath))
            return;
            
        _expansionStates.TryRemove(nodePath, out _);
    }

    /// <summary>
    /// Clears all expansion states
    /// </summary>
    public void ClearAll()
    {
        _expansionStates.Clear();
    }

    /// <summary>
    /// Gets all currently tracked expansion states
    /// </summary>
    /// <returns>Dictionary of node paths and their expansion states</returns>
    public IReadOnlyDictionary<string, bool> GetAllExpansionStates()
    {
        return new Dictionary<string, bool>(_expansionStates);
    }
}