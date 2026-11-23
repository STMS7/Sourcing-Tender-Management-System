# Sample image checks

Run from the repository root:

```powershell
dotnet run --project tests/SupplierBidExtras.Checks
```

This checks storage, format structure, size/count restrictions, path traversal, deletion and EF model metadata. It does not connect to SQL Server or apply migrations. The SQL ownership check verifies query translation only.

To preview the actual upload/gallery partials with synthetic data and no database:

```powershell
dotnet run --project tests/SupplierBidExtras.Checks -- --preview --urls http://127.0.0.1:5198
```

Open http://127.0.0.1:5198/SamplePreview. The preview image endpoint is synthetic and belongs only to this separate test executable. It is not registered by the production app.

With Playwright available to Node and Microsoft Edge installed:

```powershell
node tests/SupplierBidExtras.Checks/browser-checks.cjs
```

The script checks both desktop and mobile widths and writes screenshots to the system temporary directory. It does not submit an offer.

After the user applies the existing migration to their test database, verify in the real application:

1. Submit an offer without images and confirm existing PDF behavior is unchanged.
2. Submit another offer with JPEG and PNG images and verify its gallery after reload.
3. Reject six images, an image larger than 5 MB, renamed PDFs and truncated images.
4. Sign in as another supplier and request an image URL from the first supplier; expect 404. An unauthenticated request must require login.
5. On an intentionally failed test submission, confirm neither metadata nor uploaded images remain.

No migration commands are run by these checks.

## Supplier reports

The executable also checks report date boundaries, document filters, award counting, Admin authorization metadata, EF query translation, and actual Excel/PDF contents. Arabic font support is exercised in PDF generation.

With the same preview server running, open `/SupplierReports/Index` or run:

```powershell
node tests/SupplierBidExtras.Checks/report-browser-checks.cjs
```

This renders production report partials using synthetic supplier/document rows, tests filters and paging, and downloads real Excel/PDF files. It checks widths 375, 768, 1280 and 1920 in light/dark mode, input labels, keyboard reachability and primary-button contrast. Download signatures are checked to distinguish PDF from Excel.

The preview does not authenticate users or run production controller queries. On a user-prepared test database, confirm supplier/summary counts against the database, document paging and downloads, and that non-Admin accounts cannot open report, document or export routes. Export scope is the current filtered page, explicitly labelled on screen. No report needs a new migration.
