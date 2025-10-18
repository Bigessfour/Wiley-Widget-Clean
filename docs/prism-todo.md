# Prism Integration TODOs

- [x] Provide an `IServiceProvider` implementation that wraps Unity so Prism registrations remain compliant with [Prism Unity container guidance](https://prismlibrary.com/docs/wpf/advanced/dependency-injection.html).
- [x] Introduce a `CoreModule` that registers the shell-level settings region as outlined in [Prism module documentation](https://prismlibrary.com/docs/wpf/modules/module-initialization.html).
- [x] Update DI bootstrap logging to reflect the Prism-compliant infrastructure registrations.
- [ ] Restore startup registrations for `IEnterpriseRepository`, `IBudgetRepository`, and `IAuditRepository` so critical services resolve before Prism loads OnDemand modules (per unity resolution failures in `logs/wiley-widget-20251017.log`).
