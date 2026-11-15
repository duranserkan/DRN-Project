using DRN.Framework.EntityFramework.Context;
using DRN.Framework.SharedKernel.Domain;
using DRN.Nexus.Domain.User;
using DRN.Nexus.Infra.Identity;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Sample.Domain;
using Sample.Domain.QA.Authors;
using Sample.Domain.QA.Questions;
using Sample.Domain.Users;
using Sample.Infra.DataProtection;
using Sample.Infra.Identity;
using Sample.Infra.QA;

namespace DRN.Test.Unit.Tests.Framework.EntityFramework;

public sealed class SourceKnownNormalizationModelTests
{
    [Theory]
    [InlineData("QA")]
    [InlineData("SampleIdentity")]
    [InlineData("NexusIdentity")]
    [InlineData("DataProtection")]
    public void Existing_Snapshot_And_Current_Model_Must_Have_Equal_Relational_Identities_Before_Normalization(string contextName)
    {
        using var context = CreateContext(contextName);
        var (snapshot, current) = Models(context);
        // These assertions characterize the existing baseline. Existing drift must be reviewed before source moves.
        RelationalIdentities(current.GetRelationalModel()).Should().Equal(RelationalIdentities(snapshot.GetRelationalModel()),
            "normalization requires unchanged schemas, tables, columns, keys, indexes and foreign keys");
    }

    [Theory]
    [InlineData("QA")]
    [InlineData("SampleIdentity")]
    [InlineData("NexusIdentity")]
    [InlineData("DataProtection")]
    public void Existing_Snapshot_Should_Require_No_Migration_Operations_Before_Normalization(string contextName)
    {
        using var context = CreateContext(contextName);
        var (snapshot, current) = Models(context);
        var differences = context.GetService<IMigrationsModelDiffer>().GetDifferences(
            snapshot.GetRelationalModel(), current.GetRelationalModel());
        differences.Should().BeEmpty("any pre-existing snapshot drift needs a separate reviewed reconciliation before namespace normalization");
    }

    [Theory]
    [InlineData("QA", "Sample.Infra.QA.QAContext", "Sample.Infra.QA.Migrations.QAContextModelSnapshot", "qa_context", "qa_context_history")]
    [InlineData("SampleIdentity", "Sample.Infra.Identity.SampleIdentityContext", "Sample.Infra.Identity.Migrations.SampleIdentityContextModelSnapshot", "sample_identity_context", "sample_identity_context_history")]
    [InlineData("NexusIdentity", "DRN.Nexus.Infra.Identity.NexusIdentityContext", "DRN.Nexus.Infra.Identity.Migrations.NexusIdentityContextModelSnapshot", "nexus_identity_context", "nexus_identity_context_history")]
    [InlineData("DataProtection", "Sample.Infra.DataProtection.DrnDataProtectionContext", "Sample.Infra.DataProtection.Migrations.DrnDataProtectionContextModelSnapshot", "__drn_data_protection_context", "drn_data_protection_context_history")]
    public void Context_Snapshot_Schema_And_History_Identities_Should_Remain_Stable(string selection, string contextName,
        string snapshotName, string schema, string historyTable)
    {
        using var context = CreateContext(selection);
        context.GetType().FullName.Should().Be(contextName);
        var assembly = context.GetService<IMigrationsAssembly>();
        assembly.Assembly.Should().BeSameAs(context.GetType().Assembly);
        var snapshot = assembly.ModelSnapshot;
        snapshot.Should().NotBeNull();
        snapshot!.GetType().FullName.Should().Be(snapshotName);
        snapshot.GetType().GetCustomAttribute<DbContextAttribute>()!.ContextType.Should().Be(context.GetType());
        snapshot.Model.GetDefaultSchema().Should().Be(schema);
        context.GetService<IDesignTimeModel>().Model.GetDefaultSchema().Should().Be(schema);
        var options = RelationalOptionsExtension.Extract(context.GetService<IDbContextOptions>());
        options.MigrationsHistoryTableName.Should().Be(historyTable);
        options.MigrationsHistoryTableSchema.Should().Be("__entity_migrations");
        options.Connection.Should().BeNull();
        options.ConnectionString.Should().BeNull();
    }

    [Fact]
    public void Qa_User_And_Author_Should_Keep_Distinct_Entity_Ids_And_Authentication_Users_Should_Stay_Separate()
    {
        ((byte)SampleEntityTypes.User).Should().Be(7);
        ((byte)SampleEntityTypes.Author).Should().Be(8);
        var userId = SourceKnownEntity.GetEntityTypeId<User>();
        var authorId = SourceKnownEntity.GetEntityTypeId<Author>();
        userId.EntityType.Should().Be(7);
        authorId.EntityType.Should().Be(8);
        userId.AppId.Should().Be(0);
        authorId.AppId.Should().Be(0);
        userId.Should().NotBe(authorId);
        typeof(User).IsAssignableTo(typeof(AggregateRoot)).Should().BeTrue();
        typeof(Author).IsAssignableTo(typeof(AggregateRoot)).Should().BeTrue();
        typeof(User).IsAssignableTo(typeof(IdentityUser)).Should().BeFalse();
        typeof(SampleUser).IsAssignableTo(typeof(IdentityUser)).Should().BeTrue();
        typeof(NexusUser).IsAssignableTo(typeof(IdentityUser)).Should().BeTrue();
        typeof(SampleUser).IsAssignableTo(typeof(SourceKnownEntity)).Should().BeFalse();
        typeof(NexusUser).IsAssignableTo(typeof(SourceKnownEntity)).Should().BeFalse();
        typeof(User).GetProperty(nameof(User.Id))!.PropertyType.Should().Be(typeof(long));
        typeof(SampleUser).GetProperty(nameof(SampleUser.Id))!.PropertyType.Should().Be(typeof(string));
        typeof(NexusUser).GetProperty(nameof(NexusUser.Id))!.PropertyType.Should().Be(typeof(string));
    }

    [Fact]
    public void Qa_Relationship_Should_Target_Domain_User_Without_Inferring_An_Authentication_Link()
    {
        using var context = new QAContext(Options<QAContext>());
        var model = context.GetService<IDesignTimeModel>().Model;
        var user = model.FindEntityType(typeof(User));
        user.Should().NotBeNull();
        user!.GetTableName().Should().Be("users");
        user.GetSchema().Should().Be("qa_context");
        user.FindPrimaryKey()!.Properties.Should().ContainSingle().Which.ClrType.Should().Be(typeof(long));
        var question = model.FindEntityType(typeof(Question))!;
        var relationship = question.GetForeignKeys().Single(key => key.Properties.Any(property => property.Name == nameof(Question.UserId)));
        relationship.PrincipalEntityType.Should().BeSameAs(user);
        relationship.PrincipalKey.Properties.Should().ContainSingle().Which.Name.Should().Be(nameof(User.Id));
        model.FindEntityType(typeof(SampleUser)).Should().BeNull();
        model.FindEntityType(typeof(NexusUser)).Should().BeNull();
        context.GetService<IMigrationsAssembly>().ModelSnapshot!.Model.FindEntityType("Sample.Domain.Users.User").Should().NotBeNull();
    }

    [Theory]
    [InlineData("SampleIdentity", typeof(SampleUser), "Sample.Domain.Users.SampleUser")]
    [InlineData("NexusIdentity", typeof(NexusUser), "DRN.Nexus.Domain.User.NexusUser")]
    public void Authentication_Models_Should_Preserve_String_Keys_And_Identity_Tables(string selection, Type userType, string historicalName)
    {
        using var context = CreateContext(selection);
        var model = context.GetService<IDesignTimeModel>().Model;
        var user = model.FindEntityType(userType)!;
        user.GetTableName().Should().Be("users");
        user.FindPrimaryKey()!.Properties.Should().ContainSingle().Which.ClrType.Should().Be(typeof(string));
        model.FindEntityType(typeof(User)).Should().BeNull();
        model.FindEntityType(typeof(Author)).Should().BeNull();
        model.GetRelationalModel().Tables.Select(table => table.Name).Should().Contain(
            ["users", "user_tokens", "user_logins", "user_claims", "roles", "user_roles", "role_claims"]);
        context.GetService<IMigrationsAssembly>().ModelSnapshot!.Model.FindEntityType(historicalName).Should().NotBeNull();
    }

    [Fact]
    public void DataProtection_Model_Should_Preserve_Real_Key_Context_And_Storage_Identity()
    {
        using var context = new DrnDataProtectionContext(Options<DrnDataProtectionContext>());
        context.Should().BeAssignableTo<IDataProtectionKeyContext>();
        var model = context.GetService<IDesignTimeModel>().Model;
        var key = model.FindEntityType(typeof(DataProtectionKey))!;
        key.GetSchema().Should().Be("__drn_data_protection_context");
        key.GetTableName().Should().Be("data_protection_keys");
        key.FindPrimaryKey()!.Properties.Should().ContainSingle().Which.ClrType.Should().Be(typeof(int));
        key.FindProperty(nameof(DataProtectionKey.Xml)).Should().NotBeNull();
    }

    private static DbContext CreateContext(string selection) => selection switch
    {
        "QA" => new QAContext(Options<QAContext>()),
        "SampleIdentity" => new SampleIdentityContext(Options<SampleIdentityContext>()),
        "NexusIdentity" => new NexusIdentityContext(Options<NexusIdentityContext>()),
        "DataProtection" => new DrnDataProtectionContext(Options<DrnDataProtectionContext>()),
        _ => throw new ArgumentOutOfRangeException(nameof(selection))
    };

    private static DbContextOptions<TContext> Options<TContext>() where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        // The provider is configured for metadata only, without credentials, connection or application services.
        DbContextConventions.UpdateDbContextOptionsBuilder<TContext>(builder);
        return builder.Options;
    }

    private static (IModel Snapshot, IModel Current) Models(DbContext context)
    {
        var snapshot = context.GetService<IMigrationsAssembly>().ModelSnapshot;
        snapshot.Should().NotBeNull("the existing snapshot is the required pre-move comparison baseline");
        var snapshotModel = snapshot!.Model;
        if (snapshotModel is IMutableModel mutable) snapshotModel = mutable.FinalizeModel();
        var initialized = context.GetService<IModelRuntimeInitializer>().Initialize(snapshotModel, designTime: true, validationLogger: null);
        return (initialized, context.GetService<IDesignTimeModel>().Model);
    }

    private static string[] RelationalIdentities(IRelationalModel model)
    {
        var identities = new List<string>();
        foreach (var table in model.Tables)
        {
            var name = (table.Schema ?? "<default>") + "." + table.Name;
            identities.Add("table:" + name);
            foreach (var column in table.Columns)
                identities.Add($"column:{name}.{column.Name}|{column.StoreType}|nullable={column.IsNullable}");
            foreach (var key in table.UniqueConstraints)
                identities.Add($"key:{name}.{key.Name}|primary={key.GetIsPrimaryKey()}|{string.Join(",", key.Columns.Select(column => column.Name))}");
            foreach (var index in table.Indexes)
                identities.Add($"index:{name}.{index.Name}|unique={index.IsUnique}|{string.Join(",", index.Columns.Select(column => column.Name))}");
            foreach (var foreignKey in table.ForeignKeyConstraints)
                identities.Add($"fk:{name}.{foreignKey.Name}|{string.Join(",", foreignKey.Columns.Select(column => column.Name))}|" +
                    $"{foreignKey.PrincipalTable.Schema ?? "<default>"}.{foreignKey.PrincipalTable.Name}|" +
                    $"{string.Join(",", foreignKey.PrincipalColumns.Select(column => column.Name))}|{foreignKey.OnDeleteAction}");
        }
        return identities.Order(StringComparer.Ordinal).ToArray();
    }
}
