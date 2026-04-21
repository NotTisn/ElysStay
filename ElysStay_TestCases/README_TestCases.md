# ElysStay Test Case Documentation

This directory provides the complete test case specifications for the ElysStay Rental Management application. The test cases have been meticulously designed following the provided reference format and thoroughly cover all modules in the ElysStay application.

## Project Information
*   **Project Name:** ElysStay - Rental Management
*   **Test Environment:** .NET 10 REST API, PostgreSQL, xUnit, SpecFlow
*   **Team Members:**
    *   Võ Trung Tín - 23521595
    *   Từ Thị Hồng Phúc - 2252xxxx (Note: Verify ID)
    *   Phan Phú Thọ - 23521520

## File Structure

To make it easy to import into an Excel workbook (each file acts as a separate sheet), the test documentation is broken down into the following CSV files:

1.  **`00_Test_Statistics.csv`**: Contains the testing coverage overview and execution counts.
2.  **`01_User_Auth.csv`**: Signup, Login, Profile updates, and Role Management.
3.  **`02_Property_Room.csv`**: Managing buildings, adding rooms, updating statuses (Available, Maintenance).
4.  **`03_Reservation.csv`**: Searching available rooms, placing booking reservations, handling cancellations.
5.  **`04_Contract.csv`**: Converting reservations to active contracts, terminating contracts, and handing deposits.
6.  **`05_Meter_Reading.csv`**: Adding utility readings (water/electricity) and auto-calculating consumption.
7.  **`06_Invoice_Billing.csv`**: Generating draft invoices, applying service charges, and marking overdue.
8.  **`07_Payment.csv`**: Tenant making full/partial payments and validating invoice status updates.

*To assemble the final Excel file, open Excel and import each of these CSV files into their respective named sheets.*
