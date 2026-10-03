## T-01 Salary or receipt round-tripping

A credit that looks like income is sent back to the payer, or to someone linked to the payer, within a few days. The applicant's account shows a healthy inflow, but the money never stays. Common where an associate or a friendly business "pays a salary" for the length of the statement period.

### Red flags
- Debit to the same account or name as a recent large credit, within 72 hours
- Returned amount close to the credit, often less a small "fee"
- The payer is not the employer named on the salary slip

### Detected by
`SALARY_ROUNDTRIP` — deterministic match on counterparty and timing.

## T-02 Window dressing before the application

Large deposits land in the weeks before the loan application and leave shortly after, so the average balance reads several times higher than the account's normal level. Average-balance tests are the target.

### Red flags
- Average balance in the last 15 days over twice the prior 75-day average
- One or two credits from unrelated parties account for the rise
- The same money leaves within ten days, often to the original sender

### Detected by
`PRE_APPLICATION_WINDOW_DRESSING`.

## T-03 Edited bank statement

A genuine statement is altered in a PDF editor: an amount, a balance or a payer name is changed to inflate income or hide a liability. The edit rarely survives arithmetic — the running balance stops following from the transactions, page totals stop matching their rows, and the file's metadata names a consumer editor.

### Red flags
- Running balance that does not equal previous balance plus credit minus debit
- Closing balance of one page differs from the opening balance of the next
- Stated page totals that do not sum the rows
- Producer or creator is a word processor or PDF editor rather than a bank system
- Modified date long after the created date
- A row in a different font, weight or alignment from its neighbours

### Detected by
`BALANCE_DISCONTINUITY`, `PAGE_CARRY_MISMATCH`, `TOTALS_MISMATCH`, `METADATA_ANOMALY`, `VISUAL_INCONSISTENCY`.

## T-04 Fabricated salary slip

A salary slip produced from a template rather than a payroll system. Figures are typed in, so gross pay, deductions and net pay frequently fail to reconcile, statutory deductions (PF, professional tax, TDS) are missing for an employer that would have to make them, and the employer often cannot be found on the company register.

### Red flags
- Gross pay less deductions does not equal net pay
- No PF or TDS on a salary where they would be mandatory
- Employer not on the MCA register, or struck off
- Net pay on the slip differs from the salary credit on the statement

### Detected by
`SLIP_ARITHMETIC_ERROR`, `IMPLAUSIBLE_EMPLOYER`, `INCOME_MISMATCH`.

## T-05 Mule or pass-through account

The account receives money and forwards it almost at once. It has turnover but no working balance, which inflates apparent business volume while hiding that the funds belong to someone else.

### Red flags
- Over 80% of credits leave within a day of arriving
- Many unrelated senders, few recipients — or the reverse
- Balance returns to near zero after each cycle

### Detected by
`PASS_THROUGH`.

## T-06 Undeclared EMIs and loan stacking

The applicant declares few or no existing loans, but the statement shows recurring debits to lenders and NBFCs, or several fresh disbursals within weeks — applying to many lenders at once before any bureau report reflects the new loans.

### Red flags
- Recurring fixed-amount debits on the same date each month to a lender or NACH mandate
- Two or more loan disbursals in the last 60 days
- Declared EMIs well below the EMIs visible on the statement

### Detected by
`UNDECLARED_EMI`, `LOAN_STACKING`.

## T-07 Turnover inflated against tax filings

Business turnover on the bank statement or in the application is far above what the GST returns and the filed ITR report. Compare like with like: GST turnover against ITR business turnover, never against ITR gross total income, which is closer to profit.

### Red flags
- Bank credits several times declared GST turnover
- ITR business turnover a fraction of the turnover claimed
- Uploaded ITR copy differs from the ITR fetched from the source

### Detected by
`INCOME_MISMATCH`, `ITR_MISMATCH`, `STATEMENT_VS_AA_MISMATCH`.

## T-08 Shared templates and fraud rings

One fabricator produces documents for many applicants. The same statement or slip template, the same phone number, address or salary-payer account recurs across applications that claim to be unrelated.

### Red flags
- Byte-identical file uploaded in two applications
- Same layout and fonts across documents claiming different issuers
- Phone, email, address or device shared between applicants

### Detected by
`SHARED_TEMPLATE`, `SHARED_ENTITY`, `FRAUD_RING`.

## T-09 Gambling and crypto exposure

Regular payments to betting, fantasy-gaming or crypto platforms. Not fraud in itself, but a credit risk the policy usually caps, and a common reason applicants edit statements.

### Red flags
- Payments to known betting or crypto merchants
- Rising frequency in the months before the application

### Detected by
`GAMBLING_CRYPTO_EXPOSURE`.

## T-10 Cheque and NACH bounces

Returned cheques and failed ECS or NACH debits show an account that cannot meet its obligations. Applicants with frequent bounces are the ones most tempted to submit an edited statement.

### Red flags
- "RETURN", "INSUFFICIENT FUNDS" or bounce charges in the narration
- Bounce charges debited without a matching return line, suggesting the return was removed

### Detected by
`BOUNCES`.
