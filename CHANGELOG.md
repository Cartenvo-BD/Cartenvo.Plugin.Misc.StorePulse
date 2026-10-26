# Changelog

Versions are `<nopCommerce version>.<release>`, so release 10 is `4.80.10` on nopCommerce 4.80 and `4.90.10` on 4.90.

## 10

- **nopCommerce 4.80 support.** One source tree now builds for 4.80 and 4.90. Tested on a fresh 4.80.9 store with SQL Server: install, scan, upgrade readiness, settings, scheduled scans, the dashboard card and both report formats.
- **Large stores.** Size checks stop counting at their warning level and show the result as a minimum (for example "100,000+"). Each check now has a 10-second limit, and a check that runs out of time is cancelled and shows "Couldn't check", while the rest of the scan finishes.
- **Faster log checks.** Log checks read only the recent part of the Log table. On SQL Server, where nopCommerce indexes the log by date only, a cold-cache scan of a 7 GB store went from 16.4 s to 5.6 s.
- The upgrade readiness page always offers 4.90 as a target.

Performance was measured on PostgreSQL 18 (8.5 GB: 3 million guests, 3 million log records, 5 million activity log records, 1 million queued emails) and SQL Server 2022 Express (7 GB, same volumes):

| | Before | After |
|---|---|---|
| Full scan, PostgreSQL, cold cache | over 30 s; later checks skipped | 4.8 s |
| Full scan, SQL Server, cold cache | 16.4 s | 5.6 s |
| A table locked by another process | scan waits | that check is cancelled after 10 s |

## 8

- 22 new checks based on nopCommerce GitHub issues and forum threads, for 39 in total: database growth, email account setup, SSL certificate expiry, nopCommerce system warnings, security basics, plugin load failures, known nopCommerce bugs and catalog quality.
- New dashboard: score ring, status filters, "Fix these first", "Since the previous scan", score trend, and an "Open page" link for each finding.
- Upgrade readiness page and an admin home page card.
- Redesigned HTML report.

## 7

- Optional scheduled scans for each store, off by default, every 1–168 hours.

## 6

- Checks run through a shared runner that isolates failures. Checks skipped because the scan ran out of time show "Couldn't check".

## Earlier

- First checks for scheduled tasks, the email queue, errors, store URL and HTTPS, and plugin compatibility. Scan history, JSON and HTML reports, and permissions.
