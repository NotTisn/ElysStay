Feature: Monthly Invoice Calculation
  As a Building Owner
  I want invoices calculated correctly with room rent + service charges + penalties - discounts
  So I get paid fairly and tenants can verify billing

  Background:
    Given a building owner with email "owner@test.com"
    And a building named "Test Building" owned by the owner
    And a room "101" in the building with rent 5000000 VND per month
    And a tenant with email "tenant@test.com"
    And an active contract between tenant and room "101"

  Scenario: Calculate invoice with room rent only
    When I generate invoice for March 2026
    Then the invoice should have:
      | RoomAmount   | 5000000   |
      | ServiceAmount| 0         |
      | TotalAmount  | 5000000   |
    And invoice status should be "Unpaid"

  Scenario: Calculate invoice with room rent and water charges
    Given water service enabled with unit price 10000 VND/m³
    And meter reading for room "101": previous 100m³, current 110m³
    When I generate invoice for March 2026
    Then the invoice should have:
      | RoomAmount    | 5000000   |
      | ServiceAmount | 100000    | # (110-100)*10000
      | TotalAmount   | 5100000   |

  Scenario: Apply penalty and discount to invoice
    When I generate invoice for March 2026 with penalty 500000 and discount 100000
    Then the invoice should have:
      | RoomAmount     | 5000000    |
      | PenaltyAmount  | 500000     |
      | DiscountAmount | 100000     |
      | TotalAmount    | 5400000    | # 5000000 + 500000 - 100000
