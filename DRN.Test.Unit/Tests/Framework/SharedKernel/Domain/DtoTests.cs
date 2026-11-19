using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Testing.Extensions;
using Sample.Domain.QA.Answer;
using Sample.Domain.QA.AnswerComment;
using Sample.Domain.QA.Author;
using Sample.Domain.QA.Category;
using Sample.Domain.QA.Question;
using Sample.Domain.QA.QuestionComment;
using Sample.Domain.QA.User;
using Sample.Domain.QB.QBTest;
using Tag = Sample.Domain.QA.Tag.TagEntity;

namespace DRN.Test.Unit.Tests.Framework.SharedKernel.Domain;

public class DtoTests
{
    [Fact]
    public void Profile_Entities_Should_Expose_Only_Inherited_Dto_Properties()
    {
        var author = new AuthorEntity("Author");
        var user = new UserEntity("Given", "Family", "Alias", new ContactDetail("sample@example.test"),
            new Address("Street", "City", "Country", "Postcode"));
        VerifyMetadata(author, author.ToDto());
        VerifyMetadata(user, user.ToDto());

        static void VerifyMetadata(SourceKnownEntity entity, Dto dto)
        {
            dto.GetType().GetProperties().Should().OnlyContain(property => property.DeclaringType == typeof(Dto));
            dto.Id.Should().Be(entity.EntityId);
            dto.CreatedAt.Should().Be(entity.CreatedAt);
            dto.ModifiedAt.Should().Be(entity.ModifiedAt);
        }
    }

    [Fact]
    public void Qa_Projections_Should_Copy_Their_Selected_Properties()
    {
        var user = new UserEntity("Given", "Family", "Alias", new ContactDetail("sample@example.test"),
            new Address("Street", "City", "Country", "Postcode"));
        var category = new CategoryEntity("Category");
        var question = new QuestionEntity("Title", "Question body", user, category);
        var answer = new AnswerEntity("Answer body", question, user) { IsAccepted = true };

        category.ToDto().Name.Should().Be(category.Name);
        var questionDto = question.ToDto();
        questionDto.Title.Should().Be(question.Title);
        questionDto.Body.Should().Be(question.Body);
        var answerDto = answer.ToDto();
        answerDto.IsAccepted.Should().BeTrue();
        answerDto.Body.Should().Be(answer.Body);
        new QuestionCommentEntity("Question comment", user).ToDto().Body.Should().Be("Question comment");
        new AnswerCommentEntity("Answer comment", answer, user).ToDto().Body.Should().Be("Answer comment");
    }

    [Fact]
    public void Qb_Projection_Should_Copy_Selected_Values()
    {
        var entity = new QBTestEntity { TestValue = 42, TestValueString = "selected" };
        var dto = entity.ToDto();
        dto.TestValue.Should().Be(entity.TestValue);
        dto.TestValueString.Should().Be(entity.TestValueString);
    }

    [Fact]
    public void Dto_Should_Serialize_And_Deserialize()
    {
        var tag = new Tag(nameof(Dto_Should_Serialize_And_Deserialize));
        var dto = new Dto(tag);
        var tagDto = tag.ToDto();
        tagDto.Name.Should().Be(tag.Name);
        tagDto.Model.Should().Be(tag.Model);
        
        dto.ValidateObjectSerialization();
        tagDto.ValidateObjectSerialization();
    }
}
