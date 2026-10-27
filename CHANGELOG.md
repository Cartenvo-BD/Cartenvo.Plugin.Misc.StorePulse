# Changelog

Versions are `<nopCommerce version>.<release>`, so release 10 is `4.80.10` on nopCommerce 4.80 and `4.90.10` on 4.90.

## 13

- **Before you upgrade to 5.00.** When the target on the Upgrade readiness page is 5.00 or later, a new list shows what in this store needs work: forums, news and polls (they move into plugins), the Manual payment method (no longer included), card details stored in orders, email templates that use the old `%Order.CustomValues%` token, plugin code that uses APIs 5.00 changes (AutoMapper, `RemotePost.Post()`, attribute `PictureId`, forum/news/poll classes), the theme, ASP.NET Core 10 on the server, and old attribute picture links that the 5.00 upgrade would copy a second time.
- The list is read when the page opens. Nothing is stored or changed, and it doesn't affect the health score. Card details are only counted, never read. Plugin files are read as metadata and never loaded.
- Each item links to the admin page where it's fixed, and the card links to the nopCommerce 5.0 upgrade guide on cartenvo.com.
- **nopCommerce 5.00 build.** The same source now also builds `5.00.13` for nopCommerce 5.00 on .NET 10 (IWebHelper's new namespace, linq2db 6 async methods, migration helpers instead of the removed synchronous service methods). The 5.00 build has no readiness list, since the store is already on 5.00.
- Tested on 4.80 with SQL Server (readiness list with seeded data) and on a fresh 5.00.0 store with SQL Server (install, scan, catalog lists, upgrade page, settings, dashboard card).

## 12

- **Catalog audit.** 13 new catalog checks, for 19 in total: products without SKU, GTIN or part number, manufacturer, description or URL name; sold-out products that can't be ordered; published products past their end date (customers get a 404); old prices lower than the price (shown as a price rise); products no store or customer role can see; empty categories; categories and manufacturers without meta description; duplicate meta descriptions; reviews waiting more than 7 days for approval.
- **Show affected items.** Each catalog finding with a problem can list the first 50 products, categories, manufacturers or reviews behind it, with links to edit them. The list is read when you open it and is never stored in scan history or reports.
- **Choose catalog checks.** Settings has a checkbox for each catalog check, so stores that don't use SKUs or GTINs can turn those off.
- "Products without meta description" now follows nopCommerce: when "Generate product meta description" is on, products with a short description are no longer counted.
- Catalog checks use only fields that are the same in nopCommerce 4.80, 4.90 and 5.00. Tested on 4.80 with SQL Server and 4.90 with PostgreSQL, including a 100,000-product catalog (full scan 3.6 s).

## 11

- **Help popup.** "Learn more" on each finding opens a popup inside the plugin: what the check looks at, why it matters, common causes, steps to fix it and when it's safe to ignore. The texts are locale resources, so stores can translate them.
- **Full guides on cartenvo.com.** The popup links to a guide for the check at `cartenvo.com/plugins/storepulse/guides/`. Links carry `utm_source=storepulse` and the check id, nothing about the store. Set `StorePulseLinks.GuidesPublished` to false to hide them.
- The HTML report links each problem to its guide.
- Plugin logo (`logo.png`, 120 × 120) shown in Configuration → Local plugins. `logo.svg` is its source.
- Corrected the advice for emails linked to a deleted account: queued emails can't be moved to another account, so delete them and trigger them again.

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
