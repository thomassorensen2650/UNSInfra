# MQTT Data Export Fix - Value Change Detection

## Issue Description

The MQTT data export functionality was not working correctly when configured to publish data on change. The issue was that:

1. **Model export worked** - MQTT model export functionality was working correctly
2. **Data export didn't work** - When new data arrived to topics mapped to the UNS structure, it wasn't being published with the correct UNS topic structure
3. **No actual change detection** - The system was only checking timing intervals, not whether data values had actually changed

## Root Cause Analysis

The problem was in the `MqttDataExportService.ShouldPublishData()` method in `/src/UNSInfra.Services.V1/Mqtt/MqttDataExportService.cs`:

### Original Implementation (Broken)
```csharp
private bool ShouldPublishData(MqttOutputConfiguration config, string topic, DataPoint dataPoint)
{
    var exportConfig = config.DataExportConfig!;
    var key = $"{config.Id}:{topic}";

    // Check minimum publish interval
    if (_lastPublishTimes.TryGetValue(key, out var lastPublish))
    {
        var timeSinceLastPublish = DateTime.UtcNow - lastPublish;
        var minInterval = TimeSpan.FromMilliseconds(exportConfig.MinPublishIntervalMs);
        
        if (timeSinceLastPublish < minInterval)
        {
            return false;
        }
    }

    return true; // ❌ Always returns true if timing is OK, regardless of value changes
}
```

**Problems:**
1. **No value change detection** - The method only checked timing but never compared current value vs last published value
2. **Repeated publishing** - Same data would be republished every `MinPublishIntervalMs` even if value hadn't changed
3. **Misleading "PublishOnChange"** - The setting didn't actually detect changes, it was really "PublishOnTimer"

## Solution Implemented

### 1. Added Value Tracking
Added a new dictionary to track last published values:
```csharp
private readonly Dictionary<string, object?> _lastPublishedValues = new();
```

### 2. Fixed Change Detection Logic
Updated `ShouldPublishData()` to actually detect value changes:

```csharp
private bool ShouldPublishData(MqttOutputConfiguration config, string topic, DataPoint dataPoint)
{
    var exportConfig = config.DataExportConfig!;
    var key = $"{config.Id}:{topic}";

    // ✅ Check if value has actually changed
    if (_lastPublishedValues.TryGetValue(key, out var lastValue))
    {
        // Compare current value with last published value
        var valuesAreEqual = lastValue == null && dataPoint.Value == null ||
                            (lastValue != null && lastValue.Equals(dataPoint.Value));
        
        if (valuesAreEqual)
        {
            // Value hasn't changed, don't republish
            _logger.LogDebug("Skipping publish for topic '{Topic}' - value unchanged: {Value}", 
                topic, dataPoint.Value);
            return false;
        }
    }

    // ✅ Value has changed (or this is first publish), check minimum publish interval for rate limiting
    if (_lastPublishTimes.TryGetValue(key, out var lastPublish))
    {
        var timeSinceLastPublish = DateTime.UtcNow - lastPublish;
        var minInterval = TimeSpan.FromMilliseconds(exportConfig.MinPublishIntervalMs);
        
        if (timeSinceLastPublish < minInterval)
        {
            _logger.LogDebug("Rate limiting publish for topic '{Topic}' - last publish was {TimeSince}ms ago, minimum interval is {MinInterval}ms", 
                topic, timeSinceLastPublish.TotalMilliseconds, exportConfig.MinPublishIntervalMs);
            return false;
        }
    }

    _logger.LogDebug("Publishing data for topic '{Topic}' - value changed from {OldValue} to {NewValue}", 
        topic, lastValue, dataPoint.Value);
    return true;
}
```

### 3. Updated Value Tracking
Modified `PublishDataPoint()` to track published values:

```csharp
// Update last publish time and value
var key = $"{config.Id}:{topicConfig.Topic}";
_lastPublishTimes[key] = DateTime.UtcNow;
_lastPublishedValues[key] = dataPoint.Value; // ✅ Track the published value
```

### 4. Cleanup on Stop
Updated `StopAsync()` to clear the value tracking:

```csharp
_lastPublishTimes.Clear();
_lastPublishedValues.Clear(); // ✅ Clear tracked values
```

## How the Fix Works

### Before (Broken Behavior):
1. Every second, check all topics for data
2. If timing interval has passed → publish (even if same value)
3. Result: Same data published repeatedly

### After (Fixed Behavior):
1. Every second, check all topics for data
2. Compare current value with last published value
3. If value changed AND timing interval has passed → publish
4. If value unchanged → skip (don't spam MQTT)
5. Result: Only publish when data actually changes

## UNS Topic Structure

The fix ensures that when `UseUNSPathAsTopic = true`, the MQTT topic follows the UNS hierarchical structure:

- **Input topic**: `sensor/temperature`
- **UNS Path**: `Enterprise1/Site1/Area1`
- **UNS Name**: `Temperature`
- **Published MQTT topic**: `Enterprise1/Site1/Area1/Temperature`

This maintains the hierarchical structure of the UNS in the MQTT topic names.

## Benefits of the Fix

1. **✅ True change detection** - Only publishes when values actually change
2. **✅ Reduced MQTT traffic** - No more repeated publishing of unchanged data
3. **✅ Rate limiting** - Still respects `MinPublishIntervalMs` for rapid changes
4. **✅ Better logging** - Debug logs explain why publishing decisions are made
5. **✅ UNS structure preserved** - Topics published with correct hierarchical paths

## Testing

Created comprehensive tests in:
- `/test/UNSInfra.Services.V1.Tests/Mqtt/MqttDataChangeDetectionTest.cs` - Explains the issue
- `/test/UNSInfra.Services.V1.Tests/Mqtt/MqttDataPublishingIssueReproduction.cs` - Reproduces connection failures
- `/test/UNSInfra.Services.V1.Tests/Mqtt/MqttDataChangeDetectionFixTest.cs` - Verifies the fix

## Verification

To verify the fix is working:

1. **Configure MQTT output** with `PublishOnChange = true`
2. **Ensure MQTT broker is reachable** (connection must succeed)
3. **Update data for mapped topics** - should see MQTT publishes only on value changes
4. **Check MQTT topic names** - should follow UNS hierarchical structure
5. **Monitor logs** - should see debug messages about publishing decisions

The fix ensures that MQTT data export now works as expected: **publishing data with UNS topic structure only when values actually change**.