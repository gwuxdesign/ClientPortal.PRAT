@Cookies
Feature: Cookie Consent
  As a registered user
  To comply with regulations
  All information on the cookie policies are available

  @Smoke
  Scenario: Cookie policy appears for fresh browsers
    Given the user navigates to the Client Portal
    When the cookie policy pop-up appears
    Then the user can accept or reject it

  # Rule: Cookie page
  #   Background:
  #     Given the user navigates to the Client Portal

  # @Smoke
  # Scenario: Cookie policy page exists
  #   Given the user is logged in
  #   When the user clicks the "Cookie policy" menu link
  #   Then the user navigates to the "Cookie policy" page
