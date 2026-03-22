Feature: Payment Recording and Tracking
  As a Building Manager
  I want to record payments via different methods and track payment status
  So we have accurate financial records

  Background:
    Given a building owner
    And an invoice with amount 5000000 VND
    And payment methods available: CASH, BANK_TRANSFER, MOMO, ZALOPAY

  Scenario: Record payment via bank transfer
    When I record payment of 5000000 VND via BANK_TRANSFER
    Then payment should be created with:
      | Amount        | 5000000       |
      | Method        | BANK_TRANSFER |
      | Status        | Completed     |
      | Invoice       | Marked as Paid|
    And invoice status should be "Paid"

  Scenario: Record partial payment
    When I record payment of 2500000 VND via CASH
    Then payment should be created successfully
    And invoice status should be "PartialPaid"
    And remaining amount should be 2500000 VND

  Scenario: Record multiple payments to clear invoice
    When I record first payment of 3000000 VND
    And I record second payment of 2000000 VND
    Then total paid should be 5000000 VND
    And invoice status should be "Paid"

  Scenario: Record MOMO payment with reference code
    When I record MOMO payment of 5000000 VND with reference "MOMO123456"
    Then payment should include reference code "MOMO123456"
    And system should validate MOMO reference format

  Scenario: Reject payment greater than invoice amount
    When I try to record payment of 6000000 VND for 5000000 VND invoice
    Then system should reject with error "Payment cannot exceed invoice amount"
