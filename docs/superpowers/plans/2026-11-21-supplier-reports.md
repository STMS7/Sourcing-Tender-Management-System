# Supplier reports implementation plan

Goal: Add an Admin-only supplier report with registration, verification and document filters, expiry tracking, participation and award counts, and PDF/Excel downloads while preserving existing reports.

Architecture: Keep new read-only queries and DTOs in Services/SupplierBidExtras and Models/SupplierBidExtras. Add SupplierReportsController, Razor views and scoped styles. Reuse the existing exporter. No migrations or database changes. Paginate all screens. Downloads explicitly contain the current filtered supplier page; KPI totals cover all matching suppliers.

User tasks: identify document problems, inspect documents and export the displayed page. Keep the existing olive/Bootstrap visual language, labelled filters, textual statuses, contained table scrolling and empty/error states.

- [x] Test expiry boundaries, missing dates, filters, invalid dates, participation, authorization, SQL translation and export contents.
- [x] Implement DTOs, read-only service and server pagination.
- [x] Add Admin controller, validation, exports, service registration and supplier-page link.
- [x] Add accessible views and scoped light/dark styles.
- [x] Build and run executable/export checks and synthetic browser checks at mobile, tablet and desktop widths without touching the database.

The shared PDF exporter did not support Arabic in the installed QuestPDF version. Added a feature-local PDF exporter using an embedded DejaVu Sans font with its original license; Excel still reuses the shared exporter. No shared report files were modified.

Verification uses synthetic fixtures and model SQL translation, not an actual database connection. Full production SQL execution, application shell rendering and session authorization must still be checked on the user's test database. Browser previews use the real report partials, but fixture-owned queries and routes; they do not prove the controller/database integration.
