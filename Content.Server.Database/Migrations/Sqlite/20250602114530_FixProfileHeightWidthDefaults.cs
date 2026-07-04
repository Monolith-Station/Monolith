using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class FixProfileHeightWidthDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update all existing profiles that have invalid height/width values
            // (0 or very small values that cause sprite scale errors) to default 1.0
            migrationBuilder.Sql(@"
                UPDATE profile 
                SET height = 1.0 
                WHERE height <= 0.005;
            ");

            migrationBuilder.Sql(@"
                UPDATE profile 
                SET width = 1.0 
                WHERE width <= 0.005;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No rollback for data fixes - we don't want to restore broken data
        }
    }
}