Feature: Contract Lifecycle and Deposit Management
  As a Tenant
  I want to understand contract status and get my deposit refunded when contract ends
  So I have peace of mind about my rental agreement

  Background:
    Given a building owner
    And a building with available room
    And a tenant
    And a deposit amount of 10000000 VND

  Scenario: Contract starts as Active with Unpaid deposit
    When I create contract for tenant
    Then contract status should be "Active"
    And deposit status should be "Unpaid"

  Scenario: Transition from Unpaid to Paid deposit
    Given an active contract with unpaid deposit
    When I record payment of 10000000 VND for deposit
    Then deposit status should be "Paid"
    And invoice status should be "Paid"

  Scenario: Terminate contract and refund deposit
    Given an active contract with paid deposit of 10000000 VND
    When I terminate contract with reason "Tenant moved out"
    Then contract status should be "Terminated"
    And termination date should be set
    And system should generate refund payment for 10000000 VND
    And deposit status should be "Refunded"

  Scenario: Prevent terminating already terminated contract
    Given a terminated contract
    When I try to terminate contract again
    Then system should reject with error "Contract is already terminated"
