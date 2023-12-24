using Sample.Domain.QA.Questions;

namespace Sample.Domain.QA.Answers;

public class Answer : AggregateRoot
{
    private Answer()
    {
    }

    public Answer(string body, Question question, long postedBy)
    {
        Body = body;
        QuestionId = question.Id;
        PostedBy = postedBy;
    }

    public string Body { get; set; }
    public long QuestionId { get; private set; }
    public long PostedBy { get; private set; }
    public bool IsAccepted { get; set; }
}