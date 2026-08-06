using CompileTimeFirst.Sample.ReadModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace CompileTimeFirst.Sample.Web.OData;

public sealed class SubjectsController(SchoolODataReadScope db) : ODataController
{
    [EnableQuery(PageSize = 100, MaxTop = 100, MaxExpansionDepth = 0)]
    public IQueryable<SubjectReadItem> Get() => db.Subjects;
}

public sealed class GradesController(SchoolODataReadScope db) : ODataController
{
    [EnableQuery(PageSize = 100, MaxTop = 100, MaxExpansionDepth = 0)]
    public IQueryable<GradeReadItem> Get() => db.Grades;
}

public sealed class QuestionsController(SchoolODataReadScope db) : ODataController
{
    [EnableQuery(PageSize = 100, MaxTop = 100, MaxExpansionDepth = 0)]
    public IQueryable<QuestionReadItem> Get() => db.Questions;
}

public sealed class QuestionOptionsController(SchoolODataReadScope db) : ODataController
{
    [EnableQuery(PageSize = 100, MaxTop = 100, MaxExpansionDepth = 0)]
    public IQueryable<QuestionOptionReadItem> Get() => db.QuestionOptions;
}
