using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinRiskLensAI.Data.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class Add_LoanCasePush_EnterpriseName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EnterpriseName",
                table: "t_LoanCasePush",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnterpriseName",
                table: "t_LoanCasePush");
        }
    }
}
