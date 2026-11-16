# FactoryFlow ERP – System Architecture & Implementation Plan

## 1. Overview
**FactoryFlow** is a multi-tenant **Manufacturing, Production & Cost Accounting ERP** platform designed for food, garment, furniture, carpentry, aluminum, steel, detergent, bakery, and general manufacturing enterprises.

### Core Architectural Principles
1. **Multi-Tenancy & Strict Isolation**: Every business entity implements `ITenantEntity` (`TenantId`). Global EF Core Query Filters + JWT `tenant_id` claim enforcement ensure zero cross-tenant data leakage.
2. **Clean Architecture (Backend)**:
   - `FactoryFlow.Domain`: Entities, Value Objects, Enums, Domain Events, Accounting Rules, Exceptions, Interfaces. Zero external framework dependencies.
   - `FactoryFlow.Application`: Use Cases / Services, DTOs, FluentValidation Validators, Mapping, `ICurrentUserService`, `ITenantService`, `IApplicationDbContext`.
   - `FactoryFlow.Infrastructure`: EF Core `AppDbContext` (SQL Server), Identity + JWT + Refresh Token Rotation, Repositories/UnitOfWork, Double-Entry Accounting Engine, Inventory Valuation Engine (FIFO & Weighted Average), Costing Engine, PDF/Excel Export, Audit Logging, Seeders.
   - `FactoryFlow.API`: RESTful Controllers, Global Exception Middleware, Rate Limiting, CORS, Swagger/OpenAPI, Serilog Structured Logging, RBAC Policy Authorization.
   - `FactoryFlow.Tests`: Unit, Integration, Multi-Tenancy Isolation, Accounting Balance, Inventory Valuation, and Production Costing tests.
3. **Feature-Based Architecture (Frontend – Angular + Pure CSS)**:
   - `src/app/core/`: Auth service, JWT/Refresh interceptor, Tenant/Language/Theme services, Route Guards, Permission directives.
   - `src/app/shared/`: Reusable UI components (DataTable with search/filter/pagination, Modal, ConfirmDialog, Toast, StatusBadge, Loading/Empty/Error states, Currency/Date pipes).
   - `src/app/layouts/`: Main ERP Shell (Sidebar, Topbar, Theme toggle Light/Dark, RTL/LTR Arabic & English switcher, Notifications drawer).
   - `src/app/features/`: Self-contained feature modules (`auth`, `dashboard`, `raw-materials`, `products`, `bom`, `manufacturing`, `costing`, `labor`, `machines`, `quality`, `warehouses`, `purchasing`, `sales`, `accounting`, `treasury`, `reports`, `settings`, `audit-logs`).
   - **Styling**: 100% Pure CSS using CSS Custom Properties (`:root`, `[data-theme="dark"]`, `[dir="rtl"]`, `[dir="ltr"]`), Flexbox, and CSS Grid—no third-party CSS frameworks.

---

## 2. Solution Folder Structure
```text
FactoryFlow/
  backend/
    FactoryFlow.sln
    src/
      FactoryFlow.Domain/
      FactoryFlow.Application/
      FactoryFlow.Infrastructure/
      FactoryFlow.API/
    tests/
      FactoryFlow.Tests/
  frontend/
    (Angular Standalone + Pure CSS)
  database/
    (SQL scripts / migration notes)
  docs/
    ARCHITECTURE.md
    ERD.md
    DEPLOYMENT.md
  README.md
```

---

## 3. Financial, Accounting & Inventory Invariants
- **Precision**: All monetary amounts use `decimal(18,4)` and quantities use `decimal(18,4)` (never `float` or `double`).
- **Double-Entry Accounting**: Every `JournalEntry` must satisfy $\sum \text{Debit} = \sum \text{Credit}$ before posting. Posted entries are immutable; corrections require reversal/adjustment entries with full audit trail.
- **Idempotency & Concurrency**: Financial documents and inventory movements use `RowVersion` (`byte[]` concurrency token) and `IdempotencyKey` where applicable.
- **Inventory Valuation**: Supports per-tenant `InventoryValuationMethod` (`FIFO` or `WeightedAverage`). Negative stock is blocked unless explicitly allowed by tenant policy.
- **Manufacturing Flow**:
  - `Draft -> Planned -> InProgress -> Paused -> Completed | Cancelled`
  - Materials are **reserved** on planning/start and **issued** via explicit Warehouse Issue transactions (`WIP` debit, `Raw Materials Inventory` credit).
  - Direct labor & overhead (allocated by labor hours or machine hours per tenant setting) accumulate into `Work In Progress (WIP)`.
  - Completion transfers `Finished Goods` from `WIP` to `Finished Goods Inventory` and records unit cost, variance, scrap, and waste.

---

## 4. Phased Implementation Plan
- **Phase 1**: Solution Setup, Domain Entities, Enums, Accounting & Multi-Tenancy Core.
- **Phase 2**: EF Core `AppDbContext`, SQL Server LocalDB Migrations, Identity, JWT + Refresh Token Rotation, RBAC, Seed Data (Demo Tenant + Chart of Accounts + Sample Factory Data).
- **Phase 3**: Core Application Services & API Controllers (Auth, Tenants, Users/Roles, Dashboard, Master Data: Warehouses, Raw Materials, Products, BOM).
- **Phase 4**: Manufacturing, Cost Accounting, Labor, Machines, Quality Control, Inventory Movements (FIFO / Weighted Average).
- **Phase 5**: Purchasing, Sales, General Ledger (Auto-posting Journal Entries, Trial Balance, Income Statement, Balance Sheet, Cash Flow), Treasury (Cashboxes & Banks), Currencies & Taxes, Notifications, Reports (CSV/Excel/Print/PDF), Audit Logs.
- **Phase 6**: Angular Frontend (Pure CSS, RTL/LTR, Light/Dark, Full Feature Modules connected to real API).
- **Phase 7**: Automated Tests (`FactoryFlow.Tests`), End-to-End Verification, Documentation (`README.md`, `DEPLOYMENT.md`).

