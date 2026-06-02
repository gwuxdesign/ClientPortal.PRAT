@Navigation
Feature: Navigation
  As a registered user
  I want to be able to verify all pages exist and are navigable

  Background:
  Given the user navigates to the Client Portal

  @Smoke
  Scenario Outline: Navigation after login
    Given the user is logged in
    When the user clicks the "<page>" menu link
    Then the user navigates to the "<page>" page
    Examples:
    | page                 |
    | My notifications     |
    | My profile           |
    | My documents         |
    | Privacy policy       |
    | Terms and conditions |
    | Cookie policy        |

Rule: Cookie page
  Background:
    Given the user clears the cookie pop up

  @Smoke
  Scenario: Navigation password reset
    Then the user navigates to the "Password reset" page

  @Smoke
  Scenario: Navigation login page
    Then the user navigates to the "Login" page