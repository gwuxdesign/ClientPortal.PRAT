@Documents
Feature: Documents page
  As a registered user
  I want to be able to navigate to the Documents page

  Background:
    Given the user navigates to the Client Portal
    And the user is logged in

  # @Smoke
  # Scenario: Documents Page Check
  #   When the user clicks the "My documents" menu link
  #   Then the user navigates to the "My documents" page

  @Smoke
  Scenario Outline: User is able to filter documents
    Given the user clicks the "My documents" menu link
    When the user applies a "<filter>" on the Documents
    Then the user should only see the right "<count>"
    And the "<list>" of documents
    Examples:
      | filter       | count | list                                                                                              |
      | All          | 6     | Test, Mortgage Provider List, Making more of your money, Defect106224HO, COB Booklet, COB Booklet |
      | Read         | 2     | Mortgage Provider List, Making more of your money                                                 |
      | Unread       | 3     | Defect106224HO, COB Booklet, COB Booklet                                                          |
      | My uploads   | 1     | Test                                                                                              |
      | Newest first | 6     | Test, Mortgage Provider List, Making more of your money, Defect106224HO, COB Booklet, COB Booklet |
      | Oldest first | 6     | COB Booklet, COB Booklet, Defect106224HO, Making more of your money, Mortgage Provider List, Test |
      | Title (A-Z)  | 6     | COB Booklet, COB Booklet, Defect106224HO, Making more of your money, Mortgage Provider List, Test |
      | Title (Z-A)  | 6     | Test, Mortgage Provider List, Making more of your money, Defect106224HO, COB Booklet, COB Booklet |