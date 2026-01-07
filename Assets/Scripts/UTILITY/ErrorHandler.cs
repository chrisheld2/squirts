using UnityEngine;
using System.Collections;
using System.Text.RegularExpressions;
using System.Collections.Generic;

public class ErrorHandler : MonoBehaviour
{
    [System.Serializable]
    public class MethodMapping
    {
        public string originalMethod;
        public string obfuscatedMethod;
    }

    [Header("Debug Settings")]
    public bool enableVerboseLogging = true;
    public bool parseJavaScriptStackTrace = true;

    [Header("Method Mappings (Optional)")]
    [Tooltip("Map obfuscated method names back to original C# methods")]
    public List<MethodMapping> methodMappings = new List<MethodMapping>();

    void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
        ConfigureLogging();
    }

    void ConfigureLogging()
    {
        // Force detailed logging for all platforms during debugging
        Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.Full);
        Application.SetStackTraceLogType(LogType.Exception, StackTraceLogType.Full);
        Application.SetStackTraceLogType(LogType.Assert, StackTraceLogType.Full);
        Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.ScriptOnly);

        Debug.Log("ErrorHandler: Configured detailed stack trace logging");
        Debug.Log($"Platform: {Application.platform}");
        Debug.Log($"Unity Version: {Application.unityVersion}");
        Debug.Log($"Development Build: {Debug.isDebugBuild}");

#if UNITY_WEBGL && !UNITY_EDITOR
        Debug.Log("WebGL build detected - enhanced error tracking enabled");
#endif
    }

    void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    void HandleLog(string logString, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception)
        {
            Debug.Log("=== ERROR CAUGHT ===");
            Debug.Log("Error Message: " + logString);
            Debug.Log("Platform: " + Application.platform);
            Debug.Log("Unity Version: " + Application.unityVersion);

            // Try to get current stack trace if none provided
            if (string.IsNullOrEmpty(stackTrace))
            {
                try
                {
                    stackTrace = System.Environment.StackTrace;
                    Debug.Log("Using Environment.StackTrace as fallback");
                }
                catch
                {
                    try
                    {
                        throw new System.Exception("Stack trace capture");
                    }
                    catch (System.Exception ex)
                    {
                        stackTrace = ex.StackTrace;
                        Debug.Log("Using Exception.StackTrace as fallback");
                    }
                }
            }

            if (!string.IsNullOrEmpty(stackTrace))
            {
                ParseStackTrace(stackTrace);
            }
            else
            {
                Debug.Log("No stack trace available - trying alternative methods");
                TryGetCurrentMethodInfo();
            }

            Debug.Log("====================");
        }
    }

    void ParseStackTrace(string stackTrace)
    {
        if (enableVerboseLogging)
        {
            Debug.Log("Full Stack Trace:\n" + stackTrace);
        }

        string[] lines = stackTrace.Split('\n');
        bool foundRelevantInfo = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // Try different parsing methods
            if (TryParseCSharpStackTrace(line) ||
                TryParseWebGLStackTrace(line) ||
                TryParseJavaScriptStackTrace(line))
            {
                foundRelevantInfo = true;
            }
        }

        if (!foundRelevantInfo)
        {
            Debug.Log("Could not parse stack trace format. Raw first line: " +
                     (lines.Length > 0 ? lines[0] : "No lines available"));

            // Try to extract any useful information
            ExtractAnyUsefulInfo(stackTrace);
        }
    }

    bool TryParseCSharpStackTrace(string line)
    {
        // Standard C# stack trace format: "ClassName.MethodName () (at Assets/Scripts/FileName.cs:123)"
        Match match = Regex.Match(line, @"(\w+)\.(\w+)\s*\([^)]*\)\s*\(at\s+([^:]+):(\d+)\)");
        if (match.Success)
        {
            string className = match.Groups[1].Value;
            string methodName = match.Groups[2].Value;
            string filePath = match.Groups[3].Value;
            string lineNumber = match.Groups[4].Value;

            Debug.Log($"C# Error in: {className}.{methodName}()");
            Debug.Log($"File: {filePath} at line {lineNumber}");
            return true;
        }

        // Alternative format: "at ClassName.MethodName (FileName.cs:123)"
        match = Regex.Match(line, @"at\s+(\w+)\.(\w+)\s*[^(]*\(([^:]+):(\d+)\)");
        if (match.Success)
        {
            string className = match.Groups[1].Value;
            string methodName = match.Groups[2].Value;
            string fileName = match.Groups[3].Value;
            string lineNumber = match.Groups[4].Value;

            Debug.Log($"C# Error in: {className}.{methodName}()");
            Debug.Log($"File: {fileName} at line {lineNumber}");
            return true;
        }

        return false;
    }

    bool TryParseWebGLStackTrace(string line)
    {
        // WebGL sometimes preserves method names in a different format
        // Look for patterns like: "UnityEngine.MonoBehaviour.StartCoroutine"
        Match match = Regex.Match(line, @"(\w+(?:\.\w+)*)\s*\(");
        if (match.Success)
        {
            string fullMethodPath = match.Groups[1].Value;

            // Try to map obfuscated names back to original names
            string mappedMethod = MapObfuscatedMethod(fullMethodPath);
            if (mappedMethod != fullMethodPath)
            {
                Debug.Log($"WebGL Error in mapped method: {mappedMethod} (was: {fullMethodPath})");
            }
            else
            {
                Debug.Log($"WebGL Error in method: {fullMethodPath}");
            }

            // Try to extract any numbers that might be line references
            Match lineMatch = Regex.Match(line, @":(\d+)");
            if (lineMatch.Success)
            {
                Debug.Log($"Possible line reference: {lineMatch.Groups[1].Value}");
            }

            return true;
        }

        return false;
    }

    bool TryParseJavaScriptStackTrace(string line)
    {
        if (!parseJavaScriptStackTrace) return false;

        // JavaScript stack trace patterns (WebGL compilation result)
        // Pattern: "at functionName (file.js:line:column)"
        Match match = Regex.Match(line, @"at\s+([^(]+)\s*\(([^:]+):(\d+):(\d+)\)");
        if (match.Success)
        {
            string functionName = match.Groups[1].Value.Trim();
            string fileName = match.Groups[2].Value;
            string lineNumber = match.Groups[3].Value;
            string columnNumber = match.Groups[4].Value;

            // Try to map back to C# method if possible
            string mappedMethod = MapObfuscatedMethod(functionName);

            Debug.Log($"JavaScript Error in: {mappedMethod}");
            Debug.Log($"JS File: {fileName} at {lineNumber}:{columnNumber}");

            if (mappedMethod != functionName)
            {
                Debug.Log($"(Original JS function: {functionName})");
            }

            return true;
        }

        // Simpler pattern: "functionName@file:line:column"
        match = Regex.Match(line, @"([^@]+)@([^:]+):(\d+):(\d+)");
        if (match.Success)
        {
            string functionName = match.Groups[1].Value.Trim();
            string fileName = match.Groups[2].Value;
            string lineNumber = match.Groups[3].Value;

            string mappedMethod = MapObfuscatedMethod(functionName);
            Debug.Log($"JavaScript Error in: {mappedMethod} at {fileName}:{lineNumber}");
            return true;
        }

        return false;
    }

    void ExtractAnyUsefulInfo(string stackTrace)
    {
        // Look for any class names that might still be preserved
        MatchCollection classMatches = Regex.Matches(stackTrace, @"[A-Z][a-zA-Z0-9_]*\.[A-Z][a-zA-Z0-9_]*");
        if (classMatches.Count > 0)
        {
            Debug.Log("Possible class references found:");
            HashSet<string> uniqueClasses = new HashSet<string>();
            foreach (Match match in classMatches)
            {
                uniqueClasses.Add(match.Value);
            }
            foreach (string className in uniqueClasses)
            {
                Debug.Log("- " + className);
            }
        }

        // Look for any line numbers
        MatchCollection lineMatches = Regex.Matches(stackTrace, @":(\d+)");
        if (lineMatches.Count > 0)
        {
            Debug.Log("Possible line numbers: ");
            foreach (Match match in lineMatches)
            {
                Debug.Log("- Line " + match.Groups[1].Value);
            }
        }

        // Look for file references
        MatchCollection fileMatches = Regex.Matches(stackTrace, @"(\w+\.(?:cs|js))\b");
        if (fileMatches.Count > 0)
        {
            Debug.Log("Possible file references:");
            HashSet<string> uniqueFiles = new HashSet<string>();
            foreach (Match match in fileMatches)
            {
                uniqueFiles.Add(match.Value);
            }
            foreach (string fileName in uniqueFiles)
            {
                Debug.Log("- " + fileName);
            }
        }
    }

    string MapObfuscatedMethod(string obfuscatedMethod)
    {
        // Check if we have a mapping for this obfuscated method
        foreach (var mapping in methodMappings)
        {
            if (mapping.obfuscatedMethod == obfuscatedMethod)
            {
                return mapping.originalMethod;
            }
        }

        // If no mapping found, return the original
        return obfuscatedMethod;
    }

    // Call this method to add mappings at runtime if needed
    public void AddMethodMapping(string original, string obfuscated)
    {
        methodMappings.Add(new MethodMapping
        {
            originalMethod = original,
            obfuscatedMethod = obfuscated
        });
    }

    void TryGetCurrentMethodInfo()
    {
        try
        {
            var stackTrace = new System.Diagnostics.StackTrace(true);
            var frames = stackTrace.GetFrames();

            Debug.Log("=== STACK TRACE FRAMES ===");
            for (int i = 0; i < frames.Length && i < 10; i++)
            {
                var frame = frames[i];
                var method = frame.GetMethod();
                if (method != null)
                {
                    var className = method.DeclaringType?.Name ?? "Unknown";
                    var methodName = method.Name ?? "Unknown";
                    var fileName = frame.GetFileName() ?? "Unknown";
                    var lineNumber = frame.GetFileLineNumber();

                    Debug.Log($"Frame {i}: {className}.{methodName}() in {fileName}:{lineNumber}");
                }
            }
            Debug.Log("=== END STACK TRACE ===");
        }
        catch (System.Exception ex)
        {
            Debug.Log("Failed to get stack trace info: " + ex.Message);
        }
    }

    // Helper method to enable/disable verbose logging at runtime
    public void SetVerboseLogging(bool enabled)
    {
        enableVerboseLogging = enabled;
    }
}