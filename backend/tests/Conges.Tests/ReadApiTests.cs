using System.Net;
using System.Net.Http.Json;
using Conges.Application.Employees;
using Conges.Application.LeaveRequests;
using Conges.Domain.LeaveRequests;

namespace Conges.Tests;

public sealed class ReadApiTests(CongesApiFactory factory) : IClassFixture<CongesApiFactory>
{
    [Fact]
    public async Task Employees_are_listed_with_their_balance()
    {
        var employees = await factory.CreateClient().GetFromJsonAsync<List<EmployeeDto>>("/api/employees", Json.Options);

        Assert.Equal(9, employees!.Count);
        Assert.Contains(employees, e => e.FullName == "Ines Moreau" && e.PaidLeaveBalance == 3);
    }

    [Fact]
    public async Task Me_requires_the_user_header()
    {
        var response = await factory.CreateClient().GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_returns_the_current_employee()
    {
        var me = await factory.CreateClientAs(2).GetFromJsonAsync<EmployeeDto>("/api/me", Json.Options);

        Assert.Equal("Hugo Bernard", me!.FullName);
        Assert.True(me.IsManager);
    }

    [Fact]
    public async Task Leave_requests_can_be_filtered_by_status()
    {
        var pending = await factory.CreateClient()
            .GetFromJsonAsync<List<LeaveRequestDto>>("/api/leave-requests?status=Pending", Json.Options);

        Assert.NotEmpty(pending!);
        Assert.All(pending!, r => Assert.Equal(LeaveStatus.Pending, r.Status));
    }

    [Fact]
    public async Task Unknown_leave_request_returns_problem_details_404()
    {
        var response = await factory.CreateClient().GetAsync("/api/leave-requests/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
