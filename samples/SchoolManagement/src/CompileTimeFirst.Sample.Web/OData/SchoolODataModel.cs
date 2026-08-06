using CompileTimeFirst.Sample.ReadModel;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;

namespace CompileTimeFirst.Sample.Web.OData;

public static class SchoolODataModel
{
    public static IEdmModel Create()
    {
        var builder = new ODataConventionModelBuilder();
        builder.EntitySet<SubjectReadItem>("Subjects");
        builder.EntitySet<GradeReadItem>("Grades");
        builder.EntitySet<QuestionReadItem>("Questions");
        builder.EntitySet<QuestionOptionReadItem>("QuestionOptions");
        return builder.GetEdmModel();
    }
}
