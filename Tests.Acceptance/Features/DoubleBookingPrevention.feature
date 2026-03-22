Feature: Double-Booking Prevention
  As a System Administrator
  I want to prevent booking the same room for overlapping dates
  So there are no conflicts and legal disputes

  Background:
    Given a building owner with email "owner@test.com"
    And a building named "Test Building"
    And a room "102" in the building

  Scenario: Reject reservation with overlapping dates
    Given room "102" is already reserved from 1/3/2026 to 30/4/2026
    When I try to create reservation from 15/3/2026 to 15/5/2026
    Then system should reject with error "Room not available for selected dates"
    And reservation should NOT be created

  Scenario: Allow reservation after current reservation ends
    Given room "102" is reserved from 1/3/2026 to 30/4/2026
    When I create reservation from 1/5/2026 to 30/6/2026
    Then reservation should be created successfully
    And status should be "Pending"

  Scenario: Reject multiple overlapping reservations
    Given room "102" is reserved from 1/3/2026 to 31/3/2026
    And room "102" is also reserved from 20/4/2026 to 30/4/2026
    When I try to create reservation from 15/3/2026 to 25/4/2026
    Then system should reject with error "Room not available for selected dates"
