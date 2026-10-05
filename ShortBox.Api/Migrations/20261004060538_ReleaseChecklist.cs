using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShortBox.Api.Migrations
{
    /// <inheritdoc />
    public partial class ReleaseChecklist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ThumbnailUri",
                table: "PullList",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<bool>(
                name: "IsWanted",
                table: "PullList",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Number",
                table: "PullList",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Series",
                table: "PullList",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "StoreDate",
                table: "PullList",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsWanted",
                table: "PullList");

            migrationBuilder.DropColumn(
                name: "Number",
                table: "PullList");

            migrationBuilder.DropColumn(
                name: "Series",
                table: "PullList");

            migrationBuilder.DropColumn(
                name: "StoreDate",
                table: "PullList");

            migrationBuilder.AlterColumn<string>(
                name: "ThumbnailUri",
                table: "PullList",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
