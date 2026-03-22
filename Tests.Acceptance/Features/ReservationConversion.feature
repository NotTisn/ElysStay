Feature: Reservation to Contract Conversion
  As a Building Manager
  I want to convert pending reservations to active contracts
  So the reservation deposit carries over and the tenant can move in

  Background:
    Given a building owner
    And a room with deposit required 10000000 VND
    And a tenant

  Scenario: Convert pending reservation to contract
    Given a pending reservation with deposit 10000000 VND
    When I convert reservation to contract
    Then contract should be created with:
      | Status         | Active             |
      | DepositAmount  | 10000000           |
      | DepositStatus  | Unpaid             | # Inherited from reservation
      | Room           | Same room          |
      | Tenant         | Same tenant        |
    And reservation status should be "Confirmed"

  Scenario: Reject conversion of non-pending reservation
    Given a cancelled reservation
    When I try to convert reservation to contract
    Then system should reject with error "Only pending reservations can be converted"

  Scenario: Prevent double contract from same reservation
    Given a pending reservation that was already converted to contract
    When I try to convert it again
    Then system should reject with error "Reservation already converted to contract"
