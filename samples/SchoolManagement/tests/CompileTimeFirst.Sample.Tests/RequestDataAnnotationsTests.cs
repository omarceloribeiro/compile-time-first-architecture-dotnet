using System.ComponentModel.DataAnnotations;
using CompileTimeFirst.Sample.Application;
using CompileTimeFirst.Sample.Application.Grades;
using CompileTimeFirst.Sample.Application.Questions;
using CompileTimeFirst.Sample.Application.Subjects;
using StandaloneOptionRequest = CompileTimeFirst.Sample.Application.QuestionOptions.CreateQuestionOptionRequest;

namespace CompileTimeFirst.Sample.Tests;

public sealed class RequestDataAnnotationsTests
{
    [Fact]
    public void Normalized_length_accepts_padded_text_at_the_limit()
    {
        var request = new StandaloneOptionRequest(
            Guid.NewGuid(),
            $"  {new string('o', QuestionShape.MaxOptionTextLength)}  ",
            false,
            1);

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            results,
            validateAllProperties: true);

        Assert.True(isValid);
        Assert.Empty(results);
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public void Scalar_contracts_reject_invalid_values(object request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            results,
            validateAllProperties: true);

        Assert.False(isValid);
        Assert.NotEmpty(results);
    }

    public static TheoryData<object> InvalidRequests =>
        new()
        {
            new CreateSubjectRequest(" "),
            new CreateSubjectRequest(new string('s', 201)),
            new CreateGradeRequest(" ", 1),
            new CreateGradeRequest(new string('g', 101), 1),
            new CreateGradeRequest("Grade", 0),
            new CreateGradeRequest("Grade", 21),
            new StandaloneOptionRequest(Guid.NewGuid(), " ", false, 1),
            new StandaloneOptionRequest(
                Guid.NewGuid(),
                $"  {new string('o', QuestionShape.MaxOptionTextLength + 1)}  ",
                false,
                1),
            new StandaloneOptionRequest(Guid.NewGuid(), "Option", false, 0),
            new StandaloneOptionRequest(Guid.NewGuid(), "Option", false, 101)
        };
}
