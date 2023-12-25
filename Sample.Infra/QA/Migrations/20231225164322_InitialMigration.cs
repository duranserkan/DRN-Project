using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sample.Infra.QA.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "qa");

            migrationBuilder.EnsureSchema(
                name: "user");

            migrationBuilder.CreateTable(
                name: "categories",
                schema: "qa",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tag",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tag", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "user",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    surname = table.Column<string>(type: "text", nullable: false),
                    user_name = table.Column<string>(type: "text", nullable: false),
                    address_city = table.Column<string>(type: "text", nullable: false),
                    address_country = table.Column<string>(type: "text", nullable: false),
                    address_postcode = table.Column<string>(type: "text", nullable: false),
                    address_street = table.Column<string>(type: "text", nullable: false),
                    contact_email = table.Column<string>(type: "text", nullable: false),
                    contact_phone = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "questions",
                schema: "qa",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "text", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    category_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_questions", x => x.id);
                    table.ForeignKey(
                        name: "fk_questions_categories_category_id",
                        column: x => x.category_id,
                        principalSchema: "qa",
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_questions_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "user",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "answer",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    body = table.Column<string>(type: "text", nullable: false),
                    question_id = table.Column<long>(type: "bigint", nullable: false),
                    posted_by = table.Column<long>(type: "bigint", nullable: false),
                    is_accepted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_answer", x => x.id);
                    table.ForeignKey(
                        name: "fk_answer_questions_question_id",
                        column: x => x.question_id,
                        principalSchema: "qa",
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "comment",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    body = table.Column<string>(type: "text", nullable: false),
                    posted_by = table.Column<long>(type: "bigint", nullable: false),
                    question_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_comment", x => x.id);
                    table.ForeignKey(
                        name: "fk_comment_questions_question_id",
                        column: x => x.question_id,
                        principalSchema: "qa",
                        principalTable: "questions",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "question_tag",
                columns: table => new
                {
                    posts_id = table.Column<long>(type: "bigint", nullable: false),
                    tags_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_question_tag", x => new { x.posts_id, x.tags_id });
                    table.ForeignKey(
                        name: "fk_question_tag_questions_posts_id",
                        column: x => x.posts_id,
                        principalSchema: "qa",
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_question_tag_tag_tags_id",
                        column: x => x.tags_id,
                        principalTable: "tag",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_answer_question_id",
                table: "answer",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ix_comment_question_id",
                table: "comment",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ix_question_tag_tags_id",
                table: "question_tag",
                column: "tags_id");

            migrationBuilder.CreateIndex(
                name: "ix_questions_category_id",
                schema: "qa",
                table: "questions",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_questions_user_id",
                schema: "qa",
                table: "questions",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "answer");

            migrationBuilder.DropTable(
                name: "comment");

            migrationBuilder.DropTable(
                name: "question_tag");

            migrationBuilder.DropTable(
                name: "questions",
                schema: "qa");

            migrationBuilder.DropTable(
                name: "tag");

            migrationBuilder.DropTable(
                name: "categories",
                schema: "qa");

            migrationBuilder.DropTable(
                name: "users",
                schema: "user");
        }
    }
}
