using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TodoList.Application.Projects;
using TodoList.Application.Tags;
using TodoList.Application.Tasks;

namespace TodoList.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<CreateTaskRequestValidator>();

        services.AddScoped<ProjectService>();
        services.AddScoped<ProjectQueries>();
        services.AddScoped<TagService>();
        services.AddScoped<TagQueries>();
        services.AddScoped<TaskService>();
        services.AddScoped<TaskQueries>();

        return services;
    }
}
