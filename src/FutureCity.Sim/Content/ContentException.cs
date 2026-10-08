namespace FutureCity.Sim.Content;

/// <summary>Thrown when content files are missing, malformed or fail validation. Lists every problem found.</summary>
public sealed class ContentException : Exception
{
    /// <summary>Creates the exception from a list of problems.</summary>
    public ContentException(IReadOnlyList<string> errors)
        : base("Content failed to load:" + Environment.NewLine + string.Join(Environment.NewLine, errors.Select(e => "  - " + e)))
    {
        Errors = errors;
    }

    /// <summary>Every problem found, one per entry.</summary>
    public IReadOnlyList<string> Errors { get; }
}
