using UNSInfra.ConnectionSDK.Abstractions;

namespace UNSInfra.ConnectionSDK.Services;

/// <summary>
/// Default implementation of IConnectionRegistry
/// </summary>
public class ConnectionRegistry : IConnectionRegistry
{
    private readonly Dictionary<string, IConnectionDescriptor> _descriptors = new();
    private readonly object _lockObject = new();

    /// <inheritdoc />
    public void RegisterConnection(IConnectionDescriptor descriptor)
    {
        if (descriptor == null)
            throw new ArgumentNullException(nameof(descriptor));

        if (string.IsNullOrEmpty(descriptor.ConnectionType))
            throw new ArgumentException("Connection type cannot be null or empty", nameof(descriptor));

        lock (_lockObject)
        {
            _descriptors[descriptor.ConnectionType] = descriptor;
        }
    }

    /// <inheritdoc />
    public IEnumerable<IConnectionDescriptor> GetAllDescriptors()
    {
        lock (_lockObject)
        {
            return _descriptors.Values.ToList();
        }
    }

    /// <inheritdoc />
    public IConnectionDescriptor? GetDescriptor(string connectionType)
    {
        if (string.IsNullOrEmpty(connectionType))
            return null;

        lock (_lockObject)
        {
            return _descriptors.TryGetValue(connectionType, out var descriptor) ? descriptor : null;
        }
    }

    /// <inheritdoc />
    public IEnumerable<IConnectionDescriptor> GetDescriptorsByCategory(string category)
    {
        if (string.IsNullOrEmpty(category))
            return Enumerable.Empty<IConnectionDescriptor>();

        lock (_lockObject)
        {
            return _descriptors.Values
                .Where(d => string.Equals(d.Category, category, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    /// <inheritdoc />
    public bool IsRegistered(string connectionType)
    {
        if (string.IsNullOrEmpty(connectionType))
            return false;

        lock (_lockObject)
        {
            return _descriptors.ContainsKey(connectionType);
        }
    }
}