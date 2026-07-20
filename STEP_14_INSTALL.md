# Step 14 — Angular Purchase Orders

## Install

1. Stop the ASP.NET application and Angular development server.
2. Extract the Step 14 ZIP into the folder containing `POTracker.csproj`.
3. Choose **Replace files in destination** when Windows asks.
4. Open the solution in Visual Studio.
5. Select **Build > Rebuild Solution**.
6. Run the ASP.NET project and sign in.
7. Open `https://localhost:7271/app/purchase-orders`.

Change port `7271` if Visual Studio starts the application on another port.

## Included behavior

- Read-only Angular Purchase Orders list
- Search, handler, status, material-status, and date filters
- Server-side pagination and compact SQL aggregation
- Summary and quantity cards
- Existing MVC View, PDF, and filtered Excel actions
- Viewer, Production, and Admin read access through the existing session rules

The compiled Angular files are already included under `wwwroot/app`. Run
`npm run build` from `ClientApp` only after changing Angular source code.

No database migration is required.
