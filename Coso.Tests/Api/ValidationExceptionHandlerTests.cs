using Cosmo.Api.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Cosmo.Tests.Api;

public class ValidationExceptionHandlerTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TryHandle_ValidationFailure_GroupsAndDeduplicatesErrorsAndReturnsWriterResult(bool canWrite)
    {
        var writer = new RecordingProblemDetailsService(canWrite);
        var handler = new ValidationExceptionHandler(writer);
        var httpContext = new DefaultHttpContext();
        var exception = new ValidationException([
            new ValidationFailure("Message", "Required"),
            new ValidationFailure("Message", "Required"),
            new ValidationFailure("Message", "Too long"),
            new ValidationFailure("Other", "Invalid")]);

        var handled = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        Assert.Equal(canWrite, handled);
        Assert.Equal(400, httpContext.Response.StatusCode);
        Assert.Same(httpContext, writer.Context!.HttpContext);
        var problem = Assert.IsType<ValidationProblemDetails>(writer.Context.ProblemDetails);
        Assert.Equal(400, problem.Status);
        Assert.Equal(new[] { "Required", "Too long" }, problem.Errors["Message"]);
        Assert.Equal(new[] { "Invalid" }, problem.Errors["Other"]);
        Assert.Equal(2, problem.Errors.Count);
    }

    [Fact]
    public async Task TryHandle_UnrelatedException_LeavesResponseForOtherHandlers()
    {
        var writer = new RecordingProblemDetailsService(true);
        var httpContext = new DefaultHttpContext();

        var handled = await new ValidationExceptionHandler(writer).TryHandleAsync(
            httpContext, new InvalidOperationException(), CancellationToken.None);

        Assert.False(handled);
        Assert.Null(writer.Context);
        Assert.Equal(200, httpContext.Response.StatusCode);
    }

    private sealed class RecordingProblemDetailsService(bool canWrite) : IProblemDetailsService
    {
        public ProblemDetailsContext? Context { get; private set; }

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            Context = context;
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
        {
            Context = context;
            return ValueTask.FromResult(canWrite);
        }
    }
}
