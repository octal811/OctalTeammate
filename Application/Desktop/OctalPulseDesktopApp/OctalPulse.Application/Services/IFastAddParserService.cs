using OctalPulse.Application.Contracts;

namespace OctalPulse.Application.Services;

public interface IFastAddParserService
{
    FastAddValidationResult ParseAndValidate(string rawInput, FastAddScope? forcedScope = null);
    string StripMarkdownFences(string input);
    string FormatJson(string input);
}
