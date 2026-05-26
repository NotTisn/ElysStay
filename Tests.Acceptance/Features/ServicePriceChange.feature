Feature: Service Price Change

  As a Building Owner
  I want service price updates tracked correctly
  So billing history remains accurate

  Background:
    Given a building owner with email "owner@test.com"
    And a building named "Test Building" owned by the owner
    And a service "Water" exists with current unit price 10000 VND


  @R1
  Scenario: Update service price successfully
    Given the service has no previous unit price
    When the owner updates service price to 15000 VND
    Then current unit price should be 15000 VND
    And previous unit price should be 10000 VND
    And price updated timestamp should be changed


  @R2
  Scenario: Keep values unchanged when price remains the same
    Given current unit price is 10000 VND
    When the owner updates service price to 10000 VND
    Then current unit price should remain 10000 VND
    And previous unit price should remain unchanged
    And price updated timestamp should remain unchanged


  @R3
  Scenario: Return validation error when price is invalid
    Given current unit price is 10000 VND
    When the owner updates service price to -5000 VND
    Then validation error should be displayed
    And current unit price should remain 10000 VND


  @R4
  Scenario: Replace previous unit price after updating again
    Given current unit price is 10000 VND
    And previous unit price is 8000 VND
    When the owner updates service price to 12000 VND
    Then current unit price should be 12000 VND
    And previous unit price should be 10000 VND
    And price updated timestamp should be changed