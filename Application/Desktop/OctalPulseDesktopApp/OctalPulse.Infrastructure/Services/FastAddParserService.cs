using System.Text.Json;
using System.Text.RegularExpressions;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Domain.Entities;
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
                if (forcedScope == FastAddScope.Notes && TryGetProperty(root, out var notesProp, "notes", "userNotes", "usernotes", "personalNotes", "mynotes", "myNotes", "items", "data"))
                {
                    itemsArray = notesProp;
                }
                else if (TryGetProperty(root, out var prop, "majorTasks", "majortasks", "tasks", "cards", "items", "plan", "data"))
                {
                    itemsArray = prop;
                }
                else if (TryGetProperty(root, out var minorProp, "minorTasks", "minortasks", "subtasks", "checklist"))
                {
                    itemsArray = minorProp;
                    forcedScope ??= FastAddScope.MinorTasks;
                }
                else if (TryGetProperty(root, out var generalNotesProp, "notes", "userNotes", "usernotes", "personalNotes", "mynotes", "myNotes"))
                {
                    itemsArray = generalNotesProp;
                    forcedScope ??= FastAddScope.Notes;
                }
                else
                {
                    // Single object treated as single-item array
                    result.Errors.Add("Expected a JSON array of items or an object with 'tasks' / 'notes' property.");
                    result.IsValid = false;
                    return result;
                }
            }
            else
            {
                result.Errors.Add("JSON root must be an array of items or an object containing an items list.");
                result.IsValid = false;
                return result;
            }

            if (itemsArray.ValueKind != JsonValueKind.Array)
            {
                result.Errors.Add("The items collection found in JSON is not an array.");
                result.IsValid = false;
                return result;
            }

            var count = itemsArray.GetArrayLength();
            if (count == 0)
            {
                result.Errors.Add("The items array is empty. Please provide at least one item.");
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
            else if (detectedScope == FastAddScope.MinorTasks)
            {
                ParseMinorTasks(itemsArray, result);
            }
            else
            {
                ParseNotes(itemsArray, result);
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

            // If an element contains cardColor, listOfLinks, or noteType properties, it's Notes
            if (HasProperty(elem, "cardColor", "card_color", "cardcolor", "listOfLinks", "listoflinks", "relatedToProject", "related_to_project") ||
                (TryGetString(elem, out var typeVal, "type") && (typeVal != null && (typeVal.Equals("Reminder", StringComparison.OrdinalIgnoreCase) || typeVal.Equals("Research", StringComparison.OrdinalIgnoreCase)))))
            {
                return FastAddScope.Notes;
            }

            // If an element contains minorTasks or subtasks, or priority, it's major tasks
            if (HasProperty(elem, "minorTasks", "minortasks", "subtasks", "priority", "dueDate", "due_date"))
            {
                return FastAddScope.MajorTasks;
            }

            // If an element only has jobType or target or notes
            if (HasProperty(elem, "jobType", "job_type", "target") && !HasProperty(elem, "priority", "dueDate"))
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

    private static readonly HashSet<string> AllowedColorPalette = new(StringComparer.OrdinalIgnoreCase)
    {
        "#FFFFFF", // Pure White
        "#FEF08A", // Sunlight Yellow
        "#A7F3D0", // Mint Emerald
        "#BAE6FD", // Sky Ice
        "#DDD6FE", // Soft Lilac
        "#FECDD3", // Coral Rose
        "#FED7AA", // Warm Peach
        "#99F6E4", // Bright Aqua
        "#E2E8F0", // Slate Silver
        "#334155"  // Obsidian Navy
    };

    private static readonly Dictionary<string, string> ColorNameToHex = new(StringComparer.OrdinalIgnoreCase)
    {
        { "white", "#FFFFFF" },
        { "pure white", "#FFFFFF" },
        { "yellow", "#FEF08A" },
        { "sunlight yellow", "#FEF08A" },
        { "mint", "#A7F3D0" },
        { "emerald", "#A7F3D0" },
        { "mint emerald", "#A7F3D0" },
        { "green", "#A7F3D0" },
        { "sky", "#BAE6FD" },
        { "ice", "#BAE6FD" },
        { "sky ice", "#BAE6FD" },
        { "blue", "#BAE6FD" },
        { "lilac", "#DDD6FE" },
        { "soft lilac", "#DDD6FE" },
        { "purple", "#DDD6FE" },
        { "violet", "#DDD6FE" },
        { "coral", "#FECDD3" },
        { "rose", "#FECDD3" },
        { "coral rose", "#FECDD3" },
        { "pink", "#FECDD3" },
        { "peach", "#FED7AA" },
        { "warm peach", "#FED7AA" },
        { "orange", "#FED7AA" },
        { "aqua", "#99F6E4" },
        { "bright aqua", "#99F6E4" },
        { "cyan", "#99F6E4" },
        { "teal", "#99F6E4" },
        { "slate", "#E2E8F0" },
        { "silver", "#E2E8F0" },
        { "slate silver", "#E2E8F0" },
        { "gray", "#E2E8F0" },
        { "grey", "#E2E8F0" },
        { "obsidian", "#334155" },
        { "navy", "#334155" },
        { "obsidian navy", "#334155" },
        { "dark", "#334155" }
    };

    private static void ParseNotes(JsonElement array, FastAddValidationResult result)
    {
        var index = 0;
        foreach (var elem in array.EnumerateArray())
        {
            index++;
            if (elem.ValueKind != JsonValueKind.Object)
            {
                result.Errors.Add($"Item #{index} in JSON array is not a valid note object.");
                continue;
            }

            var note = new FastAddNoteDto();

            // 1. Title (Required)
            if (TryGetProperty(elem, out var titleProp, "title", "name", "noteTitle", "note_title", "header"))
            {
                if (titleProp.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(titleProp.GetString()))
                {
                    note.Title = titleProp.GetString()!.Trim();
                }
                else
                {
                    result.Errors.Add($"Note #{index} 'title' must be a non-empty text string.");
                }
            }
            else
            {
                result.Errors.Add($"Note #{index} is missing the required 'title' field.");
            }

            var noteDisplay = string.IsNullOrWhiteSpace(note.Title) ? $"#{index}" : $"'{note.Title}'";

            // 2. Description (Optional / Nullable)
            if (TryGetProperty(elem, out var descProp, "description", "desc", "content", "body", "text"))
            {
                if (descProp.ValueKind == JsonValueKind.String)
                {
                    note.Description = string.IsNullOrWhiteSpace(descProp.GetString()) ? null : descProp.GetString()!.Trim();
                }
                else if (descProp.ValueKind == JsonValueKind.Null)
                {
                    note.Description = null;
                }
                else
                {
                    note.Description = descProp.ToString();
                    result.Warnings.Add($"Note {noteDisplay}: 'description' was not a string ({descProp.ValueKind}). Converted to text.");
                }
            }

            // 3. Type (Optional, default Task)
            if (TryGetProperty(elem, out var typeProp, "type", "noteType", "category"))
            {
                if (typeProp.ValueKind == JsonValueKind.String)
                {
                    var typeStr = typeProp.GetString()?.Trim() ?? string.Empty;
                    note.Type = typeStr;
                    note.ResolvedType = ParseNoteType(typeStr, noteDisplay, result.Warnings);
                }
                else if (typeProp.ValueKind == JsonValueKind.Number && typeProp.TryGetInt32(out var typeInt))
                {
                    note.ResolvedType = typeInt switch
                    {
                        0 => NoteType.Reminder,
                        1 => NoteType.Task,
                        2 => NoteType.Research,
                        _ => NoteType.Task
                    };
                }
                else if (typeProp.ValueKind != JsonValueKind.Null)
                {
                    result.Warnings.Add($"Note {noteDisplay}: 'type' is not a valid text string (found {typeProp.ValueKind}). Defaulted to 'Task'.");
                    note.ResolvedType = NoteType.Task;
                }
            }
            else
            {
                note.ResolvedType = NoteType.Task;
            }

            // 4. CardColor (Optional, must be in available 10 colors palette)
            if (TryGetProperty(elem, out var colorProp, "cardColor", "cardcolor", "color", "card_color", "bg", "background"))
            {
                if (colorProp.ValueKind == JsonValueKind.String)
                {
                    var colorStr = colorProp.GetString()?.Trim();
                    note.CardColor = colorStr;
                    note.ResolvedCardColor = ValidateAndResolveColor(colorStr, noteDisplay, result.Warnings);
                }
                else if (colorProp.ValueKind != JsonValueKind.Null)
                {
                    result.Warnings.Add($"Note {noteDisplay}: 'cardColor' is not a text string (found {colorProp.ValueKind}). Fallback to Pure White (#FFFFFF) applied.");
                    note.ResolvedCardColor = "#FFFFFF";
                }
                else
                {
                    note.ResolvedCardColor = "#FFFFFF";
                }
            }
            else
            {
                note.ResolvedCardColor = "#FFFFFF";
            }

            // 5. DurationDate (Optional / Nullable)
            if (TryGetProperty(elem, out var dateProp, "durationDate", "durationdate", "date", "dueDate", "due_date", "reminderDate"))
            {
                if (dateProp.ValueKind == JsonValueKind.String)
                {
                    var dateStr = dateProp.GetString()?.Trim();
                    note.DurationDate = dateStr;
                    if (!string.IsNullOrWhiteSpace(dateStr))
                    {
                        if (DateTime.TryParse(dateStr, out var parsedDate))
                        {
                            note.ResolvedDurationDate = parsedDate;
                        }
                        else
                        {
                            result.Warnings.Add($"Note {noteDisplay}: Could not parse DurationDate '{dateStr}' (expected YYYY-MM-DD). Date omitted.");
                        }
                    }
                }
                else if (dateProp.ValueKind != JsonValueKind.Null)
                {
                    result.Warnings.Add($"Note {noteDisplay}: 'durationDate' is not a date string (found {dateProp.ValueKind}). Date omitted.");
                }
            }

            // 6. RelatedToProject (Optional / Nullable)
            if (TryGetProperty(elem, out var projProp, "relatedToProject", "project", "relatedProject", "projectName"))
            {
                if (projProp.ValueKind == JsonValueKind.String)
                {
                    var p = projProp.GetString()?.Trim();
                    note.RelatedToProject = string.IsNullOrWhiteSpace(p) ? null : p;
                }
                else if (projProp.ValueKind != JsonValueKind.Null)
                {
                    result.Warnings.Add($"Note {noteDisplay}: 'relatedToProject' is not a text string (found {projProp.ValueKind}). Project tag omitted.");
                }
            }

            // 7. ListOfLinks (Optional array of URL strings)
            if (TryGetProperty(elem, out var linksProp, "listOfLinks", "links", "urls", "linkList", "listOfLink"))
            {
                if (linksProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var linkElem in linksProp.EnumerateArray())
                    {
                        if (linkElem.ValueKind == JsonValueKind.String)
                        {
                            var link = linkElem.GetString()?.Trim();
                            if (!string.IsNullOrWhiteSpace(link))
                            {
                                if (!link.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                                    !link.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                                {
                                    link = "https://" + link;
                                }
                                if (!note.ListOfLinks.Contains(link))
                                    note.ListOfLinks.Add(link);
                            }
                        }
                        else
                        {
                            result.Warnings.Add($"Note {noteDisplay}: Link item was not a string ({linkElem.ValueKind}). Skipped.");
                        }
                    }
                }
                else if (linksProp.ValueKind == JsonValueKind.String)
                {
                    var singleLink = linksProp.GetString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(singleLink))
                    {
                        if (!singleLink.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                            !singleLink.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        {
                            singleLink = "https://" + singleLink;
                        }
                        note.ListOfLinks.Add(singleLink);
                    }
                }
                else if (linksProp.ValueKind != JsonValueKind.Null)
                {
                    result.Warnings.Add($"Note {noteDisplay}: 'listOfLinks' must be an array of URL strings (found {linksProp.ValueKind}).");
                }
            }

            // 8. Checked (Optional boolean)
            if (TryGetProperty(elem, out var checkedProp, "checked", "isDone", "isCompleted", "completed", "done"))
            {
                if (checkedProp.ValueKind == JsonValueKind.True)
                {
                    note.Checked = true;
                }
                else if (checkedProp.ValueKind == JsonValueKind.False)
                {
                    note.Checked = false;
                }
                else if (checkedProp.ValueKind == JsonValueKind.String && bool.TryParse(checkedProp.GetString(), out var boolVal))
                {
                    note.Checked = boolVal;
                }
                else if (checkedProp.ValueKind != JsonValueKind.Null)
                {
                    result.Warnings.Add($"Note {noteDisplay}: 'checked' was not a boolean value (found {checkedProp.ValueKind}). Defaulted to false.");
                }
            }

            result.Notes.Add(note);
        }
    }

    private static NoteType ParseNoteType(string typeStr, string noteDisplay, List<string> warnings)
    {
        var normalized = typeStr.Trim().ToLowerInvariant();
        return normalized switch
        {
            "task" or "todo" or "action" => NoteType.Task,
            "reminder" or "alert" or "notice" => NoteType.Reminder,
            "research" or "idea" or "reference" or "study" => NoteType.Research,
            _ => WarnAndDefaultNoteType(typeStr, noteDisplay, warnings)
        };
    }

    private static NoteType WarnAndDefaultNoteType(string input, string noteDisplay, List<string> warnings)
    {
        warnings.Add($"Note {noteDisplay}: Unknown note type '{input}'. Defaulted to 'Task' (valid types: Task, Reminder, Research).");
        return NoteType.Task;
    }

    private static string ValidateAndResolveColor(string? input, string noteDisplay, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "#FFFFFF";

        var trimmed = input.Trim();

        // 1. Direct hex match in allowed palette
        if (AllowedColorPalette.TryGetValue(trimmed, out var exactHex))
            return exactHex;

        // 2. Color name match in known palette names
        if (ColorNameToHex.TryGetValue(trimmed, out var mappedHex))
            return mappedHex;

        // 3. Invalid or unapproved color
        warnings.Add($"Note {noteDisplay}: Color '{input}' is not one of the 10 available palette colors. Fallback to Pure White (#FFFFFF) applied.");
        return "#FFFFFF";
    }
}
