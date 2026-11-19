using DRN.Framework.SharedKernel.Domain;

namespace Sample.Contract.QA.Authors;

public class AuthorDto(SourceKnownEntity? entity = null) : Dto(entity);
