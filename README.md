# Move-in Planner

A portfolio-style ASP.NET Core MVC application for managing household purchases before moving home. It separates a **requirement** (`HouseholdItem`) from the real products being considered (`ProductChoice`). This is why Amazon URLs are entered on the product-choice form rather than the initial item form.

## Features

- Dashboard with completion, budget, spend and next actions
- Category-based organisation
- Search and filtering by category and status
- Single-pick, premium-vs-budget and multiple-item requirements
- Unlimited product choices per item
- Clear product URL, direct product image URL, retailer, price, tier and quantity fields
- Optional product-image previews on purchase-option cards
- Preferred and purchased product states
- Collapsible long comparison responses
- Existing spreadsheet data imported through an EF Core data migration
- Responsive Bootstrap-based interface

## Database model

`Category 1 -> many HouseholdItem 1 -> many ProductChoice`

`HouseholdItem.SelectedProductChoiceId` points to the preferred/winning product. This second relationship is configured explicitly with `DeleteBehavior.Restrict`; product choices belonging to the item use cascade delete.

## Migrations

The project uses code-first EF Core migrations. Existing migrations are in `Data/Migrations`:

1. `20260801153000_InitialCreate` creates Categories, HouseholdItems and ProductChoices.
2. `20260801170000_ImportExistingSpreadsheetItems` inserts the spreadsheet items and their URLs.
3. `20260802183000_AddProductChoiceImageUrl` adds an optional direct image URL to each purchase option.

Apply them with:

```powershell
dotnet restore
dotnet ef database update
dotnet run
```

For a future model change:

```powershell
dotnet ef migrations add DescribeTheChange --output-dir Data/Migrations
dotnet ef database update
```

Never edit an already-applied migration in a shared project. Add a new migration instead.

## Workflow

1. Select **Add item** and define what the household needs.
2. Save. The app redirects to the item details page.
3. Select **Add product choice**.
4. Enter the product name, tier, retailer, price and **Product URL**.
5. Mark a choice preferred or purchased.

## Project structure

- `Controllers/DashboardController.cs`: aggregated dashboard queries
- `Controllers/ItemsController.cs`: item list, filters, create and details
- `Controllers/ProductChoicesController.cs`: product-choice lifecycle and preferred/purchased rules
- `Models/Entities`: persisted EF entities
- `Models/ViewModels`: form and dashboard-specific models
- `Data/Configurations`: Fluent API mapping
- `Data/Migrations`: schema and spreadsheet import history
- `Views`: Razor UI

## Connection string

The default uses SQL Server LocalDB and is configured in `appsettings.json`. Change `DefaultConnection` when using SQL Server Express, Docker SQL Server or another machine.

## Notes

This build deliberately keeps product image uploads, receipt files, warranty tracking and price-history tables as future migrations rather than mixing them into the core workflow before the item/product-choice experience is stable.

## Enhancement: item notes, editing, deletion and value summaries

This version implements the changes requested in `change.docx`:

- General notes are displayed in a dedicated panel on the item details page.
- The overall household item can be edited from its details page.
- The overall household item can be deleted through a confirmation page. Deleting an item also deletes its related purchase options.
- The dashboard now shows two distinct totals:
  - **Current plan value**: one option per item. It uses the preferred/selected option, or the cheapest option when no preference has been selected.
  - **All options logged**: the value of every purchase option entered, including alternatives. This is a research total and should not be treated as planned spend.
- Each item details page shows:
  - combined value of all logged options;
  - cheapest logged option;
  - highest logged option;
  - preferred option value.

### Why no migration was added

These changes use fields and relationships already present in the database (`GeneralNotes`, `TargetBudget`, `ProductChoices`, and `SelectedProductChoiceId`). They add controller actions, view models, calculations and Razor views only, so the database schema has not changed and no new EF Core migration is required.

### Value calculation rules

For a purchase option, its displayed total is:

```text
Price × Quantity
```

The dashboard's **Current plan value** is calculated once per household item. This avoids adding both a premium and budget alternative to the amount you are realistically planning to spend.


## Product image previews

Each purchase option can store an optional `ImageUrl`. Enter it on the create or edit purchase-option screen. The form shows an immediate preview, and the item-details page displays the image above the option information.

The value must be a direct, publicly accessible image URL. An Amazon product-page URL cannot be used as an image source. Existing options remain valid and show a neutral placeholder until an image URL is added. Broken or blocked image links fall back to an “Image unavailable” message rather than breaking the card layout.

Apply the schema change with:

```powershell
dotnet ef database update
```

## Amazon URL metadata import

The purchase-option create and edit forms now include a **Fetch Amazon details** button.

The import process:

1. Accepts `amazon.co.uk`, `amazon.com` and `amzn.eu` links only.
2. Resolves Amazon shortened links on the server.
3. Reads product metadata that Amazon exposes in the returned HTML.
4. Attempts to populate the product name, current price, image URL and retailer.
5. Leaves all fields editable and requires the user to save the form normally.

The implementation is intentionally isolated behind `IProductMetadataService`. Retailer HTML is not a stable API, so failures are treated as normal and the user can continue with manual entry. The service also restricts outbound requests to approved Amazon hosts to reduce SSRF risk, limits the amount of HTML read, and uses a short HTTP timeout.

Key files:

- `Services/ProductMetadata/IProductMetadataService.cs`
- `Services/ProductMetadata/AmazonProductMetadataService.cs`
- `Models/Requests/ProductMetadataRequest.cs`
- `Models/Results/ProductMetadataResult.cs`
- `Controllers/ProductChoicesController.cs`
- `Views/ProductChoices/_Form.cshtml`

No database migration is required for this feature because it populates existing `ProductChoice` fields.

## Amazon image preview fix

Amazon image hosts sometimes reject direct browser hotlinking. The application now serves approved Amazon image URLs through `ProductChoices/ImagePreview`, adding browser-like request headers and validating the destination host to prevent arbitrary URL proxying. The importer also checks `og:image`, `twitter:image`, the `landingImage` element, dynamic image data and common Amazon page JSON properties.

No further migration is required for this fix because it continues to use `ProductChoices.ImageUrl`.

## Amazon import button fix

The product form now renders the antiforgery token before the reusable form partial and resolves the token when the Fetch Amazon details button is clicked. Previously, the JavaScript could run before ASP.NET had emitted the form token, causing it to return without attaching the click handler. The visible symptom was that pressing the button appeared to do nothing.

## Multiple products in one preferred plan

A household item can now contain more than one purchase option in its preferred plan.
This is useful when the overall requirement needs several different products, for example:

- a washing-up setup containing a bowl and a draining rack;
- a baking set containing a mixer and separate attachments;
- a room item containing two different lamps;
- multiple packs or units of the same product.

Each purchase option has a **Purchase quantity**. Its planned value is calculated as:

```text
Current price × Purchase quantity
```

The item-level **Preferred plan** total adds together every option marked
`IsPreferred`, including each option's purchase quantity. When no preferred options
have been selected, the dashboard continues to use the cheapest logged option as the
budget fallback for that household item.

The existing `SelectedProductChoiceId` column is retained for compatibility. It is set
only when exactly one option is preferred. Multiple selected options are represented by
the `ProductChoices.IsPreferred` flag, so this feature requires no new migration.

## Grouped All Items page

The All Items page now treats `Category` as a visual subsection rather than displaying one flat list.

- Categories are ordered alphabetically.
- Items within each category are ordered alphabetically.
- Each category is an expandable/collapsible `<details>` section.
- Section headers show item count, purchased count, completion percentage and current plan value.
- The current plan value uses all preferred purchase options. If none are preferred, it uses the cheapest logged option, matching the dashboard.
- The page includes totals for filtered items, purchased items, current plan value and purchased value.
- Search, category and status filters remain available.

This version does not require a migration because it only changes query ordering, view models and presentation. A future `SubCategory` entity can be introduced if categories such as Kitchen need nested groups such as Cooking, Food Preparation and Storage.
