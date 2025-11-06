using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Testing.Extensions;
using Sample.Contract.QA.Tags;
using Sample.Domain.QA.Tags;

namespace DRN.Test.Unit.Tests.Framework.SharedKernel.Domain;

public class DtoTests
{
    [Fact]
    public void Dto_Should_Serialize_And_Deserialize()
    {
        var tag = new Tag(nameof(Dto_Should_Serialize_And_Deserialize));
        var dto = new Dto(tag);
        var tagDto = new TagDto(tag)
        {
            Name = tag.Name,
            Model = tag.Model
        };
        
        dto.ValidateObjectSerialization();
        tagDto.ValidateObjectSerialization();
    }
}