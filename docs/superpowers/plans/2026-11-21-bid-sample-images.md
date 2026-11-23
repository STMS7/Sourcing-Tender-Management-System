# Bid sample images implementation plan

**Goal:** Allow suppliers to attach sample photographs to an offer and view their own images.

**Architecture:** Separate private image storage from the existing PDF store. Save image metadata in the already prepared BidSampleImages table inside the offer transaction. Keep existing monetary fields and migrations unchanged.

**Constraints:** No migration creation or application; the user performs those manually. No new packages. Maximum five optional JPEG/PNG images, 5 MB each, within the existing 30 MB request limit. Preserve PDF attachments and existing role and ownership checks.

- [x] Write executable checks for image signatures, limits, path traversal, storage cleanup and EF relationships; run them before implementation.
- [x] Add Services/SupplierBidExtras/SampleImageStore.cs, register it, and add optional uploads to SupplierOfferViewModel.
- [x] Validate before saving, save metadata and clean files on transaction failure in SuppliersController; provide an ownership-filtered image action.
- [x] Add accessible upload and gallery partials to Offer and OfferDetails without altering the shared Bid model.
- [x] Run checks, build and inspect the rendered partials in a browser at desktop and mobile sizes. Report limitations of fixture-only browser checks.

Verification: 18 executable checks and 16 browser checks passed. EF model matches the existing migration snapshot. Browser checks render the production partials with synthetic data at 1280 and 375 px. No database was connected, created or migrated. End-to-end submission, transaction rollback and cross-supplier access still need verification against a user-prepared test database.
