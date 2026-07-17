# POTracker Premium Development Build

This source package upgrades the original POTracker MVC app with practical premium modules from the preview.

## Build Status

Verified with:

```powershell
dotnet build --no-restore
```

Result:

```text
Build succeeded.
0 warnings, 0 errors.
```

## Core Automation Rule

When a material has a delivery date, the app now automatically calculates:

```csharp
ProcessStartDate = DeliveryDate.AddMonths(-1);
AlertDate = ProcessStartDate;
PendingQty = Math.Max(Qty - DispatchQty, 0);
```

This is implemented in:

```text
Controllers/MaterialController.cs
```

## Added/Improved Modules

- Purchase order create/delete support
- Stock CRUD
- Process Start Queue
- Dispatch Command Center
- Drawing Revision Tracker
- Notification Center
- Audit Log starter page
- Purchaser Analytics
- Bulk Import placeholder
- Admin settings and user list
- Calendar events for process start, dispatch, delivery, and overdue

## New Controllers

```text
Controllers/StockController.cs
Controllers/ProcessQueueController.cs
Controllers/DispatchController.cs
Controllers/DrawingController.cs
Controllers/NotificationController.cs
Controllers/AuditController.cs
Controllers/AnalyticsController.cs
Controllers/ImportController.cs
Controllers/AdminController.cs
```

## New Views

```text
Views/Stock/*
Views/ProcessQueue/Index.cshtml
Views/Dispatch/Index.cshtml
Views/Drawing/Index.cshtml
Views/Notification/Index.cshtml
Views/Audit/Index.cshtml
Views/Analytics/Index.cshtml
Views/Import/Index.cshtml
Views/Admin/*
```

## Calendar Colors

```text
Blue   = Process start date
Green  = Delivery date
Orange = Dispatch date
Red    = Overdue
```

## How To Run

```powershell
dotnet restore
dotnet ef database update
dotnet run --launch-profile http
```

Open:

```text
http://localhost:5252
```

If the database is empty, open:

```text
/Account/Register
```

## Notes

No new database fields were required for this build. It uses the existing `Material.ProcessStartDate`, `AlertDate`, `DeliveryDate`, `DispatchDate`, `DispatchStatus`, `Stock`, and `Alert` models.

The bulk import and audit modules are starter screens. They are ready for the next step: Excel upload processing and a dedicated `AuditLog` table.
