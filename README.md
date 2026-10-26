# StorePulse for nopCommerce

**Free store health check for nopCommerce 4.80 and 4.90, by [Cartenvo](https://cartenvo.com).**

StorePulse scans your store in a few seconds and tells you what's wrong, why it matters and what to do about it. It looks at scheduled tasks, the email queue, errors, database growth, security basics, plugin compatibility, upgrade readiness and catalog quality.

It is **read-only**. A scan changes nothing in your store, runs no tasks, sends no emails and sends no data outside your server.

## Features

- **Health score** with a single list of findings, sorted so the most important problems come first ("Fix these first").
- **Plain-language results.** Each finding explains why it matters, shows the evidence, says what to do and links to the admin page where you can fix it.
- **Since the previous scan** shows new problems and fixed ones, with a score trend over time.
- **Upgrade readiness** lists the installed plugins that don't yet declare support for the nopCommerce version you plan to move to.
- **Dashboard card** on the admin home page with the latest score.
- **Reports** download as JSON or as an HTML page you can share with a client or developer. Reports leave out email addresses, subjects, credentials, URLs and raw error messages.
- **Optional scheduled scans**, off by default.
- **Scan history** for each store, kept for 30 days by default.
- **Large stores** are supported. Size checks stop counting at their warning level, each check has a 10-second limit and a whole scan a 30-second limit. It was tested on 7–8 GB databases with millions of log, customer and email records.

## What it checks

39 checks in 8 areas. A check that can't get enough evidence shows **Couldn't check**. It never counts as a pass.

| Area | Checks |
|---|---|
| Scheduled tasks | Tasks running on time, repeated failures, tasks that never started, invalid intervals |
| Email | Emails waiting to be sent, oldest waiting email, emails that failed 3 times, stalled sending, email account setup, emails linked to a deleted account |
| Errors | Fatal errors in the last 24 hours, sudden increase in errors, the same error repeating, errors in the last 24 hours, how complete the logs are |
| Configuration | Store URL, HTTPS matches the URL, SSL certificate expiry (opt-in), nopCommerce's own System → Warnings |
| Security | Detailed error pages, CAPTCHA on registration, sample admin account still in use |
| Plugins | Plugins that support this version, missing plugin dependencies, plugins that failed to load, known nopCommerce bugs in your version |
| Database | Guest customer records, bot traffic, the Delete guests task, system log size, activity log size, old sent emails, old shopping carts |
| Catalog quality | Products without images, duplicate SKUs, missing meta descriptions, products priced at 0, products without a category, duplicate page URLs |

Catalog quality has its own score and can be turned off. Warning thresholds can be changed in Settings.

## Requirements

- nopCommerce **4.80** or **4.90**.
- SQL Server or PostgreSQL. Both are tested. MySQL is supported by nopCommerce but hasn't been tested with StorePulse yet.

Each build targets one nopCommerce version. Use the 4.80 build on a 4.80 store and the 4.90 build on a 4.90 store.

## Install

1. Get the build for your nopCommerce version, from a release package or [built from source](#build-from-source).
2. Copy the `Misc.StorePulse` folder into your store's `Plugins` folder, or upload the zip in **Configuration → Local plugins**.
3. In **Configuration → Local plugins**, find **StorePulse - Store Health & Diagnostics**, click **Install** and restart the application when asked.
4. Open **System → StorePulse** and click **Run health scan**.

Upgrades apply automatically when the application starts with a newer version.

### Permissions

StorePulse adds four permissions, which administrators get automatically: view StorePulse, run scans, export reports and manage settings. Other roles get them only when you assign them in **Configuration → Access control list**.

### Uninstall

Uninstalling removes StorePulse's settings, texts, permissions, scheduled task, dashboard card and scan history table. Download any reports you want to keep first. StorePulse never changes nopCommerce's own tables.

## Build from source

Clone this repository into `src/Plugins` of a nopCommerce source tree. The tree's version (4.80 or 4.90) decides which version the build targets.

```bash
cd nopCommerce/src/Plugins
git clone https://github.com/Cartenvo-BD/Cartenvo.Plugin.Misc.StorePulse.git
dotnet build Cartenvo.Plugin.Misc.StorePulse/Cartenvo.Plugin.Misc.StorePulse.csproj -c Release
```

The output goes to `src/Presentation/Nop.Web/Plugins/Misc.StorePulse`.

- The build reads the version from the tree's `Nop.Core.csproj`. Pass `-p:NopVersion=4.80` to override it.
- `plugin.json` is a template. The build fills in the plugin version (for example `4.80.10` or `4.90.10`) and the supported nopCommerce version.
- The release number is `PluginRevision` in the `.csproj`. Bump it for every release, because upgrade migrations only run when the version changes.
- Stop the store before rebuilding. Windows locks loaded plugin files.

### Run the tests

```bash
dotnet test Cartenvo.Plugin.Misc.StorePulse/Tests/StorePulse.Core.Tests
dotnet test Cartenvo.Plugin.Misc.StorePulse/Tests/StorePulse.Adapter.Tests
```

### Repository layout

| Folder | Contents |
|---|---|
| `/` | The nopCommerce plugin: controllers, views, services and migrations |
| `StorePulse.Core/` | The shared engine: check runner, scoring, rules and HTML report. It doesn't depend on nopCommerce |
| `Tests/StorePulse.Core.Tests` | Engine tests |
| `Tests/StorePulse.Adapter.Tests` | Plugin tests: scanner, history, permissions, scheduling, lifecycle |

## Known limitations

- Some checks look at data shared by every store in a multi-store setup, such as tasks, the email queue, logs and plugins. Those results can't be broken down by store.
- The scan lock and the 30-second cooldown between scans apply within one application instance. Web farms with several instances aren't supported yet.
- Error counts come from what nopCommerce kept in its log. If logging is off or the log was cleared, StorePulse can't see those errors.
- Scheduled scans use the nopCommerce scheduler, so they stop if the scheduler stops. Run a manual scan to check.
- StorePulse never fixes anything automatically.

See [CHANGELOG.md](CHANGELOG.md) for the version history.

## Support

StorePulse is made by **Cartenvo**, a nopCommerce development company.

- Website: [cartenvo.com](https://cartenvo.com)
- Email: [contact@cartenvo.com](mailto:contact@cartenvo.com)

Report bugs and request features in [GitHub Issues](https://github.com/Cartenvo-BD/Cartenvo.Plugin.Misc.StorePulse/issues). For help with a store, custom nopCommerce work or upgrades, email us.

## License

[Apache License 2.0](LICENSE).
