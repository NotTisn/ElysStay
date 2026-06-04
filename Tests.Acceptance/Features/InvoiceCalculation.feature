Feature: Monthly Invoice Calculation
  As a Building Owner
  I want invoices calculated correctly
  So I get paid fairly and tenants can verify billing

  Background:
    Given a building owner with email "owner@test.com"
    And a building named "Test Building" owned by the owner
    And a room "101" in the building with rent 5000000 VND per month
    And a tenant with email "tenant@test.com"
    And an active contract between tenant and room "101"
    And room "101" has 1 occupant


# ======================================================
# Rent Calculation
# ======================================================

  @Rent
  Scenario: Calculate full monthly rent
    Given the contract is active for the entire month of March 2026
    When I generate invoice for March 2026
    Then the invoice should contain:

      | Field        | Value   |
      | ActiveDays   | 31      |
      | RoomAmount   | 5000000 |
      | TotalAmount  | 5000000 |

    And invoice status should be "Draft"


  @Rent
  Scenario: Calculate prorated rent for first month
    Given the contract starts on "2026-03-16"
    When I generate invoice for March 2026
    Then the invoice should contain:

      | Field        | Value   |
      | ActiveDays   | 16      |
      | RoomAmount   | 2580645 |


  @Rent
  Scenario: Calculate prorated rent for last month
    Given the contract ends on "2026-03-10"
    When I generate invoice for March 2026
    Then the invoice should contain:

      | Field        | Value   |
      | ActiveDays   | 10      |
      | RoomAmount   | 1612903 |

  @Rent
    Scenario: Full rent applied when contract spans multiple months
      Given the contract starts on "2026-02-15"
      And the contract ends on "2026-04-20"
      When I generate invoice for March 2026
      Then the invoice should contain:
      | Field | Value |
      | ActiveDays | 31 |
      | RoomAmount | 5000000 |
      And rent should not be prorated

  @Rent
    Scenario: Ensure no off-by-one error for full month calculation
      Given the contract is active from "2026-03-01" to "2026-03-31"
      When I generate invoice for March 2026
      Then the invoice should contain:
      | Field | Value |
      | ActiveDays | 31 |
      | RoomAmount | 5000000 |
      And rent should not be prorated
  @Rent
  Scenario: Calculate prorated rent when contract starts and ends in same month
    Given the contract starts on "2026-03-10"
    And the contract ends on "2026-03-15"
    When I generate invoice for March 2026
    Then the invoice should contain:

      | Field        | Value |
      | ActiveDays   | 6     |
      | RoomAmount   | 967742 |


  @Rent
  Scenario: Return validation error when active days are invalid
    Given active days is -1
    When I generate invoice for March 2026
    Then validation error should be displayed
    And no invoice should be created


  @Rent
  Scenario: Return validation error when monthly rent is missing
    Given room "101" has no monthly rent configured
    When I generate invoice for March 2026
    Then validation error should be displayed
    And no invoice should be created


# ======================================================
# Service Calculation
# ======================================================

  @Service
  Scenario: Calculate service amount using occupant count
    Given internet service enabled with unit price 100000 VND
    And service quantity is based on occupant count
    And room "101" has 3 occupants
    When I generate invoice for March 2026
    Then the invoice should contain:

      | Field             | Value   |
      | ServiceQuantity   | 3       |
      | ServiceAmount     | 300000  |
      | TotalAmount       | 5300000 |


  @Service
  Scenario: Calculate service amount using override quantity
    Given cleaning service enabled with unit price 50000 VND
    And override quantity is 2
    When I generate invoice for March 2026
    Then the invoice should contain:

      | Field             | Value |
      | ServiceQuantity   | 2     |
      | ServiceAmount     | 100000 |


  @Service
  Scenario: Use default quantity when quantity source does not exist
    Given parking service enabled with unit price 200000 VND
    And service quantity source does not exist
    And override quantity does not exist
    When I generate invoice for March 2026
    Then the invoice should contain:

      | Field             | Value |
      | ServiceQuantity   | 1     |
      | ServiceAmount     | 200000 |


  @Service
  Scenario: Calculate metered service using override unit price
    Given water service enabled with unit price 10000 VND
    And override unit price is 15000 VND
    And meter reading for room "101":

      | Previous | Current |
      | 100      | 110     |

    When I generate invoice for March 2026
    Then the invoice should contain:

      | Field             | Value |
      | Consumption       | 10    |
      | UnitPrice         | 15000 |
      | ServiceAmount     | 150000 |


  @Service
  Scenario: Create service item with zero consumption
    Given electricity service enabled with unit price 3500 VND
    And meter reading for room "101":

      | Previous | Current |
      | 1000     | 1000    |

    When I generate invoice for March 2026
    Then the invoice should contain:

      | Field             | Value |
      | Consumption       | 0     |
      | ServiceAmount     | 0     |


  @Service
  Scenario: Skip service item when room service is disabled
    Given water service enabled with unit price 10000 VND
    And water service is disabled for room "101"
    When I generate invoice for March 2026
    Then no service item should be created


  @Service
  Scenario: Show warning when meter reading is missing
    Given electricity service enabled with unit price 3500 VND
    And no meter reading exists for room "101"
    When I generate invoice for March 2026
    Then warning "Thiếu chỉ số đồng hồ" should be displayed
    And service item should not be created
  @Service
  Scenario: Override unit price takes priority over default service price
    Given water service enabled with unit price 10000 VND
    And override unit price is 15000 VND
    And room "101" has 10 consumption units
    When I generate invoice for March 2026
    Then the invoice should contain:
    | Field | Value |
    | UnitPrice | 15000 |
    | ServiceAmount | 150000 |

  @Service
  Scenario: Override quantity takes priority over occupant count
    Given cleaning service enabled with unit price 50000 VND
    And room "101" has 5 occupants
    And override quantity is 2
    When I generate invoice for March 2026
    Then the invoice should contain:
    | Field | Value |
    | ServiceQuantity | 2 |
    | ServiceAmount | 100000 |

  @Service
    Scenario: Use occupant count when override quantity is missing
    Given internet service enabled with unit price 100000 VND
    And room "101" has 3 occupants
    When I generate invoice for March 2026
    Then the invoice should contain:
    | Field | Value |
    | ServiceQuantity | 3 |

# ======================================================
# Service Validation
# ======================================================

  @Service
  Scenario: Return validation error for invalid override unit price
    Given override unit price is -100
    When I generate invoice for March 2026
    Then validation error should be displayed
    And no invoice should be created


  @Service
  Scenario: Return validation error for invalid override quantity
    Given override quantity is -1
    When I generate invoice for March 2026
    Then validation error should be displayed
    And no invoice should be created


  @Service
  Scenario: Return validation error for invalid meter consumption
    Given water service enabled with unit price 10000 VND
    And meter reading for room "101":

      | Previous | Current |
      | 100      | 90      |

    When I generate invoice for March 2026
    Then validation error should be displayed
    And no invoice should be created
  
  @Service
    Scenario: Meter reset is handled correctly
      Given previous meter reading is 1000
      And new meter is replaced with reading 0
      When I generate invoice
      Then system should create a new meter baseline or flag error

    @Service
    Scenario: Duplicate meter readings should be ignored
      Given multiple meter readings exist for same period
      When I generate invoice
      Then only latest valid reading should be used


# ======================================================
# Invoice Generation
# ======================================================

  @Invoice
  Scenario: Generate invoice successfully
    Given no invoice exists for room "101" in March 2026
    When I generate invoice for March 2026
    Then an invoice should be created
    And invoice status should be "Draft"


  @Invoice
  Scenario: Skip invoice generation when invoice already exists
    Given invoice already exists for room "101" in March 2026
    When I generate invoice for March 2026
    Then invoice generation should be skipped
    And only one invoice should exist


  @Invoice
  Scenario: Invoice generation is idempotent
    Given no invoice exists for room "101" in March 2026
    When I generate invoice for March 2026
    And I generate invoice for March 2026 again
    Then only one invoice should exist

    @Invoice
    Scenario: Calculate invoice with multiple services
      Given electricity service enabled with unit price 3000 VND
      And water service enabled with unit price 10000 VND
      And internet service enabled with unit price 100000 VND
      And room "101" has 2 occupants
      And meter readings exist:
      | Service | Consumption |
      | Electricity | 100 |
      | Water | 10 |
      When I generate invoice for March 2026
      Then the invoice should contain:
      | Field | Value |
      | TotalAmount | 5600000 |
      And invoice should include 3 service items

  @Invoice
    Scenario: Skip missing meter services but still calculate others
      Given electricity service enabled but missing meter reading
      And water service enabled with valid meter reading
      When I generate invoice for March 2026
      Then electricity service should be skipped
      And water service should be included
      And invoice should still be created

  @InvoiceStatus
    Scenario: Send invoice changes status from Draft to Sent
      Given an invoice exists in Draft status
      When owner sends invoice to tenant
      Then invoice status should be "Sent"

  @InvoiceStatus
    Scenario: Partial payment changes invoice to Partially Paid
      Given an invoice is in Sent status
      When tenant pays part of the invoice
      Then invoice status should be "PartiallyPaid"

  @InvoiceStatus
    Scenario: Full payment completes invoice
      Given an invoice is in Sent status
      When tenant pays full amount
      Then invoice status should be "Paid"

  @InvoiceStatus
    Scenario: Overdue is triggered after due date passes
      Given an invoice in Sent status with due date passed 24 hours
      When system runs overdue check job
      Then invoice status should be "Overdue"

  @Payment
    Scenario: Multiple partial payments are accumulated correctly
      Given invoice total is 5,000,000
      When tenant pays 1,000,000
      And tenant pays 2,000,000
      Then total paid amount should be 3,000,000

    @Payment
      Scenario: Overpayment is rejected or capped
        Given invoice total is 5,000,000
        When tenant attempts to pay 6,000,000
        Then payment should be rejected or capped at invoice total

    @Payment
      Scenario: Payment not allowed on Draft invoice
        Given invoice is in Draft status
        When tenant attempts to pay
        Then payment should be rejected

# ======================================================
# Penalty and Discount
# ======================================================

  @Adjustment
  Scenario: Apply penalty and discount
    When I generate invoice for March 2026 with penalty 500000 and discount 100000
    Then the invoice should contain:

      | Field             | Value   |
      | RoomAmount        | 5000000 |
      | PenaltyAmount     | 500000  |
      | DiscountAmount    | 100000  |
      | TotalAmount       | 5400000 |
  