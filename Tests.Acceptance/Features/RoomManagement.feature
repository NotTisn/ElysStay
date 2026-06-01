Feature: Room Management

Scenario: Add a new room to a building
    Given a property owner exists
    And a building named "Elysian Tower" exists
    When the owner adds a room "201" with rent 6000000 VND
    Then the room should be saved successfully
    And the room should be available for rent
