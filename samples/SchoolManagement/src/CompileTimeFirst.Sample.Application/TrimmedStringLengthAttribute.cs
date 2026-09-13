using System.ComponentModel.DataAnnotations;

namespace CompileTimeFirst.Sample.Application;

/// <summary>Validates the maximum length after trimming leading and trailing whitespace.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class TrimmedStringLengthAttribute(int maximumLength) : ValidationAttribute
{
    public int MaximumLength { get; } = maximumLength > 0
        ? maximumLength
        : throw new ArgumentOutOfRangeException(nameof(maximumLength));

    public override bool IsValid(object? value) =>
        value is null || value is string text && text.Trim().Length <= MaximumLength;
}
