*Put a full description of the app here


*Versions
1.0 - 28/05/2026
• First version of the login & sign-up page. 
• Includes the typical login information and sign-up page (Matros Arcallana)
• First version of Inventory system (Kirsten Licup)

1.1 - 31/05/2026
- Removed the sign-up button when logging in and made it hidden to avoid random users creating accounts. Makes it easier for managers/owners to create accounts for employees.
+ Changed the background photo from a kitchen into a coffee shop. 
**Changes made by: (John Miles Varca)**

- Removed Time Tracker
+ Added a UTD option when adding an item
+ Added a Log-Out button
+ Added 2 versions: Manager (all access), and Employee (Limited to Stock-in and Stock-out only)
+ Added a Check Details button in the item modal.
+ Added a logo on the top left corner
• Fixed a bug where Inventory value doesn’t decrease when deleting an item
**Changes made by: Kirsten Licup**

1.2 - 01/06/26

- Removed Check Details
- Removed Item ID
+ Added a drop-down menu feature on the main item modal to show all new “stock-ins”, along with their UTD
+ Implemented FIFO rule, wherein new “stock-ins” having an earlier UTD will be pushed to the top of the drop-down menu.
+ Added UTD option when stocking-in.
• Fixed a bug where when an Item’s Quantity drops to (0), it doesn’t disappear in the item options when stocking-out
**Changes made by: Kirsten Licup**

1.3 - 02/06/26
• Changed low-stock into a button
**Change made by: Kirsten Licup**

1.4 05/06/26
Changed color palette of Log-in/Sign-up Page
Fixed code inconsistencies
Fixed a bug where password crashes when empty
**Change made by: Kirsten Licup**

1.5 - 20/06/26
+ Repo now split into three top-level folders: backend/, middleware/, frontend/ (this README stays outside all three)
+ Auth rebuilt on JWT bearer tokens (middleware/ project), replacing the old in-memory session store — stays valid across backend restarts
+ Server-side privilege checks added: Managers can no longer create/edit Manager or Owner accounts via direct API calls, only through the UI's existing restrictions
+ Removed TestController.cs (diagnostics-only, not required for the app to run) and an unused/unreferenced legacy model (ProductCreateDto.cs)
+ Dashboard rearranged: KPI cards now sit in a 2x2 grid beside Top Stocked Items, Recent Stock Activity spans full width
• Fixed print output cropping/scrollbar artifacts on the Reports and Staff Management print views
• Fixed Bulk Deactivate giving a misleading error on session expiry; added the missing Manager/Owner privilege check
+ Added date-range validation on Generate Reports (blocks From-after-To and future-dated picks)
+ Tightened overall UI spacing/sizing app-wide for a more compact look
**Changes made by: Claude (AI pair-programming session)**

