using System.Text.Json;
using System.Text.RegularExpressions;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Infrastructure.Services;

public partial class FastAddParserService : IFastAddParserService
{
    private static readonly JsonSerializerOptions IndentedJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string StripMarkdownFences(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var cleaned = input.Trim();

        // Strip ```json ... ``` or ``` ... ```
        if (cleaned.StartsWith("```"))
        {
            var firstNewLine = cleaned.IndexOf('\n');
            if (firstNewLine != -1)
            {
                cleaned = cleaned[(firstNewLine + 1)..];
            }
        }

        if (cleaned.EndsWith("```"))
        {
            var lastFence = cleaned.LastIndexOf("```", StringComparison.Ordinal);
            if (lastFence != -1)
            {
                cleaned = cleaned[..lastFence];
            }
        }

        return cleaned.Trim();
    }

    public string FormatJson(string input)
    {
        var cleaned = StripMarkdownFences(input);
        if (string.IsNullOrWhiteSpace(cleaned))
            return input;

        try
        {
            using var doc = JsonDocument.Parse(cleaned);
            return JsonSerializer.Serialize(doc.RootElement, IndentedJsonOptions);
        }
        catch
        {
            return input;
        }
    }

    public FastAddValidationResult ParseAndValidate(string rawInput, FastAddScope? forcedScope = null)
    {
        var result = new FastAddValidationResult();

        var cleaned = StripMarkdownFences(rawInput);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            result.Errors.Add("Please paste task JSON before validating.");
            result.IsValid = false;
            return result;
        }

        JsonDocument doc;
        try
        {
            var docOptions = new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            };
            doc = JsonDocument.Parse(cleaned, docOptions);
        }
        catch (JsonException ex)
        {
            result.Errors.Add($"Invalid JSON syntax: {ex.Message} (Line: {ex.LineNumber}, Pos: {ex.BytePositionInLine})");
            result.IsValid = false;
            return result;
        }

        using (doc)
        {
            var root = doc.RootElement;
            JsonElement itemsArray;

            if (root.ValueKind == JsonValueKind.Array)
            {
                itemsArray = root;
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                // Look for common array properties
                if (TryGetProperty(root, out var prop, "majorTasks", "majortasks", "tasks", "cards", "items", "plan", "data"))
                {
                    itemsArray = prop;
                }
                else if (TryGetProperty(root, out var minorProp, "minorTasks", "minortasks", "subtasks", "checklist"))
                {
                    itemsArray = minorProp;
                    forcedScope ??= FastAddScope.MinorTasks;
                }
                else
                {
                    // Single object treated as single-item array
                    result.Errors.Add("Expected a JSON array of tasks or an object with 'tasks' / 'majorTasks' property.");
                    result.IsValid = false;
                    return result;
                }
            }
            else
            {
                result.Errors.Add("JSON root must be an array of tasks or an object containing a tasks list.");
                result.IsValid = false;
                return result;
            }

            if (itemsArray.ValueKind != JsonValueKind.Array)
            {
                result.Errors.Add("The tasks collection found in JSON is not an array.");
                result.IsValid = false;
                return result;
            }

            var count = itemsArray.GetArrayLength();
            if (count == 0)
            {
                result.Errors.Add("The tasks array is empty. Please provide at least one task.");
                result.IsValid = false;
                return result;
            }

            // Scope detection
            var detectedScope = forcedScope ?? DetectScope(itemsArray);
            result.DetectedScope = detectedScope;

            if (detectedScope == FastAddScope.MajorTasks)
            {
                ParseMajorTasks(itemsArray, result);
            }
            else
            {
                ParseMinorTasks(itemsArray, result);
            }

            result.IsValid = result.Errors.Count == 0;
            return result;
        }
    }

    private static FastAddScope DetectScope(JsonElement array)
    {
        foreach (var elem in array.EnumerateArray())
        {
            if (elem.ValueKind != JsonValueKind.Object)
                continue;

            // If an element contains minorTasks or subtasks, or priority, it's major tasks
            if (HasProperty(elem, "minorTasks", "minortasks", "subtasks", "priority", "dueDate", "due_date"))
            {
                return FastAddScope.MajorTasks;
            }

            // If an element only has jobType or target or notes
            if (HasProperty(elem, "jobType", "job_type", "target", "notes") && !HasProperty(elem, "priority", "dueDate"))
            {
                return FastAddScope.MinorTasks;
            }
        }

        return FastAddScope.MajorTasks;
    }

    private static void ParseMajorTasks(JsonElement array, FastAddValidationResult result)
    {
        var index = 0;
        foreach (var elem in array.EnumerateArray())
        {
            index++;
            if (elem.ValueKind != JsonValueKind.Object)
            {
                result.Errors.Add($"Item #{index} in JSON array is not an object.");
                continue;
            }

            var major = new FastAddMajorTaskDto();

            // 1. Title (Required)
            if (TryGetString(elem, out var title, "title", "name", "taskTitle", "task_title") && !string.IsNullOrWhiteSpace(title))
            {
                major.Title = title.Trim();
            }
            else
            {
                result.Errors.Add($"Major Task #{index} is missing the required 'title' field.");
            }

            // 2. Description (Optional / Nullable)
            if (TryGetString(elem, out var desc, "description", "desc"))
                major.Description = string.IsNullOrWhiteSpace(desc) ? null : desc.Trim();

            // 3. Details (Optional / Nullable)
            if (TryGetString(elem, out var details, "details", "detail"))
                major.Details = string.IsNullOrWhiteSpace(details) ? null : details.Trim();

            // 4. ExternalLink (Optional / Nullable)
            if (TryGetString(elem, out var link, "externalLink", "externallink", "external_link", "link", "url"))
                major.ExternalLink = string.IsNullOrWhiteSpace(link) ? null : link.Trim();

            // 5. Priority (Optional, defaults to Medium)
            if (TryGetString(elem, out var prioStr, "priority", "prio"))
            {
                major.Priority = prioStr;
                major.ResolvedPriority = ParsePriority(prioStr, major.Title, index, result.Warnings);
            }
            else if (TryGetInt(elem, out var prioInt, "priority", "prio"))
            {
                major.ResolvedPriority = prioInt switch
                {
                    0 => Priority.Low,
                    1 => Priority.Medium,
                    2 => Priority.High,
                    3 => Priority.Critical,
                    _ => Priority.Medium
                };
            }
            else
            {
                major.ResolvedPriority = Priority.Medium;
            }

            // 6. State (Optional, defaults to Todo)
            if (TryGetString(elem, out var stateStr, "state", "status"))
            {
                major.State = stateStr;
                major.ResolvedState = ParseMajorTaskState(stateStr, major.Title, index, result.Warnings);
            }
            else
            {
                major.ResolvedState = MajorTaskState.Todo;
            }

            // 7. DueDate (Optional, defaults to +7 days)
            if (TryGetString(elem, out var dueDateStr, "dueDate", "duedate", "due_date", "due"))
            {
                major.DueDate = dueDateStr;
                if (DateTime.TryParse(dueDateStr, out var parsedDate))
                {
                    major.ResolvedDueDate = parsedDate.ToUniversalTime();
                }
                else
                {
                    major.ResolvedDueDate = DateTime.UtcNow.AddDays(7);
                    result.Warnings.Add($"Task '{major.Title}': Could not parse DueDate '{dueDateStr}'. Defaulted to 7 days from today.");
                }
            }
            else
            {
                major.ResolvedDueDate = DateTime.UtcNow.AddDays(7);
            }

            // 8. Nested Minor Tasks (Optional)
            if (TryGetProperty(elem, out var minorElem, "minorTasks", "minortasks", "minor_tasks", "subtasks", "sub_tasks", "checklist") &&
                minorElem.ValueKind == JsonValueKind.Array)
            {
                var subIndex = 0;
                foreach (var sub in minorElem.EnumerateArray())
                {
                    subIndex++;
                    if (sub.ValueKind != JsonValueKind.Object)
                        continue;

                    var minor = ParseMinorTaskDto(sub, major.Title, subIndex, result);
                    major.MinorTasks.Add(minor);
                }
            }

            result.MajorTasks.Add(major);
        }
    }

    private static void ParseMinorTasks(JsonElement array, FastAddValidationResult result)
    {
        var index = 0;
        foreach (var elem in array.EnumerateArray())
        {
            index++;
            if (elem.ValueKind != JsonValueKind.Object)
            {
                result.Errors.Add($"Item #{index} in array is not an object.");
                continue;
            }

            var minor = ParseMinorTaskDto(elem, "Minor Task", index, result);
            result.MinorTasks.Add(minor);
        }
    }

    private static FastAddMinorTaskDto ParseMinorTaskDto(JsonElement elem, string parentTitle, int index, FastAddValidationResult result)
    {
        var minor = new FastAddMinorTaskDto();

        // 1. Title (Required)
        if (TryGetString(elem, out var title, "title", "name", "taskTitle", "task_title") && !string.IsNullOrWhiteSpace(title))
        {
            minor.Title = title.Trim();
        }
        else
        {
            result.Errors.Add($"Sub-task #{index} under '{parentTitle}' is missing the required 'title' field.");
        }

        // 2. Description (Optional / Nullable)
        if (TryGetString(elem, out var desc, "description", "desc"))
            minor.Description = string.IsNullOrWhiteSpace(desc) ? null : desc.Trim();

        // 3. Target (Optional / Nullable)
        if (TryGetString(elem, out var target, "target", "acceptanceCriteria", "acceptance_criteria", "goal"))
            minor.Target = string.IsNullOrWhiteSpace(target) ? null : target.Trim();

        // 4. Notes (Optional / Nullable)
        if (TryGetString(elem, out var notes, "notes", "note", "comment", "comments"))
            minor.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        // 5. ExternalLink (Optional / Nullable)
        if (TryGetString(elem, out var link, "externalLink", "externallink", "external_link", "link", "url"))
            minor.ExternalLink = string.IsNullOrWhiteSpace(link) ? null : link.Trim();

        // 6. JobType (Optional / Nullable)
        if (TryGetString(elem, out var jobStr, "jobType", "jobtype", "job_type", "type"))
        {
            minor.JobType = jobStr;
            minor.ResolvedJobType = ParseJobType(jobStr, minor.Title, index, result.Warnings);
        }
        else if (TryGetInt(elem, out var jobInt, "jobType", "jobtype", "job_type"))
        {
            if (Enum.IsDefined(typeof(MinorTaskJobType), jobInt))
            {
                minor.ResolvedJobType = (MinorTaskJobType)jobInt;
            }
        }

        // 7. State (Optional, defaults to Todo)
        if (TryGetString(elem, out var stateStr, "state", "status"))
        {
            minor.State = stateStr;
            minor.ResolvedState = ParseMinorTaskState(stateStr, minor.Title, index, result.Warnings);
        }
        else
        {
            minor.ResolvedState = MinorTaskState.Todo;
        }

        return minor;
    }

    private static Priority ParsePriority(string? input, string taskTitle, int index, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(input)) return Priority.Medium;
        var normalized = input.Trim().Replace(" ", string.Empty).ToLowerInvariant();
        return normalized switch
        {
            "low" => Priority.Low,
            "medium" or "normal" or "med" => Priority.Medium,
            "high" => Priority.High,
            "critical" or "urgent" or "blocker" => Priority.Critical,
            _ => WarnAndDefaultPriority(input, taskTitle, warnings)
        };
    }

    private static Priority WarnAndDefaultPriority(string input, string taskTitle, List<string> warnings)
    {
        warnings.Add($"Task '{taskTitle}': Unknown priority '{input}'. Defaulted to Medium.");
        return Priority.Medium;
    }

    private static MajorTaskState ParseMajorTaskState(string? input, string taskTitle, int index, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(input)) return MajorTaskState.Todo;
        var normalized = input.Trim().Replace(" ", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
        return normalized switch
        {
            "todo" or "backlog" => MajorTaskState.Todo,
            "inprogress" or "active" or "doing" => MajorTaskState.InProgress,
            "review" or "inreview" or "testing" => MajorTaskState.Review,
            "done" or "completed" or "finished" => MajorTaskState.Done,
            "onhold" or "blocked" or "paused" => MajorTaskState.OnHold,
            _ => WarnAndDefaultMajorState(input, taskTitle, warnings)
        };
    }

    private static MajorTaskState WarnAndDefaultMajorState(string input, string taskTitle, List<string> warnings)
    {
        warnings.Add($"Task '{taskTitle}': Unknown state '{input}'. Defaulted to Todo.");
        return MajorTaskState.Todo;
    }

    private static MinorTaskState ParseMinorTaskState(string? input, string taskTitle, int index, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(input)) return MinorTaskState.Todo;
        var normalized = input.Trim().Replace(" ", string.Empty).ToLowerInvariant();
        return normalized switch
        {
            "todo" or "pending" => MinorTaskState.Todo,
            "inprogress" or "doing" => MinorTaskState.InProgress,
            "done" or "completed" => MinorTaskState.Done,
            "canceled" or "cancelled" => MinorTaskState.Canceled,
            "failed" => MinorTaskState.Failed,
            _ => MinorTaskState.Todo
        };
    }

    private static MinorTaskJobType? ParseJobType(string? input, string taskTitle, int index, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var normalized = input.Trim().Replace(" ", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
        return normalized switch
        {
            "problemsolving" or "problem" => MinorTaskJobType.ProblemSolving,
            "solvebug" or "bug" or "bugfix" or "fix" => MinorTaskJobType.SolveBug,
            "review" or "codeview" or "codereview" => MinorTaskJobType.Review,
            "testing" or "test" or "qa" => MinorTaskJobType.Testing,
            "research" or "investigate" or "spike" => MinorTaskJobType.Research,
            "developing" or "dev" or "development" or "coding" or "feature" => MinorTaskJobType.Developing,
            "update" or "maintenance" or "refactor" => MinorTaskJobType.Update,
            _ => WarnAndDefaultJobType(input, taskTitle, warnings)
        };
    }

    private static MinorTaskJobType? WarnAndDefaultJobType(string input, string taskTitle, List<string> warnings)
    {
        warnings.Add($"Sub-task '{taskTitle}': Unknown JobType '{input}'. Left as None.");
        return null;
    }

    private static bool HasProperty(JsonElement elem, params string[] names)
    {
        return TryGetProperty(elem, out _, names);
    }

    private static bool TryGetProperty(JsonElement elem, out JsonElement value, params string[] names)
    {
        foreach (var prop in elem.EnumerateObject())
        {
            foreach (var name in names)
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static bool TryGetString(JsonElement elem, out string? value, params string[] names)
    {
        if (TryGetProperty(elem, out var prop, names))
        {
            if (prop.ValueKind == JsonValueKind.String)
            {
                value = prop.GetString();
                return true;
            }
            if (prop.ValueKind == JsonValueKind.Null)
            {
                value = null;
                return true;
            }
            if (prop.ValueKind == JsonValueKind.Number || prop.ValueKind == JsonValueKind.True || prop.ValueKind == JsonValueKind.False)
            {
                value = prop.ToString();
                return true;
            }
        }

        value = null;
        return false;
    }

    private static bool TryGetInt(JsonElement elem, out int value, params string[] names)
    {
        if (TryGetProperty(elem, out var prop, names))
        {
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out value))
            {
                return true;
            }
            if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out value))
            {
                return true;
            }
        }

        value = 0;
        return false;
    }
}
