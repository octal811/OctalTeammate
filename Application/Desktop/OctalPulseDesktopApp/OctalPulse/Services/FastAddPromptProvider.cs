namespace OctalPulse.Services;

public static class FastAddPromptProvider
{
    public static string GetMajorTasksPrompt()
    {
        return """
You are a technical project planner. Based on the plan or feature we discussed, generate a structured list of Major Tasks (and optional child Minor Tasks) for my project management system.

IMPORTANT RULES FOR THE JSON OUTPUT:
1. Output ONLY a valid JSON array of Major Task objects. Do NOT include markdown code fences, greetings, or explanations.
2. Mandatory fields: "title" is required for each task.
3. Optional/Nullable fields: You can set "description", "details", "externalLink", "dueDate" to null if unknown. For example: "externalLink": null.
4. Valid "priority" values: "Low", "Medium", "High", "Critical" (default is "Medium").
5. Valid "state" values: "Todo", "InProgress", "Review", "Done", "OnHold" (default is "Todo").
6. Optional "minorTasks" array: Each major task can have child subtasks with:
   - "title" (required)
   - "description" (optional/null)
   - "target" (acceptance criteria, optional/null)
   - "jobType": "Developing", "SolveBug", "Review", "Testing", "Research", "ProblemSolving", "Update" (or null)
   - "notes" (optional/null)
   - "externalLink": null

JSON SCHEMA EXAMPLE:
[
  {
    "title": "Implement User Authentication & Session Management",
    "description": "Provide secure JWT login, token refresh, and profile management.",
    "details": "Follow RFC 7519 for JWT handling and secure storage in DPAPI.",
    "externalLink": null,
    "state": "Todo",
    "priority": "High",
    "dueDate": "2026-10-15",
    "minorTasks": [
      {
        "title": "Create Login and Register XAML forms",
        "description": "Responsive layout with validation and dark theme support.",
        "target": "Pixel-perfect UI with accessible keyboard navigation",
        "jobType": "Developing",
        "notes": null,
        "externalLink": null
      },
      {
        "title": "Write unit tests for AuthDelegatingHandler",
        "description": "Test automatic token refresh upon 401 response.",
        "target": "100% test coverage on auth handler",
        "jobType": "Testing",
        "notes": null,
        "externalLink": null
      }
    ]
  },
  {
    "title": "Configure CI/CD Build & Release Pipeline",
    "description": "Automate desktop builds and test execution on GitHub Actions.",
    "details": null,
    "externalLink": null,
    "state": "Todo",
    "priority": "Medium",
    "dueDate": null,
    "minorTasks": []
  }
]
""";
    }

    public static string GetMinorTasksPrompt()
    {
        return """
You are a technical project planner. Break down the task or feature we discussed into a detailed checklist of Minor Tasks (Sub-tasks) for my project management system.

IMPORTANT RULES FOR THE JSON OUTPUT:
1. Output ONLY a valid JSON array of Minor Task objects. Do NOT include markdown code fences, greetings, or explanations.
2. Mandatory fields: "title" is required.
3. Optional/Nullable fields: You can set "description", "target", "notes", "externalLink" to null if unknown. For example: "externalLink": null.
4. Valid "jobType" values: "Developing", "SolveBug", "Review", "Testing", "Research", "ProblemSolving", "Update" (or null).
5. Valid "state" values: "Todo", "InProgress", "Done", "Canceled", "Failed" (default is "Todo").

JSON SCHEMA EXAMPLE:
[
  {
    "title": "Design XAML layout and control styles",
    "description": "Modern dark and light mode themes with smooth transitions.",
    "target": "Matches design system tokens",
    "jobType": "Developing",
    "notes": null,
    "externalLink": null
  },
  {
    "title": "Implement ViewModel with validation and relay commands",
    "description": "Support MVVM pattern with CommunityToolkit.",
    "target": "All commands have active CanExecute validation",
    "jobType": "Developing",
    "notes": null,
    "externalLink": null
  },
  {
    "title": "Execute end-to-end user acceptance testing",
    "description": "Verify task creation, cancellation, and API feedback.",
    "target": "Zero crashes and smooth UX",
    "jobType": "Testing",
    "notes": "Test on both light and dark themes",
    "externalLink": null
  }
]
""";
    }

    public static string GetSampleMajorTasksJson()
    {
        return """
[
  {
    "title": "Build Fast Add AI Task Importer",
    "description": "Allow users to batch-import Major and Minor tasks from external AI plans.",
    "details": "Includes lenient JSON parser, pre-flight validation, and 1-by-1 execution screen.",
    "externalLink": null,
    "state": "InProgress",
    "priority": "Critical",
    "dueDate": "2026-10-01",
    "minorTasks": [
      {
        "title": "Implement IFastAddParserService with lenient JSON rules",
        "description": "Strip markdown fences, validate enums, and handle null fields gracefully.",
        "target": "Handles direct arrays and wrapped JSON objects",
        "jobType": "Developing",
        "notes": null,
        "externalLink": null
      },
      {
        "title": "Build FastAddWindow UI with live progress indicators",
        "description": "Display sequential 1-by-1 task creation status with retry capability.",
        "target": "Modern WPF UI with dark and light theme support",
        "jobType": "Developing",
        "notes": "Include ready-to-copy AI prompt template",
        "externalLink": null
      },
      {
        "title": "Integrate Fast Add buttons in Tasks Board and Major Task Details",
        "description": "Wire commands to trigger Fast Add dialog with preselected scopes.",
        "target": "Seamless UX across board and detail views",
        "jobType": "Developing",
        "notes": null,
        "externalLink": null
      }
    ]
  },
  {
    "title": "Real-time Notification Enhancement",
    "description": "Broadcast task creation events to all connected clients via SignalR.",
    "details": null,
    "externalLink": null,
    "state": "Todo",
    "priority": "High",
    "dueDate": "2026-10-15",
    "minorTasks": [
      {
        "title": "Verify SignalR hub group subscription on track join",
        "description": "Ensure task counters update automatically without manual refresh.",
        "target": "Sub-second update latency",
        "jobType": "Testing",
        "notes": null,
        "externalLink": null
      }
    ]
  }
]
""";
    }

    public static string GetSampleMinorTasksJson()
    {
        return """
[
  {
    "title": "Analyze edge cases in JSON input",
    "description": "Handle trailing commas, single quotes, and missing optional properties.",
    "target": "Robust error handling with clear user warnings",
    "jobType": "Research",
    "notes": null,
    "externalLink": null
  },
  {
    "title": "Implement live progress row animation",
    "description": "Show spinning indicator for currently running item and checkmark when done.",
    "target": "Fluid visual response during batch operations",
    "jobType": "Developing",
    "notes": null,
    "externalLink": null
  },
  {
    "title": "Verify error recovery and failed item retry",
    "description": "Ensure failed API calls do not abort the entire queue and allow re-trying.",
    "target": "Individual retry for failed items",
    "jobType": "Testing",
    "notes": "Simulate network timeout or 400 bad request",
    "externalLink": null
  }
]
""";
    }
}
