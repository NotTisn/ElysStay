using TechTalk.SpecFlow;
using Xunit;
using System;
using System.Linq;
using System.Threading.Tasks;
using Domain.Entities;
using Domain.Enums;
using Tests.Integration.Fixtures;
using Tests.Integration.Builders;
using Microsoft.EntityFrameworkCore;

namespace ElysStay.Tests.Acceptance.StepDefinitions;

[Binding]
public class UserLoginSteps
{
    private readonly DatabaseFixture _fixture;
    private string _loginEmail = string.Empty;
    private string _loginPassword = string.Empty;
    
    // For demo purposes, we hold the token or exception
    private string? _authToken;
    private Exception? _loginException;

    public UserLoginSteps(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Given("a registered user exists with email \"([^\"]*)\" and password \"([^\"]*)\"")]
    public async Task GivenARegisteredUserExistsWithEmailAndPassword(string email, string password)
    {
        // For demonstration: Instead of saving the raw password, typically we'd hash it.
        // We will just store it to mimic a newly registered user.
        
        // Clear users to prevent duplicate email constraint issues between scenarios
        _fixture.DbContext.Users.RemoveRange(_fixture.DbContext.Users);
        await _fixture.DbContext.SaveChangesAsync();

        var user = TestDataBuilder.CreateUser(email: email, role: UserRole.Tenant);
        
        // Let's assume your Password property is accessible, or use whatever property ElyStay uses.
        // For a demo, this setup gets the user into the test database.
        
        await _fixture.DbContext.Users.AddAsync(user);
        await _fixture.DbContext.SaveChangesAsync();
        
        // We store the exact password to simulate matching in the When step for the demo.
        _loginPassword = password;
    }

    [When("I attempt to login with email \"([^\"]*)\" and password \"([^\"]*)\"")]
    public async Task WhenIAttemptToLoginWithEmailAndPassword(string email, string attemptPassword)
    {
        try
        {
            // Simulate calling the Login Service / API Endpoint
            var user = await _fixture.DbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
            
            if (user == null || attemptPassword != _loginPassword)
            {
                throw new Exception("Invalid email or password");
            }
            
            // If it succeeds, mock returning a JWT generated token
            _authToken = $"eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.{user.Id}";
        }
        catch (Exception ex)
        {
            _loginException = ex;
        }
    }

    [Then("the login should be successful")]
    public void ThenTheLoginShouldBeSuccessful()
    {
        Assert.Null(_loginException);
    }

    [Then("I should receive an authentication token")]
    public void ThenIShouldReceiveAnAuthenticationToken()
    {
        Assert.False(string.IsNullOrEmpty(_authToken), "Authentication token should not be null or empty.");
    }

    [Then("the login should fail")]
    public void ThenTheLoginShouldFail()
    {
        Assert.NotNull(_loginException);
        Assert.Null(_authToken);
    }

    [Then("I should see an error message \"([^\"]*)\"")]
    public void ThenIShouldSeeAnErrorMessage(string expectedError)
    {
        Assert.NotNull(_loginException);
        Assert.Contains(expectedError, _loginException.Message);
    }
}
