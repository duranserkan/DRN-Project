namespace Sample.Domain.QA.Comments;

public class Comment : AggregateRoot
{
    private Comment()
    {
    }

    public Comment(string body, long postedBy)
    {
        Body = body;
        PostedBy = postedBy;
    }

    public string Body { get; set; }
    public long PostedBy { get; set; }
}