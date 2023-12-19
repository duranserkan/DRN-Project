namespace Sample.Domain.QA.Answers;

public class Answer
{
    public Answer(string body)
    {
        Body = body;
    }

    public int Id { get; set; }
    public string Body { get; set; }
    public int PostedBy { get; set; }
    public int QuestionId { get; set; }
    public DateTimeOffset DatePosted { get; set; }
    public bool IsAccepted { get; set; }
}