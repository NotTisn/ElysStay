Feature: Move-out Process
  As a Building Owner
  I want to correctly process tenant move-outs
  So that deposit refunds are calculated fairly and all state transitions happen correctly

  Background:
    Given a building owner for move-out tests
    And a building for move-out tests
    And a room "201" with deposit 10000000 VND
    And a tenant for move-out tests
    And an active contract with deposit 10000000 VND

# ======================================================
# Deposit Calculation (DEP-04)
# ======================================================

  @MoveOut @Deposit
  Scenario: Full refund when no deductions
    Given no deductions on move-out
    When I terminate the contract on "2026-06-30"
    Then the deposit status should be "Refunded"
    And the refund amount should be 10000000 VND
    And a DEPOSIT_REFUND payment of 10000000 VND should be created

  @MoveOut @Deposit
  Scenario: Partial refund when there are deductions
    Given deductions of 3000000 VND for damages
    When I terminate the contract on "2026-06-30"
    Then the deposit status should be "PartiallyRefunded"
    And the refund amount should be 7000000 VND
    And a DEPOSIT_REFUND payment of 7000000 VND should be created

  @MoveOut @Deposit
  Scenario: Deposit forfeited when deductions equal full deposit
    Given deductions of 10000000 VND for damages
    When I terminate the contract on "2026-06-30"
    Then the deposit status should be "Forfeited"
    And the refund amount should be 0 VND
    And an audit DEPOSIT_REFUND payment of 0 VND should be created

# ======================================================
# Contract & Room State Transitions
# ======================================================

  @MoveOut @Status
  Scenario: Contract status changes to Terminated
    Given no deductions on move-out
    When I terminate the contract on "2026-06-30"
    Then the contract status should be "Terminated"
    And the termination date should be "2026-06-30"

  @MoveOut @Status
  Scenario: Occupied room becomes available after move-out
    Given the room is occupied by the tenant
    And no deductions on move-out
    When I terminate the contract on "2026-06-30"
    Then the room status should be "Available"

  @MoveOut @Status
  Scenario: Room in maintenance stays in maintenance after move-out
    Given the room is in maintenance status
    And no deductions on move-out
    When I terminate the contract on "2026-06-30"
    Then the room status should be "Maintenance"

# ======================================================
# Contract Tenants Move-Out Date (SD-02)
# ======================================================

  @MoveOut @Tenants
  Scenario: Move-out date is recorded for all active tenants
    Given the contract has 2 active occupants
    And no deductions on move-out
    When I terminate the contract on "2026-06-30"
    Then all active occupants should have move-out date "2026-06-30"

# ======================================================
# Auto-void Future Invoices
# ======================================================

  @MoveOut @Invoices
  Scenario: Future unpaid invoices are voided on termination
    Given there is a Draft invoice for July 2026
    And there is a Sent invoice for August 2026
    And no deductions on move-out
    When I terminate the contract on "2026-06-30"
    Then the July 2026 invoice should be Void
    And the August 2026 invoice should be Void

  @MoveOut @Invoices
  Scenario: Invoice for the termination month is not voided
    Given there is a Draft invoice for June 2026
    And no deductions on move-out
    When I terminate the contract on "2026-06-30"
    Then the June 2026 invoice should still be Draft

  @MoveOut @Invoices
  Scenario: Future invoice with existing payment is not voided
    Given there is a Sent invoice for July 2026 with a payment
    And no deductions on move-out
    When I terminate the contract on "2026-06-30"
    Then the July 2026 invoice should still be Sent

# ======================================================
# Validation Rules
# ======================================================

  @MoveOut @Validation
  Scenario: Cannot terminate an already terminated contract
    Given the contract is already terminated
    When I try to terminate the contract on "2026-07-31"
    Then the move-out should be rejected with conflict error

  @MoveOut @Validation
  Scenario: Deductions cannot be negative
    Given deductions of -500000 VND for damages
    When I try to terminate the contract on "2026-06-30"
    Then the move-out should be rejected with validation error

  @MoveOut @Validation
  Scenario: Deductions cannot exceed deposit amount
    Given deductions of 15000000 VND for damages
    When I try to terminate the contract on "2026-06-30"
    Then the move-out should be rejected with validation error

  @MoveOut @Validation
  Scenario: Termination date cannot be before contract start date
    When I try to terminate the contract on "2025-12-31"
    Then the move-out should be rejected with validation error

# ======================================================
# Note Persistence
# ======================================================

  @MoveOut @Note
  Scenario: Termination note is saved with deduction reason
    Given deductions of 2000000 VND for damages
    And a note "Hư hỏng tường và cửa sổ"
    When I terminate the contract on "2026-06-30"
    Then the termination note "Hư hỏng tường và cửa sổ" should be saved
