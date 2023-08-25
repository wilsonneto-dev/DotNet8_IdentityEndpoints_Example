using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddAuthentication().AddBearerToken(IdentityConstants.BearerScheme);
builder.Services.AddAuthorization();

builder.Services.AddDbContext<MinTodoDbContext>(
    options => options.UseSqlServer(builder.Configuration.GetConnectionString("Store")));

builder.Services
    .AddIdentityCore<IdentityUser>()
    .AddEntityFrameworkStores<MinTodoDbContext>()
    .AddApiEndpoints();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

app.Services.CreateScope().ServiceProvider.GetRequiredService<MinTodoDbContext>().Database.EnsureDeleted();
app.Services.CreateScope().ServiceProvider.GetRequiredService<MinTodoDbContext>().Database.EnsureCreated();

app.MapGroup("/identity").MapIdentityApi<IdentityUser>();

app.MapGet("/requires-auth", (ClaimsPrincipal user) => $"Hello, {user.Identity?.Name}!").RequireAuthorization();

var todosApi = app.MapGroup("/todos");

todosApi.MapGet(string.Empty, (MinTodoDbContext dbContext)
    => TypedResults.Ok(dbContext.Todos.ToList()))
    .WithName("TodoList")
    .WithTags("Todo");

todosApi.MapPost(string.Empty, (TodoApiInput input, MinTodoDbContext dbContext)
    => {
        var todo = new Todo() { Description = input.Description };
        dbContext.Todos.Add(todo);
        dbContext.SaveChanges();
        return TypedResults.CreatedAtRoute(
            todo,
            "TodoDetails",
            new { Id = todo.Id }
        );
    })
    .WithName("TodoCreate")
    .WithTags("Todo");

todosApi.MapGet("{id:int}",
    Results<Ok<Todo>, NotFound> (int id, MinTodoDbContext dbContext)
    => dbContext.Todos.FirstOrDefault(x => x.Id == id) is Todo todo ?
        TypedResults.Ok(todo) : TypedResults.NotFound()
    ).WithName("TodoDetails")
    .WithTags("Todo", "Private");

todosApi.MapDelete("{id:int}",
    Results<NoContent, NotFound> (int id, MinTodoDbContext dbContext)
    => {
        var todo = dbContext.Todos.FirstOrDefault(x => x.Id == id);
        if(todo is null)
            return TypedResults.NotFound();
        dbContext.Todos.Remove(todo);
        dbContext.SaveChanges();
        return TypedResults.NoContent();
    }).WithName("TodoDelete")
    .WithTags("Todo", "Public");

todosApi.MapPut("{id:int}",
    Results<Ok<Todo>, NotFound> (TodoApiInput input, int id, MinTodoDbContext dbContext)
    => {
        var todo = dbContext.Todos.FirstOrDefault(x => x.Id == id);
        if(todo is null)
            return TypedResults.NotFound();
        todo.Description = input.Description;
        dbContext.Todos.Update(todo);
        dbContext.SaveChanges();
        return TypedResults.Ok(todo);
    }).WithName("TodoUpdate")
    .WithTags("Todo", "Private");


app.Run();

record TodoApiInput(string Description = "");

public class Todo
{
    public int Id { get; set; }
    public string Description { get; set; } = "";
}

class MinTodoDbContext : IdentityDbContext<IdentityUser>
{
    public DbSet<Todo> Todos => Set<Todo>();
 
    public MinTodoDbContext(DbContextOptions<MinTodoDbContext> options)
        : base(options) { }

}