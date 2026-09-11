using CompileTimeFirst.Sample.Domain;

namespace CompileTimeFirst.Sample.Application.Questions;

public sealed record QuestionOptionDraft(string Text, bool IsCorrect, int Order);

/// <summary>
/// The shape a question of each type must have.
///
/// This is the single source for both directions of the same rule: the editor builds defaults from
/// it, and the write use case validates against it. Keeping only the rejecting half in the use case
/// would leave the constructing half stranded in a screen - and screens are disposable, so that
/// knowledge would be lost the moment the screen was rewritten.
/// </summary>
public static class QuestionShape
{
    public const int MinObjectiveOptions = 2;
    public const int MaxOptions = 100;
    public const int MaxStatementLength = 4_000;
    public const int MaxOptionTextLength = 1_000;
    public const string TrueOptionText = "True";
    public const string FalseOptionText = "False";

    public static bool UsesOptions(QuestionType type) => type != QuestionType.OpenText;

    public static bool AllowsCustomOptions(QuestionType type) =>
        type is QuestionType.SingleChoice or QuestionType.MultipleChoice;

    public static bool RequiresExactlyOneCorrectOption(QuestionType type) =>
        type is QuestionType.SingleChoice or QuestionType.TrueOrFalse;

    public static IReadOnlyList<QuestionOptionDraft> DefaultOptions(QuestionType type) => type switch
    {
        QuestionType.OpenText => [],
        QuestionType.TrueOrFalse =>
        [
            new QuestionOptionDraft(TrueOptionText, true, 1),
            new QuestionOptionDraft(FalseOptionText, false, 2)
        ],
        _ =>
        [
            new QuestionOptionDraft(string.Empty, true, 1),
            new QuestionOptionDraft(string.Empty, false, 2)
        ]
    };

    public static bool MatchesTrueOrFalseShape(IReadOnlyList<QuestionOptionDraft> orderedOptions)
    {
        ArgumentNullException.ThrowIfNull(orderedOptions);

        return orderedOptions.Count == 2 &&
            orderedOptions[0].Order == 1 &&
            orderedOptions[0].Text.Trim().Equals(TrueOptionText, StringComparison.OrdinalIgnoreCase) &&
            orderedOptions[1].Order == 2 &&
            orderedOptions[1].Text.Trim().Equals(FalseOptionText, StringComparison.OrdinalIgnoreCase);
    }
}
