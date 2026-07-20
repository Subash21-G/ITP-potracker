# PO Tracker Angular Client — Step 15

The Angular Materials and Purchase Orders screens can run in two modes:

- Development: Angular's live development server proxies API requests to ASP.NET.
- Integrated production: ASP.NET serves the compiled Angular application from
  `wwwroot/app` under `/app`.

The existing MVC pages and `/api` endpoints remain available.

## Angular routes

- `/app/materials`
- `/app/purchase-orders`
- `/app/purchase-orders/{id}`

The Purchase Orders screens use these read-only endpoints:

- `GET /api/purchase-orders`
- `GET /api/purchase-orders/{id}`

Purchase Order details paginate linked materials on the server. Material
details, PO PDF, MVC details, and filtered Excel actions continue to use the
existing MVC application during the gradual migration.

## Requirements

- Steps 11 through 14 installed
- Node.js 22.22.3+, 24.15+, or 26+
- npm 8+

## Development mode

1. Start the ASP.NET application.
2. Confirm that every target in `proxy.conf.json` uses the active ASP.NET origin.
3. Run `npm install` once.
4. Run `npm start -- --ssl` when the backend uses HTTPS.
5. Open `https://localhost:4200/materials` or
   `https://localhost:4200/purchase-orders`.

Development mode provides live reload and requires both processes to remain open.

## Build the integrated application

From this `ClientApp` folder, run:

```powershell
npm run build
```

The production build is written directly to the parent ASP.NET project's
`wwwroot/app` directory. The build uses `/app/` as its base URL and hashed asset
filenames for safe browser caching.

Then stop the Angular development server, start only the ASP.NET application,
and open:

```text
https://localhost:7271/app/materials
```

or:

```text
https://localhost:7271/app/purchase-orders
```

Change the port to the active ASP.NET port when necessary. Deep links under
`/app/*` return the Angular `index.html`, while MVC and API routes keep their
existing behavior.

Re-run `npm run build` after changing Angular source files. No database migration
is required.
