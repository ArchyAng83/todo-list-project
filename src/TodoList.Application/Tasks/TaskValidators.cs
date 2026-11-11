using FluentValidation;
using TodoList.Application.Common;
using TodoList.Domain.Tasks;

namespace TodoList.Application.Tasks;

public sealed class CreateTaskRequestValidator : AbstractValidator<CreateTaskRequest>
{
    public CreateTaskRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .WithErrorCode(ValidationCodes.Required)
            .MaximumLength(TodoTask.TitleMaxLength)
            .WithErrorCode(ValidationCodes.TooLong);

        RuleFor(x => x.Priority)
            .IsInEnum()
            .WithErrorCode(ValidationCodes.InvalidEnum);

        RuleFor(x => x.DueTime)
            .Null()
            .When(x => x.DueDate is null)
            .WithErrorCode(ValidationCodes.DueTimeRequiresDueDate)
            .WithMessage("A due time requires a due date.");

        RuleFor(x => x.ReminderOffsetMinutes)
            .InclusiveBetween(0, TodoTask.MaxReminderOffsetMinutes)
            .When(x => x.ReminderOffsetMinutes.HasValue)
            .WithErrorCode(ValidationCodes.OutOfRange);

        RuleFor(x => x.ReminderOffsetMinutes)
            .Null()
            .When(x => x.DueDate is null)
            .WithErrorCode(ValidationCodes.ReminderRequiresDueDate)
            .WithMessage("A reminder requires a due date.");

        RuleFor(x => x.Recurrence)
            .IsInEnum()
            .WithErrorCode(ValidationCodes.InvalidEnum);

        RuleFor(x => x.Recurrence)
            .Equal(RecurrenceFrequency.None)
            .When(x => x.DueDate is null)
            .WithErrorCode(ValidationCodes.RecurrenceRequiresDueDate)
            .WithMessage("A recurring task requires a due date.");

        RuleFor(x => x.RecurrenceInterval)
            .InclusiveBetween(1, TodoTask.MaxRecurrenceInterval)
            .When(x => x.Recurrence != RecurrenceFrequency.None)
            .WithErrorCode(ValidationCodes.OutOfRange)
            .WithMessage($"Recurrence interval must be between 1 and {TodoTask.MaxRecurrenceInterval}.");

        RuleFor(x => x.RecurrenceEndDate)
            .Must((request, endDate) =>
                endDate is null || request.DueDate is null || endDate >= request.DueDate)
            .WithErrorCode(ValidationCodes.RecurrenceEndBeforeDue)
            .WithMessage("The recurrence end date cannot be before the due date.");
    }
}

public sealed class UpdateTaskRequestValidator : AbstractValidator<UpdateTaskRequest>
{
    public UpdateTaskRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .WithErrorCode(ValidationCodes.Required)
            .MaximumLength(TodoTask.TitleMaxLength)
            .WithErrorCode(ValidationCodes.TooLong);

        RuleFor(x => x.Notes)
            .MaximumLength(TodoTask.NotesMaxLength)
            .WithErrorCode(ValidationCodes.TooLong);

        RuleFor(x => x.Priority)
            .IsInEnum()
            .WithErrorCode(ValidationCodes.InvalidEnum);

        RuleFor(x => x.ReminderOffsetMinutes)
            .InclusiveBetween(0, TodoTask.MaxReminderOffsetMinutes)
            .When(x => x.ReminderOffsetMinutes.HasValue)
            .WithErrorCode(ValidationCodes.OutOfRange);

        RuleFor(x => x.DueTime)
            .Null()
            .When(x => x.DueDate is null)
            .WithErrorCode(ValidationCodes.DueTimeRequiresDueDate)
            .WithMessage("A due time requires a due date.");

        RuleFor(x => x.ReminderOffsetMinutes)
            .Null()
            .When(x => x.DueDate is null)
            .WithErrorCode(ValidationCodes.ReminderRequiresDueDate)
            .WithMessage("A reminder requires a due date.");

        RuleFor(x => x.Recurrence)
            .IsInEnum()
            .WithErrorCode(ValidationCodes.InvalidEnum);

        RuleFor(x => x.Recurrence)
            .Equal(RecurrenceFrequency.None)
            .When(x => x.DueDate is null)
            .WithErrorCode(ValidationCodes.RecurrenceRequiresDueDate)
            .WithMessage("A recurring task requires a due date.");

        RuleFor(x => x.RecurrenceInterval)
            .InclusiveBetween(1, TodoTask.MaxRecurrenceInterval)
            .When(x => x.Recurrence != RecurrenceFrequency.None)
            .WithErrorCode(ValidationCodes.OutOfRange)
            .WithMessage($"Recurrence interval must be between 1 and {TodoTask.MaxRecurrenceInterval}.");

        RuleFor(x => x.RecurrenceEndDate)
            .Must((request, endDate) =>
                endDate is null || request.DueDate is null || endDate >= request.DueDate)
            .WithErrorCode(ValidationCodes.RecurrenceEndBeforeDue)
            .WithMessage("The recurrence end date cannot be before the due date.");
    }
}

public sealed class MoveTaskRequestValidator : AbstractValidator<MoveTaskRequest>
{
    public MoveTaskRequestValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum()
            .WithErrorCode(ValidationCodes.InvalidEnum);

        RuleFor(x => x.TargetIndex)
            .GreaterThanOrEqualTo(0)
            .WithErrorCode(ValidationCodes.TargetIndexInvalid);
    }
}

public sealed class AddChecklistItemRequestValidator : AbstractValidator<AddChecklistItemRequest>
{
    public AddChecklistItemRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .WithErrorCode(ValidationCodes.Required)
            .MaximumLength(ChecklistItem.TitleMaxLength)
            .WithErrorCode(ValidationCodes.TooLong);
    }
}

public sealed class AddTaskTagRequestValidator : AbstractValidator<AddTaskTagRequest>
{
    public AddTaskTagRequestValidator()
    {
        RuleFor(x => x.TagId)
            .NotEmpty()
            .WithErrorCode(ValidationCodes.Required);
    }
}
