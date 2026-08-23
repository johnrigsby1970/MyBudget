Tool to manage personal budget, with envelopes mixed with known bills to project usage over longer term across multiple accounts and account types.

# Stay On Target

Stay On Target is a local-first, personal envelope budgeting and financial forecasting desktop application designed for Windows. It helps you allocate funds into named categories (envelopes), track expected versus actual bills, and project your financial health across multiple accounts over the long term.

## Privacy Policy

**Your privacy is absolute.** Stay On Target operates on a **local-first architecture**.

* **No Cloud Storage or Data Harvesting:** Your financial data, budgets, and transaction histories are stored locally on your device inside an encrypted SQLite database.
* **Data We Do Not Collect:** We do not collect names, account numbers, passwords, or any identifiable personal information.
* **Imported Transaction Data:** When you import bank or credit card statements, transaction descriptions are processed locally on your machine to normalize merchant names and map categories. While transaction records contain vendor names and amounts, **this data never leaves your local device**, and we do not transmit, store, or have access to your financial records.
* **Crash Reporting & Telemetry:** If error tracking (such as Sentry) is utilized, it is strictly limited to technical crash diagnostics and application exceptions to improve stability. No financial data, transaction amounts, or personal identifiers are ever included in telemetry logs.

## Dependencies

* Built for Windows 10 and above.
* Powered by the Microsoft .NET runtime.

## Description

The application allocates money from accounts into named buckets/categories (envelopes) and tracks expected vs. actual amounts per period, which is the core concept of envelope budgeting:

1. **Budget Bucket** - represents named budget categories with expected amounts, which are the "envelopes"
2. **Period Bucket** - tracks actual amounts per period for each bucket, allowing you to allocate money into envelopes each pay period
3. **Account linkage** - buckets can be linked to specific accounts, enabling envelope-style allocation of funds

## Getting Started

### Installing via Microsoft Store (Recommended)
* Stay On Target is available directly through the Microsoft Store for secure installation, automatic updates, and seamless system integration.

### Installing via GitHub Releases (Portable / Manual)
* Download the latest `StayOnTarget-Portable-win-x64.zip` from the GitHub Releases page.
* Extract the zip file to a directory of your choice on your computer.
* Locate and run `StayOnTarget.exe`.

## Authors

John Rigsby

## Version History

* **v0.0.1.0** - Initial release, featuring core envelope budgeting, local SQLite database storage, and bank file importing.

## License

Stay On Target © 2026 by John Rigsby. All rights reserved.

This software is proprietary commercial software. You may view the source code in this repository for educational and auditing purposes, but you are strictly prohibited from redistributing, modifying, decompiling for malicious use, or selling commercial derivatives of this software without explicit written permission from the author.
