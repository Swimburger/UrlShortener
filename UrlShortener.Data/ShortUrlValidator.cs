using System.Text.RegularExpressions;

namespace UrlShortener.Data;

public static class ShortUrlValidator
{
    private static readonly Regex PathRegex = new Regex(
        "^[a-zA-Z0-9_]*$",
        RegexOptions.None,
        TimeSpan.FromMilliseconds(1)
    );

    public static bool Validate(this ShortUrl shortUrl, out IDictionary<string, string[]> validationResults)
    {
        validationResults = new Dictionary<string, string[]>();
        var isDestinationValid = ValidateDestination(
            shortUrl.Destination,
            out var destinationValidationResults
        );
        var isPathValid = ValidatePath(
            shortUrl.Path,
            out var pathValidationResults
        );

        validationResults.Add("destination", destinationValidationResults);
        validationResults.Add("path", pathValidationResults);

        return isDestinationValid && isPathValid;
    }

    public static bool ValidateDestination(string? destination, out string[] validationResults)
    {
        if (destination == null)
        {
            validationResults = new[] {"Destination cannot be null."};
            return false;
        }

        if (destination == "")
        {
            validationResults = new[] {"Destination cannot empty."};
            return false;
        }

        if (!Uri.IsWellFormedUriString(destination, UriKind.Absolute))
        {
            validationResults = new[] {"Destination has to be a valid absolute URL."};
            return false;
        }

        validationResults = Array.Empty<string>();
        return true;
    }

    public static bool ValidatePath(string? path, out string[] validationResults)
    {
        if (path == null)
        {
            validationResults = new[] {"Path cannot be null."};
            return false;
        }

        if (path == "")
        {
            validationResults = new[] {"Path cannot empty."};
            return false;
        }

        var validationResultsList = new List<string>();
        if (path.Length > 10)
            validationResultsList.Add("Path cannot be longer than 10 characters.");

        if (!PathRegex.IsMatch(path))
            validationResultsList.Add("Path can only contain alphanumeric characters and underscores.");
        
        validationResults = validationResultsList.ToArray();
        return validationResultsList.Count > 0;
    }
}