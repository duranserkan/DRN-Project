using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sample.Infra.QA.Migrations
{
    /// <inheritdoc />
    public partial class SecondMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_answer_questions_question_id",
                table: "answer");

            migrationBuilder.DropForeignKey(
                name: "fk_question_tag_questions_posts_id",
                table: "question_tag");

            migrationBuilder.DropForeignKey(
                name: "fk_question_tag_tag_tags_id",
                table: "question_tag");

            migrationBuilder.DropTable(
                name: "comment");

            migrationBuilder.DropPrimaryKey(
                name: "pk_tag",
                table: "tag");

            migrationBuilder.DropPrimaryKey(
                name: "pk_answer",
                table: "answer");

            migrationBuilder.RenameTable(
                name: "question_tag",
                newName: "question_tag",
                newSchema: "qa");

            migrationBuilder.RenameTable(
                name: "tag",
                newName: "tags",
                newSchema: "qa");

            migrationBuilder.RenameTable(
                name: "answer",
                newName: "answers",
                newSchema: "qa");

            migrationBuilder.RenameColumn(
                name: "posts_id",
                schema: "qa",
                table: "question_tag",
                newName: "questions_id");

            migrationBuilder.RenameColumn(
                name: "posted_by",
                schema: "qa",
                table: "answers",
                newName: "user_id");

            migrationBuilder.RenameIndex(
                name: "ix_answer_question_id",
                schema: "qa",
                table: "answers",
                newName: "ix_answers_question_id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_tags",
                schema: "qa",
                table: "tags",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_answers",
                schema: "qa",
                table: "answers",
                column: "id");

            migrationBuilder.CreateTable(
                name: "answer_comments",
                schema: "qa",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    body = table.Column<string>(type: "text", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    answer_id = table.Column<long>(type: "bigint", nullable: false),
                    answer_comment_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_answer_comments", x => x.id);
                    table.ForeignKey(
                        name: "fk_answer_comments_answer_comments_answer_comment_id",
                        column: x => x.answer_comment_id,
                        principalSchema: "qa",
                        principalTable: "answer_comments",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_answer_comments_answers_answer_id",
                        column: x => x.answer_id,
                        principalSchema: "qa",
                        principalTable: "answers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "question_comments",
                schema: "qa",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    body = table.Column<string>(type: "text", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    question_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_question_comments", x => x.id);
                    table.ForeignKey(
                        name: "fk_question_comments_questions_question_id",
                        column: x => x.question_id,
                        principalSchema: "qa",
                        principalTable: "questions",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_answer_comments_answer_comment_id",
                schema: "qa",
                table: "answer_comments",
                column: "answer_comment_id");

            migrationBuilder.CreateIndex(
                name: "ix_answer_comments_answer_id",
                schema: "qa",
                table: "answer_comments",
                column: "answer_id");

            migrationBuilder.CreateIndex(
                name: "ix_question_comments_question_id",
                schema: "qa",
                table: "question_comments",
                column: "question_id");

            migrationBuilder.AddForeignKey(
                name: "fk_answers_questions_question_id",
                schema: "qa",
                table: "answers",
                column: "question_id",
                principalSchema: "qa",
                principalTable: "questions",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_question_tag_questions_questions_id",
                schema: "qa",
                table: "question_tag",
                column: "questions_id",
                principalSchema: "qa",
                principalTable: "questions",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_question_tag_tags_tags_id",
                schema: "qa",
                table: "question_tag",
                column: "tags_id",
                principalSchema: "qa",
                principalTable: "tags",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_answers_questions_question_id",
                schema: "qa",
                table: "answers");

            migrationBuilder.DropForeignKey(
                name: "fk_question_tag_questions_questions_id",
                schema: "qa",
                table: "question_tag");

            migrationBuilder.DropForeignKey(
                name: "fk_question_tag_tags_tags_id",
                schema: "qa",
                table: "question_tag");

            migrationBuilder.DropTable(
                name: "answer_comments",
                schema: "qa");

            migrationBuilder.DropTable(
                name: "question_comments",
                schema: "qa");

            migrationBuilder.DropPrimaryKey(
                name: "pk_tags",
                schema: "qa",
                table: "tags");

            migrationBuilder.DropPrimaryKey(
                name: "pk_answers",
                schema: "qa",
                table: "answers");

            migrationBuilder.RenameTable(
                name: "question_tag",
                schema: "qa",
                newName: "question_tag");

            migrationBuilder.RenameTable(
                name: "tags",
                schema: "qa",
                newName: "tag");

            migrationBuilder.RenameTable(
                name: "answers",
                schema: "qa",
                newName: "answer");

            migrationBuilder.RenameColumn(
                name: "questions_id",
                table: "question_tag",
                newName: "posts_id");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "answer",
                newName: "posted_by");

            migrationBuilder.RenameIndex(
                name: "ix_answers_question_id",
                table: "answer",
                newName: "ix_answer_question_id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_tag",
                table: "tag",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_answer",
                table: "answer",
                column: "id");

            migrationBuilder.CreateTable(
                name: "comment",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    body = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    posted_by = table.Column<long>(type: "bigint", nullable: false),
                    question_id = table.Column<long>(type: "bigint", nullable: true)
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

            migrationBuilder.CreateIndex(
                name: "ix_comment_question_id",
                table: "comment",
                column: "question_id");

            migrationBuilder.AddForeignKey(
                name: "fk_answer_questions_question_id",
                table: "answer",
                column: "question_id",
                principalSchema: "qa",
                principalTable: "questions",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_question_tag_questions_posts_id",
                table: "question_tag",
                column: "posts_id",
                principalSchema: "qa",
                principalTable: "questions",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_question_tag_tag_tags_id",
                table: "question_tag",
                column: "tags_id",
                principalTable: "tag",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
