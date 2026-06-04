Feature: Invoice Lifecycle
  As a Building Owner
  I want to send, collect payment for, and void invoices
  So that billing follows the rental and financial-reporting rules

  # Drives the real Send/RecordPayment/Void command handlers (not direct DB writes),
  # covering the invoice side of 1.5.4a (send), 1.5.4b (payment constraints) and
  # 1.5.7 (void — OWNER only).

  Background:
    Given the owner has a building with room "101" renting at 5000000 VND and an active tenant contract

  # ======================================================
  # Send invoice (1.5.4a)
  # ======================================================

  @Send
  Scenario: Owner sends a Draft invoice to the tenant
    Given an invoice in "Draft" status
    When the owner sends the invoice
    Then the invoice status should be "Sent"

  @Send
  Scenario: Cannot send an invoice that is not in Draft status
    Given an invoice in "Sent" status
    When the owner sends the invoice
    Then the operation should be rejected
    And the invoice status should be "Sent"

  # ======================================================
  # Payment (1.5.4b)
  # ======================================================

  @Payment
  Scenario: Partial payment moves a Sent invoice to PartiallyPaid
    Given an invoice in "Sent" status
    When the owner records a payment of 2000000 VND
    Then the invoice status should be "PartiallyPaid"
    And a payment of 2000000 VND should be recorded

  @Payment
  Scenario: Full payment marks the invoice as Paid
    Given an invoice in "Sent" status
    When the owner records a payment of 5000000 VND
    Then the invoice status should be "Paid"

  @Payment
  Scenario: Payment is blocked on a Draft invoice
    Given an invoice in "Draft" status
    When the owner records a payment of 5000000 VND
    Then the operation should be rejected
    And no payment should be recorded

  @Payment
  Scenario: Payment is blocked on a Void invoice
    Given an invoice in "Void" status
    When the owner records a payment of 5000000 VND
    Then the operation should be rejected
    And no payment should be recorded

  @Payment
  Scenario: Overpayment beyond the invoice total is rejected
    Given an invoice in "Sent" status
    When the owner records a payment of 6000000 VND
    Then the operation should be rejected
    And no payment should be recorded

  # ======================================================
  # Void invoice (1.5.7 — OWNER only)
  # ======================================================

  @Void
  Scenario: Owner voids a Sent invoice
    Given an invoice in "Sent" status
    When the owner voids the invoice
    Then the invoice status should be "Void"

  @Void
  Scenario: Cannot void a fully Paid invoice
    Given an invoice in "Paid" status
    When the owner voids the invoice
    Then the operation should be rejected
    And the invoice status should be "Paid"

  @Void
  Scenario: Cannot void an already voided invoice
    Given an invoice in "Void" status
    When the owner voids the invoice
    Then the operation should be rejected

  @Void @Authorization
  Scenario: Staff cannot void an invoice
    Given an invoice in "Sent" status
    When a staff member voids the invoice
    Then the operation should be rejected with a permission error
    And the invoice status should be "Sent"

  @Void @Authorization
  Scenario: Tenant cannot void an invoice
    Given an invoice in "Sent" status
    When the tenant voids the invoice
    Then the operation should be rejected with a permission error
    And the invoice status should be "Sent"
