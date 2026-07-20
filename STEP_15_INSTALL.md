# Step 15 — Angular Purchase Order Details

## Install

1. Stop the ASP.NET application and Angular development server.
2. Extract the Step 15 ZIP into the folder containing `POTracker.csproj`.
3. Choose **Replace files in destination** when Windows asks.
4. Open the solution in Visual Studio.
5. Select **Build > Rebuild Solution**.
6. Run the ASP.NET project and sign in.
7. Open `https://localhost:7271/app/purchase-orders`.
8. Select a PO number or **View** to open its Angular details page.

Change port `7271` if Visual Studio starts the application on another port.

## Included behavior

- `GET /api/purchase-orders/{id}` details endpoint
- Angular route `/app/purchase-orders/{id}`
- PO, vendor, commercial, and summary information
- SQL-paginated linked material rows
- Existing MVC material details, PO details, and PDF links
- Viewer, Production, and Admin read access through the existing session rules

The compiled Angular files are already included under `wwwroot/app`. Run
`npm run build` from `ClientApp` only after changing Angular source code.

No database migration is required.
