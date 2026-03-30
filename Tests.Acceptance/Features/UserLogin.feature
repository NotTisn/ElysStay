Feature: User Login
  As a registered user
  I want to log into the system
  So that I can access my account and manage my rentals

  Background:
    Given a registered user exists with email "john@example.com" and password "Secret123!"

  Scenario: Successful login with valid credentials
    When I attempt to login with email "john@example.com" and password "Secret123!"
    Then the login should be successful
    And I should receive an authentication token

  Scenario: Unsuccessful login with incorrect password
    When I attempt to login with email "john@example.com" and password "WrongPassword!"
    Then the login should fail
    And I should see an error message "Invalid email or password"

  Scenario: Unsuccessful login with unregistered email
    When I attempt to login with email "unknown@example.com" and password "Secret123!"
    Then the login should fail
    And I should see an error message "Invalid email or password"
