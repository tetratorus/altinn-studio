# Changelog

All notable changes to the Altinn.FileAnalyzers package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
Entries should describe only user-facing functionality in clear, user-friendly language; omit implementation details that do not affect how people use the product.
Section ordering: Added, Changed, Fixed, Removed, Security, Deprecated.

## [Unreleased]

### Fixed

- The MIME type validator now accepts `.txt` and `.csv` files whose content is plain text (UTF-8 with or without a byte order mark, UTF-16 with a byte order mark, or Windows-1252), when `text/plain` or `text/csv` is an allowed content type. A `.csv` file is now recognized as `text/csv` also when it starts with a byte order mark, as Excel writes it. Files with other binary content renamed to `.txt` or `.csv` are still rejected.

## [9.0.0-preview.1] - 2026-09-09

### Changed

- Altinn.FileAnalyzers now builds on version 9 of the Altinn app libraries and targets .NET 10. Upgrade your app to `Altinn.App.Core` 9 before taking this version; apps still on version 8 should stay on Altinn.FileAnalyzers 8.0.0.
