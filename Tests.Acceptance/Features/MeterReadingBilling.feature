Feature: Meter Reading and Utility Billing
  As a Building Manager
  I want to record meter readings and calculate utility charges based on consumption
  So tenants pay accurately for actual usage

  Background:
    Given a building owner
    And a building with water and electricity services
    And a room with active contract
    And both services enabled for the room

  Scenario: Calculate water charges from meter reading
    When I record meter reading for water: previous 500m³, current 520m³ (month 3/2026)
    And I generate invoice for month 3/2026
    Then the invoice should include water charge:
      | Consumption    | 20      |
      | Unit Price     | 10000   |
      | Water Charge   | 200000  | # 20 * 10000

  Scenario: Calculate electricity charges from meter reading
    When I record meter reading for electricity: previous 1000kWh, current 1150kWh (month 3/2026)
    And electricity unit price is 2500 VND/kWh
    And I generate invoice for month 3/2026
    Then the invoice should include electricity charge:
      | Consumption       | 150    |
      | Unit Price        | 2500   |
      | Electricity Charge| 375000 | # 150 * 2500

  Scenario: Prevent duplicate meter reading for same month and service
    Given meter reading already exists for water in March 2026
    When I try to record another meter reading for same water in March 2026
    Then system should reject with error "Meter reading already exists for this month"
