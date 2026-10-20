using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHadithTree.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "HadithClusters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GawamiClusterId = table.Column<string>(type: "text", nullable: false),
                    Taraf = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HadithClusters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Narrators",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    KnownAs = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    NormalizedFullName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, collation: "C"),
                    NormalizedKnownAs = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true, collation: "C"),
                    Kunyah = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    GenerationTier = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    BirthYearHijri = table.Column<int>(type: "integer", nullable: true),
                    DeathYearHijri = table.Column<int>(type: "integer", nullable: true),
                    BirthPlace = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DeathPlace = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ResidencePlaces = table.Column<string>(type: "text", nullable: true),
                    GawamiRank = table.Column<string>(type: "text", nullable: true),
                    TotalNarrationsCount = table.Column<int>(type: "integer", nullable: true),
                    UniqueHadithCount = table.Column<int>(type: "integer", nullable: true),
                    Biography = table.Column<string>(type: "text", nullable: true),
                    SourceKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ShamelaManId = table.Column<int>(type: "integer", nullable: true),
                    IbnHajarRank = table.Column<int>(type: "integer", nullable: true),
                    Verdict = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ItqanId = table.Column<int>(type: "integer", nullable: true),
                    GawamiId = table.Column<int>(type: "integer", nullable: true),
                    IsMudallis = table.Column<bool>(type: "boolean", nullable: false),
                    HasMukhtalit = table.Column<bool>(type: "boolean", nullable: false),
                    MudallisTier = table.Column<int>(type: "integer", nullable: true),
                    IkhtilatNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IkhtilatSeverity = table.Column<int>(type: "integer", nullable: true),
                    NoHearingAfterIkhtilat = table.Column<bool>(type: "boolean", nullable: false),
                    ItqanGrade = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Narrators", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Hadiths",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MatnArabic = table.Column<string>(type: "text", nullable: false),
                    NormalizedMatn = table.Column<string>(type: "text", nullable: false, collation: "C"),
                    BookName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    NormalizedBookName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false, collation: "C"),
                    HadithNumber = table.Column<int>(type: "integer", nullable: false),
                    Volume = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Chapter = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FullIsnadText = table.Column<string>(type: "text", nullable: true, collation: "C"),
                    HadithClusterId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hadiths", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Hadiths_HadithClusters_HadithClusterId",
                        column: x => x.HadithClusterId,
                        principalTable: "HadithClusters",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MukhtalitGroupRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MukhtalitId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupAr = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Timing = table.Column<int>(type: "integer", nullable: false),
                    Quote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SourceBook = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MukhtalitGroupRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MukhtalitGroupRules_Narrators_MukhtalitId",
                        column: x => x.MukhtalitId,
                        principalTable: "Narrators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MukhtalitHearings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MukhtalitId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Timing = table.Column<int>(type: "integer", nullable: false),
                    Evidence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SourceBook = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MukhtalitHearings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MukhtalitHearings_Narrators_MukhtalitId",
                        column: x => x.MukhtalitId,
                        principalTable: "Narrators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MukhtalitHearings_Narrators_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Narrators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NarratorRelations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NarratorRelations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NarratorRelations_Narrators_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Narrators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NarratorRelations_Narrators_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Narrators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScholarEvaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NarratorId = table.Column<Guid>(type: "uuid", nullable: false),
                    GawamiAlemId = table.Column<int>(type: "integer", nullable: true),
                    GawamiRawyId = table.Column<int>(type: "integer", nullable: true),
                    ScholarName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    EvaluationText = table.Column<string>(type: "text", nullable: false),
                    SourceBook = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SourceVolume = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SourcePage = table.Column<int>(type: "integer", nullable: true),
                    VerdictRating = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScholarEvaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScholarEvaluations_Narrators_NarratorId",
                        column: x => x.NarratorId,
                        principalTable: "Narrators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Transmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HadithId = table.Column<Guid>(type: "uuid", nullable: false),
                    SheikhId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepOrder = table.Column<int>(type: "integer", nullable: false),
                    TransmissionTerm = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transmissions_Hadiths_HadithId",
                        column: x => x.HadithId,
                        principalTable: "Hadiths",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Transmissions_Narrators_SheikhId",
                        column: x => x.SheikhId,
                        principalTable: "Narrators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transmissions_Narrators_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Narrators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Hadiths_BookName_HadithNumber",
                table: "Hadiths",
                columns: new[] { "BookName", "HadithNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Hadiths_FullIsnadText_Trgm",
                table: "Hadiths",
                column: "FullIsnadText")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Hadiths_HadithClusterId",
                table: "Hadiths",
                column: "HadithClusterId");

            migrationBuilder.CreateIndex(
                name: "IX_Hadiths_HadithNumber",
                table: "Hadiths",
                column: "HadithNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Hadiths_NormalizedBookName",
                table: "Hadiths",
                column: "NormalizedBookName");

            migrationBuilder.CreateIndex(
                name: "IX_Hadiths_NormalizedBookName_Trgm",
                table: "Hadiths",
                column: "NormalizedBookName")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Hadiths_NormalizedMatn_Trgm",
                table: "Hadiths",
                column: "NormalizedMatn")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_MukhtalitGroupRules_MukhtalitId",
                table: "MukhtalitGroupRules",
                column: "MukhtalitId");

            migrationBuilder.CreateIndex(
                name: "IX_MukhtalitHearings_MukhtalitId_StudentId",
                table: "MukhtalitHearings",
                columns: new[] { "MukhtalitId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MukhtalitHearings_StudentId",
                table: "MukhtalitHearings",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_NarratorRelations_StudentId_TeacherId",
                table: "NarratorRelations",
                columns: new[] { "StudentId", "TeacherId" });

            migrationBuilder.CreateIndex(
                name: "IX_NarratorRelations_TeacherId_StudentId",
                table: "NarratorRelations",
                columns: new[] { "TeacherId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Narrators_DeathYearHijri",
                table: "Narrators",
                column: "DeathYearHijri");

            migrationBuilder.CreateIndex(
                name: "IX_Narrators_FullName",
                table: "Narrators",
                column: "FullName");

            migrationBuilder.CreateIndex(
                name: "IX_Narrators_ItqanId",
                table: "Narrators",
                column: "ItqanId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Narrators_KnownAs",
                table: "Narrators",
                column: "KnownAs");

            migrationBuilder.CreateIndex(
                name: "IX_Narrators_NormalizedFullName_Trgm",
                table: "Narrators",
                column: "NormalizedFullName")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Narrators_NormalizedKnownAs_Trgm",
                table: "Narrators",
                column: "NormalizedKnownAs")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Narrators_ShamelaManId",
                table: "Narrators",
                column: "ShamelaManId");

            migrationBuilder.CreateIndex(
                name: "IX_Narrators_SourceKey",
                table: "Narrators",
                column: "SourceKey",
                unique: true,
                filter: "\"SourceKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ScholarEvaluations_NarratorId",
                table: "ScholarEvaluations",
                column: "NarratorId");

            migrationBuilder.CreateIndex(
                name: "IX_ScholarEvaluations_ScholarName",
                table: "ScholarEvaluations",
                column: "ScholarName");

            migrationBuilder.CreateIndex(
                name: "IX_Transmissions_HadithId_StepOrder",
                table: "Transmissions",
                columns: new[] { "HadithId", "StepOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Transmissions_SheikhId_StudentId",
                table: "Transmissions",
                columns: new[] { "SheikhId", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Transmissions_StudentId_SheikhId",
                table: "Transmissions",
                columns: new[] { "StudentId", "SheikhId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MukhtalitGroupRules");

            migrationBuilder.DropTable(
                name: "MukhtalitHearings");

            migrationBuilder.DropTable(
                name: "NarratorRelations");

            migrationBuilder.DropTable(
                name: "ScholarEvaluations");

            migrationBuilder.DropTable(
                name: "Transmissions");

            migrationBuilder.DropTable(
                name: "Hadiths");

            migrationBuilder.DropTable(
                name: "Narrators");

            migrationBuilder.DropTable(
                name: "HadithClusters");
        }
    }
}
