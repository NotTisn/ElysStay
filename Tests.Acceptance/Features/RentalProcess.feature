Feature: Rental Process
  As a Building Owner
  I want to manage the full tenant rental lifecycle
  So that room availability, deposits, and contracts stay in sync at every step

  Background:
    Given a building owner for rental tests
    And a building for rental tests
    And an available room "101" priced at 5000000 VND for rental tests
    And a tenant for rental tests

# ======================================================
# PATH A: Direct Contract (no reservation)
# SM-06: Available → Occupied
# ======================================================

  @Rental @DirectContract
  Scenario: Direct contract changes room to Occupied
    When I create a direct contract with monthly rent 5000000 and deposit 10000000
    Then the contract status should be "Active"
    And the room status should be "Occupied"

  @Rental @DirectContract
  Scenario: Direct contract creates DEPOSIT_IN payment
    When I create a direct contract with monthly rent 5000000 and deposit 10000000
    Then a DEPOSIT_IN payment of 10000000 should be recorded for the contract

  @Rental @DirectContract
  Scenario: Direct contract auto-creates main tenant record
    When I create a direct contract with monthly rent 5000000 and deposit 10000000
    Then the contract should have 1 tenant with IsMainTenant true

  @Rental @DirectContract
  Scenario: Direct contract sends notification to tenant
    When I create a direct contract with monthly rent 5000000 and deposit 10000000
    Then a contract notification should be created for the tenant

  @Rental @DirectContract
  Scenario: Cannot create direct contract for non-Available room
    Given the room is in Maintenance status for rental tests
    When I try to create a direct contract with monthly rent 5000000 and deposit 10000000
    Then the rental action should be rejected with a conflict error

  @Rental @DirectContract
  Scenario: Cannot create zero-deposit contract on Occupied room (UQ-01)
    When I create a direct contract with monthly rent 5000000 and deposit 10000000
    And I try to create a second direct contract on the same room
    Then the rental action should be rejected with a conflict error

# ======================================================
# PATH B: Reservation → Confirm → Contract
# SM-01, SM-07, SM-02, DEP-02, DEP-03
# ======================================================

  @Rental @Reservation
  Scenario: Creating reservation changes room to Booked
    When I create a reservation with deposit 5000000
    Then the reservation status should be "Pending"
    And the room status should be "Booked"

  @Rental @Reservation
  Scenario: Confirming reservation changes status to Confirmed
    Given a pending reservation with deposit 5000000
    When I confirm the reservation
    Then the reservation status should be "Confirmed"
    And the room status should be "Booked"

  @Rental @Reservation
  Scenario: Contract from confirmed reservation converts reservation and occupies room
    Given a confirmed reservation with deposit 5000000
    When I create a contract from the reservation with monthly rent 5000000 and deposit 5000000
    Then the contract status should be "Active"
    And the room status should be "Occupied"
    And the reservation status should be "Converted"

  @Rental @Reservation
  Scenario: Reservation deposit carries over as DEPOSIT_IN payment
    Given a confirmed reservation with deposit 5000000
    When I create a contract from the reservation with monthly rent 5000000 and deposit 5000000
    Then a DEPOSIT_IN payment of 5000000 should be recorded for the contract

  @Rental @Reservation
  Scenario: Additional deposit created when contract deposit exceeds reservation deposit
    Given a confirmed reservation with deposit 3000000
    When I create a contract from the reservation with monthly rent 5000000 and deposit 8000000
    Then a DEPOSIT_IN payment of 3000000 should be recorded for the contract
    And a DEPOSIT_IN payment of 5000000 should be recorded for the contract

  @Rental @Reservation
  Scenario: Cannot create contract from Pending reservation
    Given a pending reservation with deposit 5000000
    When I try to create a contract from the reservation with monthly rent 5000000 and deposit 5000000
    Then the rental action should be rejected with a conflict error

  @Rental @Reservation
  Scenario: Cannot create contract if contract deposit is less than reservation deposit
    Given a confirmed reservation with deposit 5000000
    When I try to create a contract from the reservation with monthly rent 5000000 and deposit 3000000
    Then the rental action should be rejected with a bad request error

# ======================================================
# PATH C: Reservation Cancellation
# SM-08: Pending/Confirmed → Cancelled, SM-03: Booked → Available
# DEP-05: Deposit handling on cancel
# ======================================================

  @Rental @Cancellation
  Scenario: Cancelling a Pending reservation frees the room
    Given a pending reservation with deposit 5000000
    When I cancel the reservation with refund 0
    Then the reservation status should be "Cancelled"
    And the room status should be "Available"

  @Rental @Cancellation
  Scenario: Cancelling a Pending reservation does not create deposit payments
    Given a pending reservation with deposit 5000000
    When I cancel the reservation with refund 0
    Then no deposit payments should exist for the reservation

  @Rental @Cancellation
  Scenario: Cancelling a Confirmed reservation with full refund creates two payments
    Given a confirmed reservation with deposit 5000000
    When I cancel the reservation with refund 5000000
    Then a DEPOSIT_IN payment of 5000000 should be recorded for the reservation
    And a DEPOSIT_REFUND payment of 5000000 should be recorded for the reservation
    And the room status should be "Available"

  @Rental @Cancellation
  Scenario: Cancelling a Confirmed reservation with zero refund forfeits deposit
    Given a confirmed reservation with deposit 5000000
    When I cancel the reservation with refund 0
    Then a DEPOSIT_IN payment of 5000000 should be recorded for the reservation
    And no DEPOSIT_REFUND payment should exist for the reservation
    And the reservation refund amount should be 0

  @Rental @Cancellation
  Scenario: Cancelling a Confirmed reservation with partial refund
    Given a confirmed reservation with deposit 5000000
    When I cancel the reservation with refund 2000000
    Then a DEPOSIT_IN payment of 5000000 should be recorded for the reservation
    And a DEPOSIT_REFUND payment of 2000000 should be recorded for the reservation

  @Rental @Cancellation
  Scenario: Cannot cancel an already-Cancelled reservation
    Given a pending reservation with deposit 5000000
    And the reservation has been cancelled
    When I try to cancel the reservation again with refund 0
    Then the rental action should be rejected with a conflict error

# ======================================================
# Double-Booking Prevention
# ======================================================

  @Rental @DoubleBooking
  Scenario: Cannot reserve an already-Booked room
    Given a pending reservation with deposit 5000000
    Given a second tenant for rental tests
    When I try to create a reservation for the second tenant
    Then the rental action should be rejected with a conflict error

  @Rental @DoubleBooking
  Scenario: Cannot reserve an Occupied room
    Given a second tenant for rental tests
    When I create a direct contract with monthly rent 5000000 and deposit 10000000
    And I try to create a reservation for the second tenant
    Then the rental action should be rejected with a conflict error

  @Rental @DoubleBooking
  Scenario: Tenant cannot have two active reservations simultaneously
    Given a second available room "102" for rental tests
    And a pending reservation with deposit 5000000
    When I try to create a second reservation on room "102"
    Then the rental action should be rejected with a conflict error
