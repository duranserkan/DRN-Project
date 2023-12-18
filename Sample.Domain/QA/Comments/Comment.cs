namespace Sample.Domain.QA.Comments;

public class Comment
{
    public int Id { get; set; }
    public string Body { get; set; }
    public int PostedBy { get; set; }
    public int DatePosted { get; set; }
}