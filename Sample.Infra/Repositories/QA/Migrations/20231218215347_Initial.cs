using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sample.Infra.Repositories.QA.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sample");

            migrationBuilder.CreateTable(
                name: "questions",
                schema: "sample",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "text", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    posted_by = table.Column<int>(type: "integer", nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    date_posted = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_questions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tag",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tag", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "answer",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    body = table.Column<string>(type: "text", nullable: false),
                    posted_by = table.Column<int>(type: "integer", nullable: false),
                    question_id = table.Column<int>(type: "integer", nullable: false),
                    date_posted = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_accepted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_answer", x => x.id);
                    table.ForeignKey(
                        name: "fk_answer_questions_question_id",
                        column: x => x.question_id,
                        principalSchema: "sample",
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "comment",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    body = table.Column<string>(type: "text", nullable: false),
                    posted_by = table.Column<int>(type: "integer", nullable: false),
                    date_posted = table.Column<int>(type: "integer", nullable: false),
                    question_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_comment", x => x.id);
                    table.ForeignKey(
                        name: "fk_comment_questions_question_id",
                        column: x => x.question_id,
                        principalSchema: "sample",
                        principalTable: "questions",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "question_tag",
                columns: table => new
                {
                    posts_id = table.Column<int>(type: "integer", nullable: false),
                    tags_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_question_tag", x => new { x.posts_id, x.tags_id });
                    table.ForeignKey(
                        name: "fk_question_tag_questions_posts_id",
                        column: x => x.posts_id,
                        principalSchema: "sample",
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
                schema: "sample");

            migrationBuilder.DropTable(
                name: "tag");
        }
    }
}
