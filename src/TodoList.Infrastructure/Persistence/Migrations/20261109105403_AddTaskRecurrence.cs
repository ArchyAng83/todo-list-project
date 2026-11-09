using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoList.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskRecurrence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "recurrence",
                table: "tasks",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<DateOnly>(
                name: "recurrence_end_date",
                table: "tasks",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "recurrence_generated",
                table: "tasks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "recurrence_interval",
                table: "tasks",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "recurrence",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "recurrence_end_date",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "recurrence_generated",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "recurrence_interval",
                table: "tasks");
        }
    }
}
