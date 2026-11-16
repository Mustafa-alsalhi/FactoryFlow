# 🏭 FactoryFlow ERP — Manufacturing, Production & Cost Accounting Platform

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Angular 21](https://img.shields.io/badge/Angular-21-DD0031?logo=angular)](https://angular.dev/)
[![SQL Server](https://img.shields.io/badge/SQL%20Server-EF%20Core%208-CC2927?logo=microsoftsqlserver)](https://www.microsoft.com/sql-server)
[![Architecture](https://img.shields.io/badge/Architecture-Clean%20%2B%20Feature--Based-0284c7)](#architecture)

**FactoryFlow** is a commercial-grade, **multi-tenant Manufacturing, Production & Cost Accounting ERP** built for industrial enterprises (food & bakeries, garments, furniture, carpentry, aluminum & steel, detergents, and general manufacturing).

It integrates **production planning, Bill of Materials (BOM), Work-In-Progress (WIP) cost accounting, multi-warehouse inventory valuation (FIFO & Weighted Average), purchasing, sales, double-entry general ledger accounting, treasury, quality control, and executive analytics** into a single cohesive platform.

---

## ✨ Key Engineering Highlights

- **Strict Multi-Tenancy Isolation**: Every business entity implements `ITenantEntity`. EF Core Global Query Filters combined with JWT `tenant_id` claims guarantee zero cross-tenant data leakage.
- **Double-Entry Accounting Engine**: Automated and manual journal entries enforce $\sum \text{Debit} = \sum \text{Credit}$ (`decimal(18,4)` precision), idempotency keys, and immutable posted entries (corrections via reversal entries only).
- **Inventory Valuation Engine**: Per-tenant configurable **FIFO** (batch-tracked) and **Weighted Average** costing with negative-stock guards and inter-warehouse transfers.
- **Manufacturing WIP & Cost Accounting**: Full production order lifecycle (`Draft → Planned → InProgress → Paused → Completed / Cancelled`) tracking planned vs. actual materials, direct labor (hourly/daily/monthly + overtime + bonuses/deductions), manufacturing overhead, scrap/waste cost, and unit cost variance.
- **Bilingual & Responsive Pure CSS UI**: Built with **100% Pure CSS** (CSS Custom Properties) featuring **Arabic (RTL)** & **English (LTR)** switching and **Light / Dark** themes without third-party CSS frameworks.

---

## 🛠️ Tech Stack & Tools

### Backend
| Category | Technology |
| :--- | :--- |
| **Framework** | ASP.NET Core Web API (.NET 8, C#) |
| **Architecture** | Clean Architecture (`Domain`, `Application`, `Infrastructure`, `API`, `Tests`) |
| **ORM & Database** | Entity Framework Core 8, Microsoft SQL Server / LocalDB |
| **Security & Auth** | JWT Bearer Authentication, Refresh Token Rotation, BCrypt Password Hashing, RBAC |
| **Validation & Docs** | FluentValidation, Swagger / OpenAPI, Global Exception Middleware |
| **Protection** | Rate Limiting, CORS Policy, Concurrency Tokens (`RowVersion`), Audit Logging |

### Frontend
| Category | Technology |
| :--- | :--- |
| **Framework** | Angular 21 (Standalone Components, Zoneless Change Detection, Signals) |
| **Language** | TypeScript, RxJS, Reactive & Template Forms |
| **Architecture** | Feature-Based (`core/`, `shared/`, `layouts/`, `features/`) |
| **HTTP & Routing** | Angular Router with Auth Guards, HTTP Interceptor with auto JWT refresh |
| **Styling** | 100% Pure CSS (`styles.css` with CSS Variables, Grid/Flexbox, Print CSS) |

---

## 📦 ERP Modules & Systems

1. **Multi-Tenancy, Users & RBAC**: Company registration, tenant fiscal/valuation settings, multi-branch management, user invitation/activation, 10 default roles, granular module permissions (`View`, `Create`, `Edit`, `Delete`, `Approve`, `Print`, `Export`), and login history.
2. **Executive Dashboard**: Real-time SQL-backed KPIs (Production Value & Cost, Sales, Purchases, Expenses, Net Profit, Raw Material & Finished Goods Valuation, Active/Completed Orders, Defective Units, Low Stock Alerts, Planned vs. Actual Cost Comparison) with date and warehouse filters.
3. **Raw Materials & Finished Products**: SKUs, barcodes, categories, units of measure, minimum stock thresholds, semi-finished & finished goods, and standard cost tracking.
4. **Bill of Materials (BOM)**: Multi-component recipes, waste percentage calculations, versioning, and instant target-quantity cost estimation.
5. **Manufacturing & WIP Costing**: Production order scheduling, material issue/return to WIP, direct labor logging, overhead allocation (Electricity, Water, Rent, Maintenance, Depreciation), and completion transfer to Finished Goods.
6. **Labor, Production Lines & Machines**: Employee wage models (Hourly, Daily, Monthly), production lines, machine hourly operating costs, run/downtime hours, and maintenance logs.
7. **Quality Control (QC)**: Raw material, in-process, and finished product inspections with accepted/rejected quantities, defect reasons, and automated defect notifications.
8. **Multi-Warehouse Inventory**: Stock balances, batch & expiry tracking, inter-warehouse transfers, stock adjustments, and full inventory transaction ledger.
9. **Purchasing & Suppliers**: Suppliers, purchase orders, purchase invoices & returns with automated inventory receipt, supplier balance updates, and GL journal posting.
10. **Sales & Customers**: Customers, credit limits, cash & credit sales invoices/returns with automated stock issue, COGS calculation, and GL journal posting.
11. **General Ledger & Financial Statements**: Chart of Accounts, automated & manual Journal Entries, entry reversal, General Ledger, Trial Balance, Income Statement, and Balance Sheet.
12. **Treasury & Multi-Currency**: Cashboxes, bank accounts, receipt/payment/transfer vouchers, daily cashbox closing, multi-currency exchange rates, and configurable tax rates.
13. **Reports, Notifications & Audit Trail**: Production costing & variance reports with CSV (Excel) export and Print/PDF support, system alerts, and immutable audit logs.

---

## 🏗️ Project Structure

```text
FactoryFlow/
├── backend/
│   ├── FactoryFlow.slnx
│   ├── src/
│   │   ├── FactoryFlow.Domain/          # Entities, Enums, Domain Rules
│   │   ├── FactoryFlow.Application/     # Interfaces, Constants, Validation Contracts
│   │   ├── FactoryFlow.Infrastructure/  # EF Core AppDbContext, Accounting, Inventory, Seeder
│   │   └── FactoryFlow.API/             # REST Controllers, Auth, Middleware, Swagger
│   └── tests/
│       └── FactoryFlow.Tests/           # Automated Integration & Financial Invariant Tests
├── frontend/
│   └── src/
│       ├── styles.css                   # Pure CSS Design System (Light/Dark + RTL/LTR)
│       └── app/
│           ├── core/                    # Auth, API, I18n, Interceptor, Guards
│           ├── shared/                  # Reusable UI Components
│           ├── layouts/                 # Responsive ERP Shell (Sidebar, Topbar, Notifications)
│           └── features/                # Feature-Based ERP Modules
└── docs/
    ├── ARCHITECTURE.md                  # System Architecture & Invariants
    ├── ERD.md                           # Entity Relationship Diagram (Mermaid)
    └── DEPLOYMENT.md                    # Production Deployment Guide
```

---

## 🚀 Quick Start

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20 LTS+](https://nodejs.org/) & Angular CLI
- Microsoft SQL Server / LocalDB (`(localdb)\MSSQLLocalDB`)

### 1. Run Backend API
```powershell
cd backend
dotnet run --project src/FactoryFlow.API/FactoryFlow.API.csproj --urls http://localhost:5155
```
- The database (`FactoryFlowDb`) is automatically created and seeded on first startup.
- **Swagger UI**: `http://localhost:5155/swagger`

### 2. Run Frontend (Angular)
```powershell
cd frontend
npm install
npm start
```
- Open `http://localhost:4200`

### 3. Demo Credentials
Use the **"Fill Demo Account (DEMO)"** button on the login screen, or sign in with:
- **Company Code**: `DEMO`
- **Email**: `admin@factoryflow.demo`
- **Password**: `Admin@123456`

---

## 🧪 Automated Tests

Run the automated test suite against SQL Server LocalDB (`FactoryFlowTestDb`):
```powershell
cd backend
dotnet run --project tests/FactoryFlow.Tests/FactoryFlow.Tests.csproj
```
Verified scenarios:
- Multi-Tenancy Global Query Filter Isolation
- Double-Entry Accounting Balance (`Debit == Credit`) & Reversal Integrity
- Weighted Average Valuation & Negative Stock Guard
- FIFO Batch Valuation Precision
- BCrypt Password Hashing & Refresh Token Lifecycle

---

## 📄 Documentation
- [Architecture Overview](docs/ARCHITECTURE.md)
- [Entity Relationship Diagram (ERD)](docs/ERD.md)
- [Production Deployment Guide](docs/DEPLOYMENT.md)
