# ECIMS — Training & UAT Management System

A unified **Training Management System (TCS)** and **User Acceptance Testing (UAT)** platform built with **ASP.NET Core MVC, Entity Framework Core, ASP.NET Identity, and SQLite**.

The system manages the complete workflow from company and training management through UAT checklist execution, customer acceptance, digital sign-off, and Customer Service handoff.

---

## 📌 Project Overview

ECIMS combines two previously separate workflows into one centralized system:

### Training Management

The Training Management module supports:

* Company management
* Company representatives
* Trainers and trainees
* Training session requests
* Training scheduling
* Training completion tracking
* Role-based dashboards
* Notifications
* User management
* Training-related reporting

### User Acceptance Testing

The UAT module manages the process after a company's required training has been completed:

* UAT project creation
* UAT checklist/question bank
* Checklist attempts
* Pass / Fail / N/A responses
* Comments
* Evidence uploads
* Consultant submission
* Customer review
* Accept / Decline workflow
* Digital customer signature
* Project completion
* Customer Service handoff
* UAT status tracking

---

# 🏗️ System Architecture

The application uses a centralized ASP.NET Core architecture:

```text
                    ┌───────────────────────┐
                    │       ECIMS Web       │
                    │    ASP.NET Core MVC   │
                    └───────────┬───────────┘
                                │
             ┌──────────────────┼──────────────────┐
             │                  │                  │
             ▼                  ▼                  ▼
      Training Module      UAT Module        Authentication
             │                  │                  │
             └──────────────────┼──────────────────┘
                                │
                                ▼
                       Entity Framework Core
                                │
                                ▼
                             SQLite
                                │
                                ▼
                    External System Integration
                           REST API / HTTP
```

---

# 👥 User Roles

The system currently supports the following unified roles:

| Role                 | Main Responsibility                        |
| -------------------- | ------------------------------------------ |
| **Admin**            | Full system administration                 |
| **Consultant**       | Training and UAT management                |
| **ProjectManager**   | Project/UAT oversight                      |
| **TransportManager** | Transport-related management               |
| **ContactPerson**    | Customer representative and UAT acceptance |
| **Trainer**          | Training delivery                          |
| **CustomerService**  | Customer Service oversight and archival    |

> `ContactPerson` represents the customer-side representative responsible for reviewing and accepting UAT results.

---

# 🔐 Authentication & Authorization

ECIMS uses **ASP.NET Identity** for authentication and role-based authorization.

There is one login system shared across both Training and UAT modules.

A user's access depends on their assigned role and their relationship to the relevant company, training session, or UAT project.

### Important Trainer Note

Creating a Trainer under the **Trainers** section creates a trainer roster record.

It does **not automatically create a login account**.

For a trainer to log into the system:

1. Create the Trainer record.
2. Go to **User Management**.
3. Create a user account.
4. Assign the `Trainer` role.
5. Link the user to the appropriate Trainer record.

---

# 🏢 Company & Branch Structure

A `Company` represents the customer organization.

For UAT purposes, the company also acts as the customer associated with the UAT project.

Branches and projects are organized through:

```text
Company
   │
   └── CompanyBranch
          │
          └── UAT Project
                 │
                 └── UAT Checklist Attempt
```

This allows multiple branches/projects to be managed under a single customer company.

---

# 🔄 Training → UAT Workflow

A major business rule in the system is that UAT cannot begin until the required training has been completed.

```text
Company
   │
   ▼
Training Requested
   │
   ▼
Training Scheduled
   │
   ▼
Trainer Assigned
   │
   ▼
Training Completed
   │
   ▼
Company becomes UAT Ready
   │
   ▼
UAT Project Created
   │
   ▼
UAT Checklist Started
   │
   ▼
Checklist Submitted
   │
   ▼
Customer Review
   │
   ├── Decline ──────► Consultant Corrects Checklist
   │                         │
   │                         └── Resubmits
   │
   └── Accept
         │
         ▼
   Customer Digital Signature
         │
         ▼
   UAT Project Completed
         │
         ▼
   Send to Customer Service
```

### UAT Readiness Rule

A company becomes eligible for UAT only when **every training booked for that company has a `Completed` status**.

This rule is implemented in:

```text
Services/UatWorkflow.cs
```

---

# 📊 Dashboard

The system provides a unified dashboard with two main sections:

### Training

Displays live training-related information and statistics.

### UAT

Displays:

* UAT statistics
* Project status
* Company readiness
* Locked companies
* Unlocked companies
* UAT project information

The dashboard provides a centralized overview instead of requiring users to navigate between two separate applications.

---

# 🧪 UAT Checklist

The UAT checklist is the core testing workflow.

A Consultant can start an attempt and answer the seeded checklist items using:

* **Pass**
* **Fail**
* **N/A**

Each checklist item can also contain:

* Comments
* Evidence uploads

The Consultant can save progress and submit the completed checklist for customer review.

---

# 👤 Customer Review

After a Consultant submits a checklist, the customer's Contact Person can access:

**Review & Decide**

The Contact Person can:

### Accept

Moves the UAT project to the sign-off stage.

### Decline

Returns the checklist to the Consultant with a reason for correction.

This prevents unauthorized users from accessing the customer decision workflow.

---

# ✍️ Digital Sign-Off

After customer acceptance, the Contact Person can digitally sign the UAT project using the system's signature-pad interface.

The signing process:

1. Captures the customer's digital signature.
2. Marks the UAT project as completed.
3. Notifies relevant users.
4. Creates an Acceptance Certificate record.
5. Makes the project eligible for Customer Service handoff.

---

# 🏢 Customer Service

Customer Service receives UAT projects that have been explicitly forwarded to them.

Customer Service has a dedicated view containing only projects that have been forwarded.

This prevents Customer Service users from seeing or opening unrelated UAT projects.

Customer Service acts primarily as an:

* Oversight mechanism
* Archive
* Final operational handoff

It is **not an additional approval gate** in the current workflow.

---

# 🔔 Notifications

The system provides notifications for important workflow events.

Notifications currently cover events such as:

* Session requests
* Training scheduling
* Training updates
* Training completion
* UAT readiness
* UAT submission
* Customer acceptance
* Customer decline
* Digital sign-off
* Customer Service handoff

---

# 🔗 External System Integration

ECIMS contains an integration layer for synchronizing data with an external system.

The integration is located under:

```text
/External
```

### Main Components

```text
External/
├── Dtos.cs
├── ExternalSystemOptions.cs
├── ExternalSyncClient.cs
├── ExternalMapper.cs
├── ConstantCodes.cs
└── UserSyncCoordinator.cs
```

### Integration Responsibilities

The integration layer handles:

* DTO mapping
* HTTP communication
* External ID tracking
* Consignee synchronization
* User synchronization
* Role mapping
* Voucher synchronization
* Voucher updates
* UAT lifecycle synchronization

---

# 🔄 Consignee Synchronization

People and organizations that participate in the system can be synchronized as **Consignees** in the external system.

This includes:

* Companies
* Trainers
* Trainees
* Application Users

For users, the external Consignee `Code` uses the user's **username**.

This allows the external system to identify users using the same username used for ECIMS login.

---

# 👤 User Synchronization Flow

Users are synchronized using the following sequence:

```text
Application User
      │
      ▼
Consignee
      │
      ▼
External User
      │
      ▼
UserRoleMapper
```

The system also performs a synchronization check during login.

If an account was created before external integration was configured, or a previous synchronization failed, the next login attempts to complete the missing synchronization.

Existing external IDs are checked first to reduce the possibility of duplicate records.

---

# 🎫 Voucher Synchronization

Both Training Sessions and UAT Projects are represented as **Vouchers** in the external system.

### Training Session Voucher

Currently mapped as:

```text
Consignee1 → Trainer
Consignee2 → Company
Consignee3 → Contact Person
Consignee4 → Created By
```

### UAT Project Voucher

Currently mapped as:

```text
Consignee1 → Consultant
Consignee2 → Company
Consignee3 → Contact Person
Consignee4 → Project Manager
Consignee5 → Created By
```

> ⚠️ The exact Consignee1–Consignee5 ordering is currently a best-effort interpretation of the external system requirements and should be confirmed with the system owner.

---

# 🔁 External Create & Update Behavior

The integration supports both **creation and updating**.

### First synchronization

```text
ECIMS Entity
     │
     ▼
POST
     │
     ▼
External System
     │
     ▼
External ID returned
     │
     ▼
Saved in ECIMS
```

### Subsequent synchronization

```text
ECIMS Entity
     │
     ▼
Existing External ID?
     │
     ├── Yes → PUT / Update
     │
     └── No  → POST / Create
```

External IDs are stored on relevant ECIMS entities so that future updates target the same external records.

---

# 🧪 UAT External Synchronization Lifecycle

UAT projects are synchronized through their lifecycle.

```text
UAT Project Created
        │
        ▼
Voucher Created
        │
        ▼
Checklist Submitted
        │
        ▼
Voucher Updated
Definition = Submitted
        │
        ▼
Customer Signs
        │
        ▼
Voucher Updated
Definition = Signed
```

Voucher date fields are also populated, including:

* `IssuedDate`
* `CreatedOn`
* `LastModified`
* `StartDate`
* `EndDate`

---

# ⚙️ External System Configuration

External integration is intentionally disabled until the required configuration is provided.

Configure the external system under:

```text
appsettings.json
```

Example structure:

```json
"ExternalSystem": {
  "BaseUrl": "",
  "ApiKey": ""
}
```

The actual configuration values must be provided by the external system owner.

---

# ❗ Required External System Information

Before enabling external synchronization in production, obtain the following information from the system owner/instructor:

### 1. API Endpoint

The exact Base URL and endpoint paths.

For example:

```text
https://example.com/api/
```

The exact paths must be confirmed rather than assumed.

### 2. Authentication Method

Confirm whether the external API requires:

* API Key
* Bearer Token
* Basic Authentication
* Other authentication

### 3. SystemConstant IDs

The external DTOs contain integer values representing things such as:

* Voucher Type
* Voucher Definition
* Role
* GslType
* Other lookup constants

These values currently exist as placeholders in:

```text
External/ConstantCodes.cs
```

They must be replaced with the real values provided by the external system.

---

# 🛡️ External Integration Safety

External synchronization is designed so that an external API failure should not break the local ECIMS operation.

If:

* The Base URL is empty
* The API call fails
* The external service is unavailable

the system logs a warning and continues the local operation.

This ensures that the external integration does not become a single point of failure for the main application.

---

# 🗄️ Database

The application currently uses:

**SQLite**

Database:

```text
tcs.db
```

The application uses:

```csharp
EnsureCreated()
```

instead of EF Core migrations.

On first run, the application automatically creates the database and required schema.

---

# ⚠️ Important Database Warning

Because this version does **not use EF Core migrations**, the application contains schema safety logic that can recreate the database when the existing database schema does not match the current application model.

### Before replacing the application with a newer build:

**Back up your `tcs.db` file.**

If you are intentionally switching to a build with a changed schema, you may need to delete:

```text
tcs.db
```

and allow the application to recreate it.

> ⚠️ Deleting the database can permanently remove data that was manually entered. Always make a backup first.

---

# 🚀 Getting Started

## Prerequisites

Install:

* .NET SDK
* Git
* A supported IDE/editor such as Visual Studio or VS Code

Verify the .NET installation:

```bash
dotnet --version
```

---

## Clone the Repository

```bash
git clone https://github.com/R3D-GM/ECIMS-Training-Management-System.git
cd ECIMS-Training-Management-System
```

---

## Restore Dependencies

```bash
dotnet restore
```

---

## Build the Project

```bash
dotnet build
```

---

## Run the Application

```bash
dotnet run
```

Then open the local URL displayed by ASP.NET Core in the terminal.

---

# 🧹 If You Encounter Database Schema Problems

Stop the application and back up your database first.

Then, if you intentionally want a clean database:

```text
Delete:
tcs.db
```

Restart the application:

```bash
dotnet run
```

The application will recreate the database.

---

# 📁 Important Project Structure

```text
ECIMS-Training-Management-System/
│
├── Controllers/
│   ├── AccountController.cs
│   ├── DashboardController.cs
│   ├── TrainingSessionsController.cs
│   ├── UatProjectsController.cs
│   ├── UatChecklistController.cs
│   ├── UatSignoffController.cs
│   ├── CustomerServiceController.cs
│   └── ...
│
├── Models/
│   ├── Roles.cs
│   ├── UatEntities.cs
│   └── ...
│
├── Data/
│   ├── ApplicationDbContext.cs
│   └── SeedData.cs
│
├── Services/
│   └── UatWorkflow.cs
│
├── External/
│   ├── Dtos.cs
│   ├── ExternalMapper.cs
│   ├── ExternalSyncClient.cs
│   ├── ExternalSystemOptions.cs
│   ├── ConstantCodes.cs
│   └── UserSyncCoordinator.cs
│
├── Views/
│   ├── Dashboard/
│   ├── UatProjects/
│   ├── UatChecklist/
│   ├── UatSignoff/
│   └── Shared/
│
├── wwwroot/
│
├── Program.cs
├── appsettings.json
└── tcs.db
```

---

# ✅ Current Implementation Status

## Implemented

* [x] Unified Training + UAT application
* [x] Unified authentication
* [x] Role-based authorization
* [x] Company management
* [x] Company branches
* [x] Training workflow
* [x] Trainer and trainee management
* [x] Training notifications
* [x] UAT readiness workflow
* [x] UAT projects
* [x] UAT question bank
* [x] UAT checklist attempts
* [x] Pass / Fail / N/A responses
* [x] Comments
* [x] Evidence uploads
* [x] Consultant submission
* [x] Customer Accept / Decline
* [x] Customer digital signature
* [x] UAT completion
* [x] Customer Service handoff
* [x] Customer Service project filtering
* [x] UAT dashboard
* [x] Training dashboard
* [x] External DTO integration
* [x] Consignee synchronization
* [x] User synchronization
* [x] Role mapping
* [x] Voucher synchronization
* [x] External create/update logic
* [x] External ID tracking
* [x] Login synchronization safety net
* [x] UAT lifecycle synchronization

---

# 🚧 Remaining Work

The following features are still planned or incomplete:

### Acceptance Certificate PDF

QuestPDF integration still needs to generate the final signed acceptance certificate.

Current behavior creates the Acceptance Certificate record, but the actual PDF file has not yet been implemented.

### UAT Question Bank Administration

The UAT question bank is currently seeded through:

```text
Data/SeedData.cs
```

An Admin interface for:

* Adding sections
* Editing sections
* Adding checklist items
* Editing checklist items
* Managing the question bank

still needs to be implemented.

### Customer Checklist Editing

Currently, the customer can review the submitted checklist but cannot directly edit or flag individual checklist items before making the final decision.

### Notifications

Additional wiring may still be required for some new UAT notification scenarios.

### Security Review

A final security review should be performed after compilation and testing, particularly around:

* Ownership checks
* Role authorization
* UAT project access
* Evidence file access
* Company-level authorization

---

# 🧪 Testing Checklist

Before deploying the system, test the complete workflow.

## Authentication

* [ ] Admin login
* [ ] Consultant login
* [ ] Trainer login
* [ ] Project Manager login
* [ ] Contact Person login
* [ ] Customer Service login
* [ ] Unauthorized page access

## Training

* [ ] Create company
* [ ] Create trainer
* [ ] Create trainee
* [ ] Create training request
* [ ] Assign trainer
* [ ] Schedule training
* [ ] Verify notifications
* [ ] Mark training completed
* [ ] Verify company becomes UAT-ready

## UAT

* [ ] Create UAT project
* [ ] Verify locked company cannot create UAT
* [ ] Start checklist
* [ ] Save checklist progress
* [ ] Upload evidence
* [ ] Submit checklist
* [ ] Contact Person reviews checklist
* [ ] Decline checklist
* [ ] Consultant corrects/resubmits
* [ ] Accept checklist
* [ ] Customer signs digitally
* [ ] Project becomes completed
* [ ] Forward project to Customer Service
* [ ] Customer Service sees forwarded project

## External Integration

After receiving the correct API configuration:

* [ ] Configure Base URL
* [ ] Configure authentication
* [ ] Configure SystemConstant IDs
* [ ] Test Company → Consignee
* [ ] Test Trainer → Consignee
* [ ] Test Trainee → Consignee
* [ ] Test User → Consignee/User/Role Mapper
* [ ] Test Training Session → Voucher
* [ ] Test UAT Project → Voucher
* [ ] Test Voucher updates
* [ ] Verify external IDs
* [ ] Verify duplicate prevention
* [ ] Test external API failure behavior

---

# 🔒 Security Considerations

Before production deployment, verify:

* Role-based authorization on every controller
* Company ownership checks
* UAT project ownership
* Customer access restrictions
* Evidence upload validation
* File storage security
* Authentication configuration
* Production secrets
* API credentials
* Database backups
* HTTPS
* External API authentication
* Authorization for Customer Service views

Never commit production API keys, passwords, or other secrets to Git.

---

# 📌 Known Limitations

### Search

The top navigation search box is currently a visual placeholder and is not connected to a real search implementation.

### Trainer Accounts

A Trainer roster entry does not automatically create a login account. A separate Identity user must be created and linked.

### Acceptance Certificate

The Acceptance Certificate database record is created after signing, but the final PDF generation still needs to be implemented.

### External API

External synchronization cannot be fully activated until the external system owner provides:

1. Endpoint URL
2. Authentication requirements
3. SystemConstant IDs

### Consignee Ordering

The current Consignee1–Consignee5 mapping for Voucher records is based on the available requirements and should be confirmed with the external system owner.

---

# 🗺️ Development Roadmap

## Phase 1 — Core System

* [x] Merge Training and UAT
* [x] Unified authentication
* [x] Unified roles
* [x] Training workflow
* [x] UAT workflow

## Phase 2 — Customer Workflow

* [x] Customer review
* [x] Accept / Decline
* [x] Digital sign-off
* [x] Customer Service handoff

## Phase 3 — External Integration

* [x] DTO mapping
* [x] Consignee synchronization
* [x] User synchronization
* [x] Role mapping
* [x] Voucher synchronization
* [x] Update synchronization
* [ ] Production API configuration
* [ ] Production integration testing

## Phase 4 — Finalization

* [ ] Acceptance Certificate PDF
* [ ] Admin question-bank management
* [ ] Customer checklist item editing/flagging
* [ ] Complete notification review
* [ ] Full security audit
* [ ] End-to-end testing
* [ ] Production deployment

---

# 🧑‍💻 Development Notes

This project has undergone a substantial merge between the original Training Management System and UAT Checklist application.

Some functionality was implemented without access to a local .NET/NuGet build environment. Therefore, **always run the project locally and resolve compilation errors before considering a build production-ready**.

The recommended validation loop is:

```bash
dotnet restore
dotnet build
dotnet run
```

Then test the complete Training → UAT → Customer Acceptance → Sign-off → Customer Service workflow.

---

# 🤝 External Integration Coordination

The external integration depends on information that cannot be safely inferred from ECIMS.

The external system owner must confirm:

```text
1. API Base URL / Endpoints
2. Authentication Method
3. SystemConstant IDs
4. Voucher Consignee Ordering
```

Once these are confirmed, the integration configuration can be completed and tested against the actual external API.

---

# 📄 License

This project is maintained as an internal system for managing training and UAT workflows.

Add the appropriate organizational license or usage terms before public distribution.

---

## ⭐ Project Status

**Current status: Feature-complete core workflow with remaining production-integration and finalization tasks.**

The main Training + UAT workflow is implemented, including customer acceptance, digital sign-off, Customer Service handoff, and the foundation for external system synchronization.

Before production use, the project should undergo:

**Build → Bug Fixing → End-to-End Testing → Security Review → External API Testing → Deployment**

---
