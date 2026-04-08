@PasswordReset
Feature: Password Reset
  As a registered user
  I want to be able to reset my password at any time

  Background:
    Given the user navigates to the Client Portal
    And the user clears the cookie pop up
    When the user navigates to the "password reset" page

  @Smoke
  Scenario Outline: Password reset validation
    When the user submits an "<error>" in the reset form
    Then the user should see validation messages for "<validation>"
    Examples:
      | error   | validation         |
      | empty   | email required     |
      | invalid | bad email password |

  @Smoke
  Scenario: Cancel Password Reset
    When the user uses the "password back" button
    Then the user navigates to the "login" page

  @Smoke
  Scenario: Password Reset
    When the user submits a valid email
    Then the user is presented with a confirmation message