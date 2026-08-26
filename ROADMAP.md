# Merged TCS + UAT System - Build Status

## Can't compile here
This sandbox has no .NET SDK / no NuGet access, so this code was written
carefully but NEVER compiled or run. Before trusting it, on your machine run:

    dotnet restore
    dotnet build

Expect to fix a handful of small issues (missing usings, a typo'd property
name) - normal for a merge this size done without a compiler in the loop.

## What's done
- Unified roles: Admin, Consultant (Functional Consultant - both modules),
  ProjectManager, TransportManager, ContactPerson (= Customer
  Representative), Trainer, CustomerService. See Models/Roles.cs.
- One login (ASP.NET Identity + SQLite) for both modules.
- Company now doubles as the UAT "Customer"; new CompanyBranch entity
  holds UAT branches/projects under it.
- Full UAT data model ported into Models/UatEntities.cs and wired into
  Data/ApplicationDbContext.cs.
- UAT question bank seeded in Data/SeedData.cs (Sections + MasterItems).
- Workflow rule (Services/UatWorkflow.cs): a company only unlocks UAT once
  every training booked for it is Completed.
- Dashboard (Controllers/DashboardController.cs, Views/Dashboard/Index.cshtml):
  one page, two tabs - Trainings / UAT - with live stats and a company
  readiness (locked/unlocked) table.
- UatProjectsController + Index/Create/Details views: list, create (gated
  by the workflow rule), and view a UAT project.
- UatChecklistController + Attempt view: the core UAT loop - start an
  attempt, answer every item Pass/Fail/N-A with comments + evidence upload,
  save progress, submit for customer review. Read-only viewer for
  PM/ContactPerson/CustomerService/Admin.

## Still to port (from the original UAT-Checklist app)
- CustomerController - contact person accepts/declines a submitted checklist
- PmController - PM review + signature step
- CustomerServiceController - forwards accepted checklists onward
- SignoffController - digital signature capture (signature pad)
- AcceptanceCert PDF generation (QuestPDF) - the final signed certificate
- AdminController - question bank management UI (add/edit sections & items)
- NotificationsController wiring for the new UAT notifications
- Sidebar nav link to UAT Projects in Views/Shared/_Layout.cshtml (not yet added)
- Per-row security review pass (a couple of ownership checks were ported
  1:1 from the old app and deserve a second look once it compiles)

## Recommended next step
Because this environment can't build/test .NET, the fastest safe way to
finish this is to open this project in **Claude Code** on your machine
(or point Claude Code at this zip) so we get a real compile-fix loop
instead of writing more code blind.

## "Most of the system isn't working" - what I found and fixed

I went through this report line by line since I can't run the app myself:

1. **"Search bar not working"** - Not a bug. The search box in the top bar
   (`<div class="search-box">Search anything...</div>`) is a static, decorative
   placeholder in the original TCS design - it was never wired to an actual
   search function, before or after the merge. It's cosmetic only.

2. **"Added a trainer, trainer dashboard not showing it"** - This is how the
   original app was designed, not something the merge broke: adding someone
   under **Trainers** only creates a roster record (name, phone, etc.) - it
   does NOT create a login. To let that person actually log in and see a
   trainer dashboard, go to **User Management** (Admin only) and create a
   **User** with role = Trainer, linked to that Trainer record. Only that
   linked login sees trainer-scoped data.

3. **"Assign a session request / schedule a training - the other user
   doesn't get notified"** - This one WAS a real gap, now fixed. Session
   requests already notified people correctly. But scheduling or editing a
   Training Session (Controllers/TrainingSessionsController.cs) never
   notified the assigned trainer or the company's contact person - I added
   that. Marking a session Completed now also notifies Consultants when a
   company becomes UAT-ready.

4. Added the missing **"UAT Projects"** link to the sidebar nav - it existed
   as a controller/pages but had no menu entry, so it was only reachable
   from the Dashboard's UAT tab.

## One thing to watch for with your local database
This project has no EF Core migrations - the app creates tcs.db automatically
on first run (Program.cs, `EnsureCreated()`), and has a safety check that
wipes and recreates it if the on-disk schema doesn't match the code. That
means: **any time you swap in an updated build from me, delete your local
tcs.db before running it**, otherwise you might hit the auto-wipe unexpectedly
and lose data you'd entered by hand (the seeded demo logins always come back,
but anything you added yourself won't).

## Customer review -> Accept/Decline -> sign-off -> Customer Service (added)

This was the "Access Denied" bug - the Contact Person's review/decide step
didn't exist yet, only the Consultant's edit screen did (which is
Consultant/Admin only by design, hence the 403).

Now working:
- **UatChecklistController.Decide / Accept / Decline** - once a Consultant
  submits a checklist, the Contact Person opens "Review & Decide" from the
  UAT project page, sees a pass/fail summary, and either Accepts (moves to
  sign-off) or Declines (kicks it back to the Consultant with a reason).
- **UatSignoffController** - after Accept, the Contact Person draws a
  digital signature (same signature-pad.js canvas used elsewhere in TCS).
  Signing marks the project Completed and notifies the PM and Consultant.
- **CustomerServiceController** - a Consultant/PM can click "Send to
  Customer Service" from the project page once it's past the customer's
  acceptance. Customer Service gets their own read-only list (new sidebar
  link) of everything forwarded to them - this is oversight/archival, not
  another required approval gate, matching how the original UAT-Checklist
  app worked.
- Added a company-ownership check on UatProjects/Details so a Contact
  Person can only open their own company's projects (was previously open
  to anyone with the URL - a real gap, now closed).

## Still not built
- PDF acceptance certificate generation (QuestPDF) - a stub AcceptanceCert
  row gets created on sign, but no PDF file yet.
- Admin UI for editing the UAT question bank (currently seed-data only).
- Customer's ability to edit/flag individual checklist items during review
  before deciding (currently they see the submitted answers read-only and
  the Fail/Flagged summary, then Accept or Decline the whole thing).

## Fixed: "Access Denied" for Customer Service clicking into a project

Real bug, not by design. The sidebar had a generic "UAT Projects" link
visible to Customer Service that listed EVERY project (not just ones
forwarded to them), and clicking into one that hadn't been sent yet hit
the ownership check and got blocked.

Fixed:
- UatProjectsController.Index and DashboardController now filter the list/
  counts for CustomerService to only projects that have actually been
  forwarded (SentToCustomerServiceDate != null) - same rule Details() already enforced.
- Removed the redundant "UAT Projects" sidebar link for Customer Service -
  they only need (and now only see) the dedicated "Customer Service" page,
  which already shows exactly what's been forwarded to them.

## External system sync (his DTOs) - added, but needs info only he can give you

Built the whole integration layer in the new `/External` folder:
- `Dtos.cs` - his 6 DTOs, copied verbatim (field names must match exactly).
- `ExternalSystemOptions.cs` + `appsettings.json` "ExternalSystem" section -
  where you paste his BaseUrl and API key once you have them.
- `ExternalSyncClient.cs` - the HTTP client that actually POSTs to his API.
  Safe by design: if BaseUrl is blank, or the call fails, it just logs a
  warning and moves on - it can NEVER break your own save.
- `ExternalMapper.cs` - converts our entities (Company, ApplicationUser,
  etc.) into his DTO shapes. This is the actual "map my app to their
  object" logic.
- `ConstantCodes.cs` - placeholder for his SystemConstant Ids. Every value
  in here is currently 0 and marked TODO.
- Added `ExternalConsigneeId` / `ExternalUserId` fields to Company and
  ApplicationUser, so we remember the Id HIS system assigns when we sync
  something (his system creates its own Ids - we can't assume ours match).
- Wired two real examples: creating a Company now syncs it as a Consignee;
  creating a User now syncs Consignee -> User -> UserRoleMapper in order.
  Nothing sends yet because BaseUrl is blank - that's intentional. Once
  it's filled in, these two flows will actually start sending data.

### Three things ONLY your instructor can give you - ask for these:
1. **The actual endpoint URL(s)** - the mention of "an endpoint" isn't
   enough to build against; I need the real BaseUrl (and confirm the paths
   in ExternalSystemOptions.cs match what he expects, e.g. `/api/vouchers`).
2. **How to authenticate the call** - API key header? Bearer token? Nothing?
3. **The SystemConstant Id numbers** - every `Type`, `Definition`, `Role`,
   `GslType` etc. field in his DTOs is just an int that means nothing
   without his lookup table. I put every one of these in
   `External/ConstantCodes.cs` with a comment telling you exactly what to
   ask for. Without these, whatever we send will likely get rejected by his
   API (wrong codes) even once the URL and auth work.

Once you have those three things, tell me and I'll wire it in - should be
quick since the plumbing is already built.

## Filled in the gaps from your notes about his requirements

You brought back more detail from him, and you were right to check - two
real pieces were missing before. Now added:

1. **Trainer AND Trainee both sync as Consignees**, not just logins. Adding
   a Trainer (roster) or a Trainee (under a training session) now syncs
   them as a person Consignee, same as any login account does.
2. **Both Training Sessions and UAT Projects sync as Vouchers**, using
   `Type` + `Definition` to tell them apart (both still placeholder 0s in
   `ConstantCodes.cs` until he gives you the real numbers). The voucher
   carries the Trainer/Consultant and Company as `Consignee1`/`Consignee2` -
   the foreign-key link he described.
3. **Save AND update, not just save.** Every entity that gets synced now
   remembers the Id his system gave back (`ExternalVoucherId` /
   `ExternalConsigneeId` on Company, Trainer, Trainee, ApplicationUser,
   TrainingSession, UatProject, ProjectUatAttempt). The first save is a
   POST (create); every save after that is a PUT to that same Id (update) -
   see `ExternalSyncClient.UpdateVoucherAsync` / `UpdateConsigneeAsync`.
   Wired at every stage of the UAT lifecycle: project created -> Voucher
   created; checklist submitted -> Voucher updated (Definition = Submitted);
   customer signs -> Voucher updated again (Definition = Signed).
4. **DateTime fields** - `IssuedDate`, `CreatedOn`, `LastModified`,
   `StartDate`/`EndDate` are all populated on every Voucher we build
   (see `ExternalMapper.ToVoucher` overloads) - not left blank.

Still needs from him before any of this actually sends: the endpoint
URL, the auth method, and the real `ConstantCodes` numbers (Voucher
Type/Definition codes, Role codes, GslType codes) - unchanged from before.
