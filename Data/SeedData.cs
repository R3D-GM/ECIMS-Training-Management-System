using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TCS.Models;

namespace TCS.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var db = services.GetRequiredService<ApplicationDbContext>();

        // 1. Roles
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // 2. Reference data needed before we can link user accounts to it
        var company = await db.Companies.FirstOrDefaultAsync();
        if (company == null)
        {
            company = new Company
            {
                Name = "REAZ ENGINEERING AND CON PLC",
                Branch = "Head Office",
                Address = "Bisrate Gebreal, Addis Ababa",
                ContactPersonName = "Contact Person",
                Phone = "0911000000",
                TIN = "0000000000"
            };
            db.Companies.Add(company);
            await db.SaveChangesAsync();
        }

        var trainerRecord = await db.Trainers.FirstOrDefaultAsync();
        if (trainerRecord == null)
        {
            trainerRecord = new Trainer
            {
                Name = "Gedamu Waleign",
                Position = "Functional Consultant",
                Department = "Training",
                Email = "trainer@company.com",
                Phone = "0911111111"
            };
            db.Trainers.Add(trainerRecord);
            await db.SaveChangesAsync();
        }

        var branch = await db.CompanyBranches.FirstOrDefaultAsync(b => b.CompanyId == company.Id);
        if (branch == null)
        {
            branch = new CompanyBranch
            {
                CompanyId = company.Id,
                BranchName = company.Branch ?? "Head Office",
                Address = company.Address,
                SiteContactName = company.ContactPersonName,
                SiteContactPhone = company.Phone
            };
            db.CompanyBranches.Add(branch);
            await db.SaveChangesAsync();
        }

        // 3. The login accounts. Note: ContactPerson is one role shared by
        // both modules (Training "Contact Person" == UAT "Customer
        // Representative"), and Consultant gets menus for both modules too.
        var users = new (string Email, string FullName, string Role, int? TrainerId, int? CompanyId)[]
        {
            ("admin@company.com",      "System Administrator",  Roles.Admin,           null, null),
            ("consultant@company.com", "Functional Consultant", Roles.Consultant,      null, null),
            ("pm@company.com",         "Project Manager",       Roles.ProjectManager,  null, null),
            ("transport@company.com",  "Transport Manager",     Roles.TransportManager,null, null),
            ("contact@company.com",    "Contact Person",        Roles.ContactPerson,   null, company.Id),
            ("trainer@company.com",    "Gedamu Waleign",        Roles.Trainer,         trainerRecord.Id, null),
            ("cs@company.com",         "Customer Service",      Roles.CustomerService, null, null),
        };

        foreach (var u in users)
        {
            var existing = await userManager.FindByEmailAsync(u.Email);
            if (existing != null) continue;

            var user = new ApplicationUser
            {
                UserName = u.Email,
                Email = u.Email,
                EmailConfirmed = true,
                FullName = u.FullName,
                TrainerId = u.TrainerId,
                CompanyId = u.CompanyId
            };

            var result = await userManager.CreateAsync(user, "admin123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, u.Role);
            }
        }

        // 4. A little demo data so the dashboards / lists aren't empty on first run
        if (!await db.TrainingSessions.AnyAsync())
        {
            var session = new TrainingSession
            {
                CompanyId = company.Id,
                ConsultantName = "Functional Consultant",
                Module = "NVS POS",
                Location = "Bisrate Gebreal",
                TrainingDate = DateTime.Today.AddDays(-2),
                Duration = 1,
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                TrainerId = trainerRecord.Id,
                Status = SessionStatus.Completed
            };
            db.TrainingSessions.Add(session);
            await db.SaveChangesAsync();

            db.Trainees.Add(new Trainee
            {
                TrainingSessionId = session.Id,
                Name = "Sample Trainee",
                Position = "Cashier",
                Phone = "0911222333",
                Attendance = AttendanceStatus.Present
            });

            db.TrainingConfirmations.Add(new TrainingConfirmation
            {
                TrainingSessionId = session.Id,
                ContactPersonName = "Contact Person",
                Remarks = "Conformed to Training Section!!",
                Status = ConfirmationStatus.Pending
            });

            var upcoming = new TrainingSession
            {
                CompanyId = company.Id,
                ConsultantName = "Functional Consultant",
                Module = "Inventory Management",
                Location = "Bisrate Gebreal",
                TrainingDate = DateTime.Today.AddDays(3),
                Duration = 2,
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(11, 0, 0),
                TrainerId = trainerRecord.Id,
                Status = SessionStatus.Scheduled
            };
            db.TrainingSessions.Add(upcoming);

            db.Vehicles.Add(new Vehicle
            {
                PlateNumber = "AA-12345",
                Model = "Toyota Hiace",
                Capacity = 12,
                DriverName = "Driver One",
                DriverPhone = "0911333444",
                Status = "Available"
            });

            await db.SaveChangesAsync();
        }

        // 5. UAT question bank (reference data shared by every UAT project -
        // ported as-is from the standalone UAT-Checklist system's seed).
        if (!await db.UatSections.AnyAsync())
        {
            var sections = new (string Name, string[] Items)[]
            {
                ("Login and preparation", new[]
                {
                    "Short cut is available on the desktop with proper naming",
                    "Double click opens login page",
                    "All active users are listed under the user name combo box",
                    "On screen keyboard is provided as option for key in credentials",
                    "Login using finger print scanner [N/A]",
                    "Login using keyboard",
                    "Login using touch screen on screen keyboard [N/A]",
                    "Enter key navigate through username and password",
                    "Password characters are secured",
                    "System cannot login with wrong user name and password",
                    "The system count down unsuccessful trials and lock system after the threshold",
                    "System enforce users to change their password when login for the first time",
                    "Login screen disappear after a successful login",
                    "POS home page is displayed"
                }),
                ("POS Home screen", new[]
                {
                    "Menu navigator", "Activity", "Dash board", "Security management",
                    "Calendar", "Messages", "Task notification"
                }),
                ("Maintenance", new[]
                {
                    "Maintain menu category", "Maintain Item", "Maintain Customer with TIN",
                    "Maintain shift", "Update Item price", "Update Item tax code"
                }),
                ("Main navigator", new[]
                {
                    "Maintenance", "SMS POS and transactions", "Documents and transactions",
                    "Audit and closings", "Reports", "Hosted devices"
                }),
                ("Check test invoice content", new[]
                {
                    "Customer name", "Buyer's TIN", "Reference", "Discount",
                    "Change calculator", "Payment methods", "Seasonal message"
                }),
                ("NVS POS Operation", new[]
                {
                    "Shift selection to open POS",
                    "Search and select consignee by code, TIN or name",
                    "Select and select other consignee",
                    "Item navigation by code using keyboard",
                    "Item navigation by code using barcode scanner",
                    "Item navigation by item name using keyboard",
                    "Navigate and select line item from nested reference tabs",
                    "Select source store",
                    "The system shows the balance",
                    "Use only available item",
                    "Use fixed Price",
                    "Use flexible price",
                    "Hold an invoice",
                    "Retrieve invoice",
                    "Remove or void item from sales list",
                    "Credentials are required for void",
                    "Removed items are tracked in removed item report",
                    "Use of easy keyboard navigation (Code, Description, Qty, Unit Price)",
                    "Add to grid F5",
                    "Print F12",
                    "Maintain new customer with TIN no inside the POS screen",
                    "Apply discount",
                    "Approve tax rates",
                    "Approve non-taxable articles",
                    "Customer display visibly display all events to the customer",
                    "Apply withholding tax",
                    "Record non cash transaction like check or CPO"
                }),
                ("Closing", new[]
                {
                    "Clearing held bill", "Sales and XML reconciliation", "Voucher integrity Audit",
                    "Fiscal reconciliation audit", "Issuing of cash sales summary voucher",
                    "Issuing of Z report", "Cash sales summary and Z report are equal",
                    "Shift is closed and inaccessible for the device",
                    "Cash sales summary voucher is sent to the remote server"
                }),
                ("Report", new[]
                {
                    "Voucher report (4 X 30 = 120 reports)", "General Sales report (18 reports)",
                    "PLU summary report", "Z report", "X report", "Z summary"
                })
            };

            var sectionOrder = 0;
            foreach (var (name, items) in sections)
            {
                sectionOrder++;
                var itemOrder = 0;

                db.UatSections.Add(new UatSection
                {
                    SectionName = name,
                    DisplayOrder = sectionOrder,
                    IsActive = true,
                    MasterItems = items.Select(i => new UatMasterItem
                    {
                        TestDescription = i,
                        DisplayOrder = ++itemOrder,
                        AnswerFormat = AnswerFormat.PassFailNA,
                        IsActive = true
                    }).ToList()
                });
            }

            await db.SaveChangesAsync();
        }
    }
}
