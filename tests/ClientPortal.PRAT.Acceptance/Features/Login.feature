@Login
Feature: Login
  As a registered user
  I want to be able to log in and log out

  Background:
    Given the user navigates to the Client Portal

  @Smoke @Login
  Scenario Outline: Login functionality with validation
    Given the user enters "<login>" credentials
    When the user submits the login form
    Then the login attempt was "<status>"
    Examples:
      | login   | status       |
      | valid   | successful   |
      | invalid | unsuccessful |

  @Smoke
  Scenario Outline: Login field validation
    Given the user clears the cookie pop up
    When the user submits an "<error>" in the login form
    Then the user should see validation messages for "<validation>"
    Examples:
      | error   | validation  |
      | empty   | empty login |
      | invalid | bad email   |

  @Smoke @Sample
  Scenario: Logout Functionality
    Given the user is logged in
    When the user clicks on the logout button
    Then the user navigates to the "logout" page