using System.Net;
using System.Net.Http.Json;
using Erp.Core.Aggregates.Employees;
using FluentAssertions;

namespace Erp.IntegrationTests;

/// <summary>
/// The employees list is open to every authenticated employee — NIK, NPWP and pay are stripped
/// per row by EmployeeVisibility rather than by hiding the endpoint. That makes the filter
/// builder a disclosure risk: a predicate the caller may not read still answers, through the row
/// count, the question the response body refused. A filter on a redacted field must therefore be
/// refused outright, not applied and then redacted.
/// </summary>
public class EmployeeFilterAuthorizationTests : IntegrationTestBase
{
    public EmployeeFilterAuthorizationTests(ErpApiFactory factory) : base(factory) { }

    private sealed record EmployeeItem(Guid Id, string FullName);

    private sealed record EmployeeList(EmployeeItem[] Items, int TotalCount);

    [Theory]
    [InlineData("""[{"field":"monthlyWage","op":"gt","value":1000000}]""")]
    [InlineData("""[{"field":"nik","op":"is","value":"3201234567890123"}]""")]
    [InlineData("""[{"field":"npwp","op":"nempty","value":null}]""")]
    [InlineData("""[{"field":"terminationDate","op":"nempty","value":null}]""")]
    public async Task Filtering_a_redacted_field_is_forbidden_for_staff_and_managers(string filter)
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Biasa", manager.Id);

        foreach (var subject in new[] { manager, staff })
        {
            var client = await CreateClientForAsync(subject);

            var response = await client.GetAsync($"/api/employees?{FilterQuery.Rows(filter)}");

            response.StatusCode.Should().Be(
                HttpStatusCode.Forbidden,
                "{0} must not learn a redacted value by watching which rows survive",
                subject.FullName);
        }
    }

    [Fact]
    public async Task An_owner_may_filter_the_same_fields()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var client = await CreateClientForAsync(owner);

        var response = await client.GetAsync(
            $"/api/employees?{FilterQuery.Rows("""[{"field":"monthlyWage","op":"gt","value":0}]""")}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<EmployeeList>();
        list!.TotalCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Everyone_may_filter_the_fields_the_directory_already_shows()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Biasa", owner.Id);

        var client = await CreateClientForAsync(staff);

        var response = await client.GetAsync(
            $"/api/employees?{FilterQuery.Rows("""[{"field":"role","op":"nin","value":["Staff"]}]""")}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<EmployeeList>();
        list!.Items.Should().NotContain(item => item.FullName == "Staff Biasa");
    }

    [Theory]
    [InlineData("""[{"field":"password","op":"is","value":"x"}]""")]
    [InlineData("""[{"field":"role","op":"contains","value":"Own"}]""")]
    [InlineData("""[{"field":"hireDate","op":"before","value":"31-12-2024"}]""")]
    [InlineData("not json at all")]
    public async Task A_malformed_filter_is_rejected(string filter)
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var client = await CreateClientForAsync(owner);

        var response = await client.GetAsync($"/api/employees?{FilterQuery.Rows(filter)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
