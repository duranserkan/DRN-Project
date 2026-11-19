using DRN.Framework.SharedKernel.Domain;

namespace Sample.Contract.QA.Users;

public class UserDto(SourceKnownEntity? entity = null) : Dto(entity);
