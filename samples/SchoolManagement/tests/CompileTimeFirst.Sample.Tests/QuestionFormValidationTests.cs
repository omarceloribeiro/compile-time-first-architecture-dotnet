using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using CompileTimeFirst.Sample.Domain;

namespace CompileTimeFirst.Sample.Tests;

public sealed class QuestionFormValidationTests
{
    [Fact]
    public void Form_reuses_nested_option_contract_validation_before_submit()
    {
        var componentType = typeof(BlazorServerEntryPoint).Assembly.GetType(
            "CompileTimeFirst.Sample.BlazorServer.Components.Pages.Questions.Questions",
            throwOnError: true)!;
        var formType = componentType.GetNestedType("QuestionForm", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("QuestionForm was not found in the Questions component.");
        var optionType = componentType.GetNestedType("OptionEditor", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("OptionEditor was not found in the Questions component.");
        var form = Activator.CreateInstance(formType, nonPublic: true)
            ?? throw new InvalidOperationException("QuestionForm could not be created.");

        formType.GetProperty("Statement")!.SetValue(form, "Question");
        formType.GetProperty("SubjectId")!.SetValue(form, Guid.NewGuid());
        formType.GetProperty("GradeId")!.SetValue(form, Guid.NewGuid());
        formType.GetProperty("Type")!.SetValue(form, QuestionType.SingleChoice);

        var options = (IList)formType.GetProperty("Options")!.GetValue(form)!;
        options.Add(CreateOption(optionType, " ", isCorrect: true, order: 1));
        options.Add(CreateOption(optionType, "Other", isCorrect: false, order: 2));

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(
            form,
            new ValidationContext(form),
            results,
            validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, result =>
            string.Equals(result.ErrorMessage, "Option text is required.", StringComparison.Ordinal));
    }

    private static object CreateOption(Type optionType, string text, bool isCorrect, int order) =>
        Activator.CreateInstance(
            optionType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [text, isCorrect, order],
            culture: null)
        ?? throw new InvalidOperationException("OptionEditor could not be created.");
}
