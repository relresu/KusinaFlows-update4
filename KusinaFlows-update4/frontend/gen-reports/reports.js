// ============================================================================
// CONFIGURATION & GLOBAL STATE
// ============================================================================
const API_BASE_URL = "http://localhost:5244/api";

let rawInventory = [];
let rawHistory = [];
let currentReportType = null; // "inventory" | "movement" | "finance"

// DOM Elements - KPI Cards
const totalItemsReportEl = document.getElementById("totalItemsReport");
const totalValueReportEl = document.getElementById("totalValueReport");
const lowStockReportEl   = document.getElementById("lowStockReport");

// DOM Elements - Date Range Filter
const reportFromDate        = document.getElementById("reportFromDate");
const reportToDate          = document.getElementById("reportToDate");
const btnClearDateRange     = document.getElementById("btnClearDateRange");
const reportDateRangeWarning = document.getElementById("reportDateRangeWarning");

// DOM Elements - Report Triggers & Output
const btnInventoryReport   = document.getElementById("btnInventoryReport");
const btnMovementReport    = document.getElementById("btnMovementReport");
const btnFinanceReport     = document.getElementById("btnFinanceReport");
const btnPrintReport       = document.getElementById("btnPrintReport");
const reportTableContainer = document.getElementById("reportTableContainer");
const printReportTitle     = document.getElementById("printReportTitle");
const printReportSubtitle  = document.getElementById("printReportSubtitle");
const reportTableHead      = document.getElementById("reportTableHead");
const reportTableBody      = document.getElementById("reportTableBody");

// ============================================================================
// INITIALIZATION
// ============================================================================
async function initReportsPage() {
    // Block picking a future date directly in the native date picker
    const todayStr = new Date().toISOString().slice(0, 10);
    if (reportFromDate) reportFromDate.max = todayStr;
    if (reportToDate) reportToDate.max = todayStr;

    try {
        const [invRes, histRes] = await Promise.all([
            window.kfFetch(`${API_BASE_URL}/inventory`),
            window.kfFetch(`${API_BASE_URL}/inventory/all-history`)
        ]);

        rawInventory = invRes.ok ? await invRes.json() : [];
        rawHistory   = histRes.ok ? await histRes.json() : [];

        renderKpiCards();
    } catch (error) {
        console.error("Failed to load report data:", error);
    }
}

// ============================================================================
// KPI SUMMARY CARDS (mirrors the Dashboard's stock-level definitions)
// ============================================================================
function renderKpiCards() {
    const qtyByItem = {};
    let totalValue = 0;

    rawInventory.forEach(r => {
        const name  = r.itemName ?? r.ItemName ?? "Unknown";
        const qty   = r.quantity ?? r.Quantity ?? 0;
        const price = r.price    ?? r.Price    ?? 0;

        qtyByItem[name] = (qtyByItem[name] || 0) + qty;
        totalValue += qty * price;
    });

    const itemNames = Object.keys(qtyByItem);
    const lowStockCount = itemNames.filter(name => qtyByItem[name] > 0 && qtyByItem[name] <= 5).length;

    if (totalItemsReportEl) totalItemsReportEl.textContent = itemNames.length;
    if (totalValueReportEl) totalValueReportEl.textContent = `₱${totalValue.toLocaleString("en-PH", { minimumFractionDigits: 2 })}`;
    if (lowStockReportEl)   lowStockReportEl.textContent   = lowStockCount;
}

// ============================================================================
// DATE RANGE HELPERS
// ============================================================================
function getDateRange() {
    const from = reportFromDate.value ? new Date(reportFromDate.value + "T00:00:00") : null;
    const to   = reportToDate.value   ? new Date(reportToDate.value + "T23:59:59")   : null;
    return { from, to };
}

// Blocks From-after-To picks and dates set in the future (no data can exist
// for a period that hasn't happened yet). Returns true when the current
// selection is usable; shows an inline warning and returns false otherwise.
function validateDateRange() {
    const { from, to } = getDateRange();
    const now = new Date();

    let message = "";
    if (from && to && from > to) {
        message = "\"From\" date must be on or before the \"To\" date.";
    } else if (from && from > now) {
        message = "\"From\" date can't be in the future.";
    } else if (to && to > now) {
        message = "\"To\" date can't be in the future.";
    }

    if (reportDateRangeWarning) {
        reportDateRangeWarning.textContent = message;
        reportDateRangeWarning.style.display = message ? "block" : "none";
    }

    if (message) {
        reportTableContainer.classList.add("hidden");
        return false;
    }
    return true;
}

function isWithinRange(rawDateValue, from, to) {
    if (!from && !to) return true;
    const d = window.KFFormat.parseAnyDate(rawDateValue);
    if (!d) return false;
    if (from && d < from) return false;
    if (to && d > to) return false;
    return true;
}

function formatRangeLabel(from, to) {
    if (!from && !to) return "All-Time";
    if (from && !to) return `From ${window.KFFormat.formatDateMMDDYYYY(from)}`;
    if (!from && to) return `Through ${window.KFFormat.formatDateMMDDYYYY(to)}`;
    return `${window.KFFormat.formatDateMMDDYYYY(from)} – ${window.KFFormat.formatDateMMDDYYYY(to)}`;
}

function showReportContainer() {
    if (reportTableContainer) reportTableContainer.classList.remove("hidden");
}

// ============================================================================
// REPORT 1: CURRENT INVENTORY LEVEL
// Snapshot of currently in-stock batches, narrowed to those added within the
// selected date range (a true historical snapshot isn't tracked, so "added
// within range and still in stock today" is the closest faithful equivalent).
// ============================================================================
function renderInventoryReport() {
    currentReportType = "inventory";
    if (!validateDateRange()) return;
    const { from, to } = getDateRange();

    const rows = rawInventory.filter(r => isWithinRange(r.dateAdded ?? r.DateAdded, from, to));

    printReportTitle.textContent  = "Current Inventory Level Report";
    printReportSubtitle.textContent =
        `KusinaFlow Inventory — ${formatRangeLabel(from, to)} — Generated ${window.KFFormat.formatDateTimeDisplay(new Date())}`;

    reportTableHead.innerHTML = `
        <tr>
            <th>Item Name</th><th>Category</th><th>Price</th>
            <th>Quantity</th><th>Status</th><th>Date Added</th>
        </tr>
    `;

    if (rows.length === 0) {
        reportTableBody.innerHTML = `<tr><td colspan="6" style="text-align:center;padding:20px;color:#888;">No inventory items found for the selected range.</td></tr>`;
        showReportContainer();
        return;
    }

    reportTableBody.innerHTML = rows.map(r => {
        const qty   = r.quantity ?? r.Quantity ?? 0;
        const price = r.price    ?? r.Price    ?? 0;

        let status = "In Stock";
        if (qty === 0) status = "Out of Stock";
        else if (qty <= 5) status = "Low Stock";

        return `
            <tr>
                <td><strong>${r.itemName ?? r.ItemName}</strong></td>
                <td>${r.category ?? r.Category}</td>
                <td>₱${price.toLocaleString("en-PH", { minimumFractionDigits: 2 })}</td>
                <td>${qty}</td>
                <td>${status}</td>
                <td>${window.KFFormat.formatDateMMDDYYYY(r.dateAdded ?? r.DateAdded)}</td>
            </tr>
        `;
    }).join("");

    showReportContainer();
}

// ============================================================================
// REPORT 2: STOCK MOVEMENT
// Mirrors the Stock History page's table format exactly (same 9 columns,
// same role-tag styling, same MM/DD/YYYY + 12-hour time formatting).
// ============================================================================
function renderMovementReport() {
    currentReportType = "movement";
    if (!validateDateRange()) return;
    const { from, to } = getDateRange();

    const rows = rawHistory.filter(h => isWithinRange(h.dateTime ?? h.DateTime, from, to));

    printReportTitle.textContent  = "Stock Movement Report";
    printReportSubtitle.textContent =
        `KusinaFlow Transaction Log — ${formatRangeLabel(from, to)} — Generated ${window.KFFormat.formatDateTimeDisplay(new Date())}`;

    reportTableHead.innerHTML = `
        <tr>
            <th>Date & Time</th><th>Item Name</th><th>Action</th><th>Quantity</th>
            <th>Price</th><th>UTD</th><th>Category</th><th>Performed By</th><th>Approved By</th>
        </tr>
    `;

    if (rows.length === 0) {
        reportTableBody.innerHTML = `<tr><td colspan="9" style="text-align:center;padding:20px;color:#888;">No transactions found for the selected range.</td></tr>`;
        showReportContainer();
        return;
    }

    reportTableBody.innerHTML = rows.map(buildMovementRow).join("");
    showReportContainer();
}

function buildMovementRow(act) {
    const currentAction = act.action || act.Action || "Stock-In";
    const curQty   = act.quantity    ?? act.Quantity    ?? 0;
    const oldQty   = act.oldQuantity ?? act.OldQuantity ?? 0;
    const curPrice = parseFloat(act.price    ?? act.Price    ?? 0);
    const oldPrice = parseFloat(act.oldPrice ?? act.OldPrice ?? 0);
    const curUtd   = act.utd    ?? act.UTD    ?? 0;
    const oldUtd   = act.oldUTD ?? act.OldUTD ?? 0;
    const curCat   = act.category    ?? act.Category    ?? "General";
    const oldCat   = act.oldCategory ?? act.OldCategory ?? "General";

    const performer    = act.performedBy ?? act.PerformedBy ?? "System Auto";
    const approver     = act.approvedBy  ?? act.ApprovedBy  ?? "N/A";
    const performerPos = act.performedByPosition ?? act.PerformedByPosition ?? null;
    const approverPos  = act.approvedByPosition  ?? act.ApprovedByPosition  ?? null;
    const performerDisplay = performerPos ? `${performer} <span class="role-tag">(${performerPos})</span>` : performer;
    const approverDisplay  = approverPos  ? `${approver} <span class="role-tag">(${approverPos})</span>`  : approver;

    const displayTime = window.KFFormat.formatDateTimeDisplay(act.dateTime ?? act.DateTime ?? "N/A");
    const itemName    = act.itemName ?? act.ItemName ?? "Unknown Item";
    const batchId     = act.batchID  ?? act.BatchID  ?? null;
    const itemNameDisplay = batchId ? `${itemName} <span class="role-tag">(#BTC-${batchId})</span>` : itemName;

    const lowerAction = currentAction.toLowerCase();
    const isAdd = lowerAction.includes("add") || lowerAction.includes("fresh");

    const qtyDisplay   = (isAdd || oldQty === curQty)     ? `${curQty}`                                : `${oldQty} → ${curQty}`;
    const priceDisplay = (isAdd || oldPrice === curPrice) ? `₱${curPrice.toFixed(0)}`                   : `₱${oldPrice.toFixed(0)} → ₱${curPrice.toFixed(0)}`;
    const utdDisplay   = (isAdd || oldUtd === curUtd)     ? window.KFFormat.formatDateMMDDYYYY(curUtd)  : `${window.KFFormat.formatDateMMDDYYYY(oldUtd)} → ${window.KFFormat.formatDateMMDDYYYY(curUtd)}`;
    const catDisplay   = (isAdd || oldCat === curCat)     ? curCat                                      : `${oldCat} → ${curCat}`;

    return `
        <tr>
            <td>${displayTime}</td>
            <td><strong>${itemNameDisplay}</strong></td>
            <td>${currentAction}</td>
            <td>${qtyDisplay}</td>
            <td>${priceDisplay}</td>
            <td>${utdDisplay}</td>
            <td>${catDisplay}</td>
            <td>${performerDisplay}</td>
            <td>${approverDisplay}</td>
        </tr>
    `;
}

// ============================================================================
// REPORT 3: EXPENSE / FINANCIAL
// There's no dedicated Expense entity in the schema — this report defines
// "expense" as money spent restocking inventory (Add Item / Stock-In
// transactions), which is the only cost data the app actually tracks.
// ============================================================================
function renderFinanceReport() {
    currentReportType = "finance";
    if (!validateDateRange()) return;
    const { from, to } = getDateRange();

    const rows = rawHistory.filter(h => {
        const action = (h.action ?? h.Action ?? "").toLowerCase();
        const isRestock = action.includes("add") || action.includes("stock-in");
        return isRestock && isWithinRange(h.dateTime ?? h.DateTime, from, to);
    });

    printReportTitle.textContent  = "Expense / Financial Report";

    let totalExpense = 0;
    rows.forEach(h => {
        const qty   = h.quantity ?? h.Quantity ?? 0;
        const price = parseFloat(h.price ?? h.Price ?? 0);
        totalExpense += qty * price;
    });

    printReportSubtitle.textContent =
        `KusinaFlow — Money spent restocking inventory (Add Item / Stock-In transactions) — ${formatRangeLabel(from, to)} — Generated ${window.KFFormat.formatDateTimeDisplay(new Date())}`;

    reportTableHead.innerHTML = `
        <tr>
            <th>Date & Time</th><th>Item Name</th><th>Category</th><th>Quantity</th>
            <th>Unit Price</th><th>Total Cost</th><th>Performed By</th><th>Approved By</th>
        </tr>
    `;

    if (rows.length === 0) {
        reportTableBody.innerHTML = `<tr><td colspan="8" style="text-align:center;padding:20px;color:#888;">No restocking transactions found for the selected range.</td></tr>`;
        showReportContainer();
        return;
    }

    const bodyRows = rows.map(h => {
        const qty       = h.quantity ?? h.Quantity ?? 0;
        const price     = parseFloat(h.price ?? h.Price ?? 0);
        const lineTotal = qty * price;

        const performer    = h.performedBy ?? h.PerformedBy ?? "System Auto";
        const approver     = h.approvedBy  ?? h.ApprovedBy  ?? "N/A";
        const performerPos = h.performedByPosition ?? h.PerformedByPosition ?? null;
        const approverPos  = h.approvedByPosition  ?? h.ApprovedByPosition  ?? null;
        const performerDisplay = performerPos ? `${performer} <span class="role-tag">(${performerPos})</span>` : performer;
        const approverDisplay  = approverPos  ? `${approver} <span class="role-tag">(${approverPos})</span>`  : approver;

        return `
            <tr>
                <td>${window.KFFormat.formatDateTimeDisplay(h.dateTime ?? h.DateTime)}</td>
                <td><strong>${h.itemName ?? h.ItemName}</strong></td>
                <td>${h.category ?? h.Category}</td>
                <td>${qty}</td>
                <td>₱${price.toLocaleString("en-PH", { minimumFractionDigits: 2 })}</td>
                <td>₱${lineTotal.toLocaleString("en-PH", { minimumFractionDigits: 2 })}</td>
                <td>${performerDisplay}</td>
                <td>${approverDisplay}</td>
            </tr>
        `;
    }).join("");

    const totalRow = `
        <tr style="font-weight:bold; background:#f7f2ea;">
            <td colspan="5" style="text-align:right;">Total Expenses</td>
            <td>₱${totalExpense.toLocaleString("en-PH", { minimumFractionDigits: 2 })}</td>
            <td colspan="2"></td>
        </tr>
    `;

    reportTableBody.innerHTML = bodyRows + totalRow;
    showReportContainer();
}

// ============================================================================
// EVENT WIRING
// ============================================================================
function rerenderActiveReport() {
    if (currentReportType === "inventory") renderInventoryReport();
    else if (currentReportType === "movement") renderMovementReport();
    else if (currentReportType === "finance") renderFinanceReport();
}

if (btnInventoryReport) btnInventoryReport.addEventListener("click", renderInventoryReport);
if (btnMovementReport)  btnMovementReport.addEventListener("click", renderMovementReport);
if (btnFinanceReport)   btnFinanceReport.addEventListener("click", renderFinanceReport);
if (btnPrintReport)     btnPrintReport.addEventListener("click", () => window.print());

[reportFromDate, reportToDate].forEach(el => {
    if (!el) return;
    el.addEventListener("change", () => {
        validateDateRange(); // shows/clears the inline warning even before a report is picked
        rerenderActiveReport();
    });
});

if (btnClearDateRange) {
    btnClearDateRange.addEventListener("click", () => {
        reportFromDate.value = "";
        reportToDate.value = "";
        validateDateRange();
        rerenderActiveReport();
    });
}

// ============================================================================
// LIFECYCLE BOOTSTRAP
// ============================================================================
if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", initReportsPage);
} else {
    initReportsPage();
}
