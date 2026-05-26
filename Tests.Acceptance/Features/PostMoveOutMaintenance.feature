Feature: Post Move-out Maintenance Process
  As a Building Owner or Staff
  I want to set a room to Maintenance after a tenant moves out
  So that the room is blocked from new bookings until repairs are complete

  Background:
    Given a building owner for maintenance tests
    And a building for maintenance tests
    And a room "301" in Available status

# ======================================================
# SM-05: Valid status transitions
# ======================================================

  @Maintenance @HappyPath
  Scenario: Owner sets Available room to Maintenance after move-out
    When I change the room status to "Maintenance"
    Then the room status in the database should be "Maintenance"

  @Maintenance @HappyPath
  Scenario: Owner sets Maintenance room back to Available after repairs
    Given the room is in Maintenance status
    When I change the room status to "Available"
    Then the room status in the database should be "Available"

  @Maintenance @HappyPath
  Scenario: Status change is case-insensitive
    When I change the room status to "maintenance"
    Then the room status in the database should be "Maintenance"

  @Maintenance @HappyPath
  Scenario: Round-trip Available to Maintenance and back
    When I change the room status to "Maintenance"
    Then the room status in the database should be "Maintenance"
    When I change the room status to "Available"
    Then the room status in the database should be "Available"

# ======================================================
# SM-05: Rejection guards — invalid target status
# ======================================================

  @Maintenance @Validation
  Scenario: Cannot manually set room to Occupied
    When I try to change the room status to "Occupied"
    Then the status change should be rejected with a bad request error

  @Maintenance @Validation
  Scenario: Cannot manually set room to Booked
    When I try to change the room status to "Booked"
    Then the status change should be rejected with a bad request error

  @Maintenance @Validation
  Scenario: Invalid status string is rejected
    When I try to change the room status to "Broken"
    Then the status change should be rejected with a bad request error

# ======================================================
# SM-05: Rejection guards — invalid current status
# ======================================================

  @Maintenance @Validation
  Scenario: Cannot change status of an Occupied room
    Given the room is in Occupied status
    When I try to change the room status to "Maintenance"
    Then the status change should be rejected with a conflict error

  @Maintenance @Validation
  Scenario: Cannot change status of a Booked room
    Given the room is in Booked status
    When I try to change the room status to "Available"
    Then the status change should be rejected with a conflict error

# ======================================================
# SM-05: Same-status guard
# ======================================================

  @Maintenance @Validation
  Scenario: Cannot set Maintenance room to Maintenance again
    Given the room is in Maintenance status
    When I try to change the room status to "Maintenance"
    Then the status change should be rejected with a bad request error

  @Maintenance @Validation
  Scenario: Cannot set Available room to Available again
    When I try to change the room status to "Available"
    Then the status change should be rejected with a bad request error

# ======================================================
# State integrity
# ======================================================

  @Maintenance @Validation
  Scenario: Failed transition does not change persisted status
    Given the room is in Occupied status
    When I try to change the room status to "Maintenance"
    Then the room status in the database should be "Occupied"
