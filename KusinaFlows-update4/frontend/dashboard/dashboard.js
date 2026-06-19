// ============================================================================
// CONFIGURATION & LIVE BACKEND CORRELATION
// ============================================================================
const API_BASE_URL = "http://localhost:5244/api"; 

let rawInventory = [];

// DOM Elements - KPI Summary Cards
const totalProductsEl = document.getElementById("totalProducts");
const lowStockEl      = document.getElementById("lowStock");
const outOfStockEl    = document.getElementById("outOfStock");
const inStockEl       = document.getElementById("inStock");
const topItemEl       = document.getElementById("topItem");

// DOM Elements - Interactive Components
const activityTable   = document.getElementById("activityTable");
const topStockedChart = document.getElementById("topStockedChart");

// ============================================================================
// INITIALIZATION
// ============================================================================
async function initDashboard() {
  try {
    const inventoryResponse = await window.kfFetch(`${API_BASE_URL}/inventory`);
    if (!inventoryResponse.ok) throw new Error(`Server status: ${inventoryResponse.status}`);

    // Normalize Pascal case API response → camelCase
    const raw = await inventoryResponse.json();
    rawInventory = raw.map(r => ({
      batchID:   r.BatchID   ?? r.batchID,
      itemID:    r.ItemID    ?? r.itemID,
      itemName:  r.ItemName  ?? r.itemName  ?? "Unknown",
      category:  r.Category  ?? r.category  ?? "General",
      price:     r.Price     ?? r.price      ?? 0,
      quantity:  r.Quantity  ?? r.quantity   ?? 0,
      available: r.Available ?? r.available  ?? false,
      UTD:       r.UTD       ?? r.utd        ?? 0,
    }));

    renderSummaryCards();
    renderTopStockedChart();
    await fetchRecentActivityLogs();

  } catch (error) {
    console.error("Dashboard crash:", error);
    if (totalProductsEl) totalProductsEl.textContent = "⚠️";
    if (topItemEl)       topItemEl.textContent       = "Server Offline";
  }
}

// ============================================================================
// UTD HELPER
// ============================================================================
function formatUtdValue(rawIntegerUtd) {
  if (!rawIntegerUtd) return "N/A";
  return window.KFFormat.formatDateMMDDYYYY(rawIntegerUtd);
}

function utdToDate(utdInt) {
  const s = String(utdInt ?? 0).padStart(8, '0');
  return new Date(parseInt(s.slice(0,4)), parseInt(s.slice(4,6)) - 1, parseInt(s.slice(6,8)));
}

// ============================================================================
// SUMMARY CARDS
// Rules:
//   Available=false  → deleted by user → exclude entirely from all counts
//   Available=true, qty=0 → Out of Stock
//   Available=true, qty 1-5 → Low Stock
//   Available=true, qty 6+ → In Stock
//   Expired batches are not counted toward qty but the item still appears
// ============================================================================
function renderSummaryCards() {
  const uniqueItemsMap = {};
  const today = new Date();
  today.setHours(0, 0, 0, 0);

  rawInventory.forEach(row => {
    // Skip deleted batches (Available=false means hard-deleted, not out-of-stock)
    if (row.available === false) return;

    const name  = row.itemName ?? "Unknown";
    const qty   = row.quantity ?? 0;
    const price = row.price    ?? 0;

    const expDate = utdToDate(row.UTD);
    expDate.setHours(0, 0, 0, 0);
    const isExpired = expDate < today;

    if (!uniqueItemsMap[name]) {
      uniqueItemsMap[name] = { totalQty: 0, price };
    }

    // Only count non-expired quantity
    if (!isExpired) {
      uniqueItemsMap[name].totalQty += qty;
    }
  });

  const productsList = Object.keys(uniqueItemsMap);
  let lowStockCount    = 0;
  let outOfStockCount  = 0;
  let inStockCount     = 0;
  let highestQty       = -1;
  let highestStockName = "-";

  productsList.forEach(name => {
    const qty = uniqueItemsMap[name].totalQty;

    if (qty === 0)      outOfStockCount++;   // Available=true but nothing left
    else if (qty <= 5)  lowStockCount++;
    else                inStockCount++;

    if (qty > highestQty) {
      highestQty       = qty;
      highestStockName = `${name} (${qty})`;
    }
  });

  if (totalProductsEl) totalProductsEl.textContent = productsList.length;
  if (lowStockEl)      lowStockEl.textContent      = lowStockCount;
  if (outOfStockEl)    outOfStockEl.textContent    = outOfStockCount;
  if (inStockEl)       inStockEl.textContent       = inStockCount;
  if (topItemEl)       topItemEl.textContent       = highestStockName;

  // Highlight each KPI card when its respective count is above zero
  const lowStockCard   = lowStockEl   ? lowStockEl.closest(".card")   : null;
  const outOfStockCard = outOfStockEl ? outOfStockEl.closest(".card") : null;
  const inStockCard    = inStockEl    ? inStockEl.closest(".card")    : null;

  if (lowStockCard)   lowStockCard.classList.toggle("card-alert-low", lowStockCount > 0);
  if (outOfStockCard) outOfStockCard.classList.toggle("card-alert-out", outOfStockCount > 0);
  if (inStockCard)    inStockCard.classList.toggle("card-alert-in", inStockCount > 0);
}

// ============================================================================
// RECENT ACTIVITY TABLE (9 columns, Pascal-case API fields)
// ============================================================================
async function fetchRecentActivityLogs() {
  if (!activityTable) return;
  activityTable.innerHTML = "";

  try {
    const response = await window.kfFetch(`${API_BASE_URL}/inventory/all-history`);
    if (!response.ok) throw new Error(`History status: ${response.status}`);

    const historyData = await response.json();

    if (historyData.length === 0) {
      activityTable.innerHTML = `<tr><td colspan="9" style="text-align:center;padding:15px;color:#888;">No recent transactions logged yet.</td></tr>`;
      return;
    }

    historyData.slice(0, 5).forEach(log => {
      const tr = document.createElement("tr");

      const currentAction = log.Action ?? log.action ?? "Add Item";
      const actionLc      = currentAction.toLowerCase();

      let typeStyle = "background:#e3fafc;color:#0c8599;";
      if (actionLc.includes("add") || actionLc.includes("stock-in")) {
        typeStyle = "background:#d4edda;color:#28a745;";
      } else if (actionLc.includes("out") || actionLc.includes("delet")) {
        typeStyle = "background:#ffe0e3;color:#ff4757;";
      } else if (actionLc.includes("edit")) {
        typeStyle = "background:#fff3cd;color:#856404;";
      }

      const isAdd    = actionLc === "add item" || actionLc === "stock-in";
      const curQty   = log.Quantity    ?? log.quantity    ?? 0;
      const oldQty   = log.OldQuantity ?? log.oldQuantity ?? curQty;
      const curPrice = parseFloat(log.Price    ?? log.price    ?? 0);
      const oldPrice = parseFloat(log.OldPrice ?? log.oldPrice ?? curPrice);
      const curUtd   = log.UTD    ?? log.utd    ?? 0;
      const oldUtd   = log.OldUTD ?? log.oldUtd ?? curUtd;
      const curCat   = log.Category    ?? log.category    ?? "General";
      const oldCat   = log.OldCategory ?? log.oldCategory ?? curCat;

      const qtyDisplay   = (isAdd || oldQty === curQty)     ? `${curQty}`                                    : `${oldQty} → ${curQty}`;
      const priceDisplay = (isAdd || oldPrice === curPrice)  ? `₱${curPrice.toFixed(0)}`                     : `₱${oldPrice.toFixed(0)} → ₱${curPrice.toFixed(0)}`;
      const utdDisplay   = (isAdd || oldUtd === curUtd)      ? formatUtdValue(curUtd)                        : `${formatUtdValue(oldUtd)} → ${formatUtdValue(curUtd)}`;
      const catDisplay   = (isAdd || oldCat === curCat)      ? curCat                                        : `${oldCat} → ${curCat}`;

      const dateTime  = window.KFFormat.formatDateTimeDisplay(log.DateTime ?? log.dateTime ?? "N/A");
      const itemName  = log.ItemName    ?? log.itemName    ?? "Unknown";
      const performer = log.PerformedBy ?? log.performedBy ?? "System Auto";
      const approver  = log.ApprovedBy  ?? log.approvedBy  ?? "N/A";
      const performerPos = log.PerformedByPosition ?? log.performedByPosition ?? null;
      const approverPos  = log.ApprovedByPosition  ?? log.approvedByPosition  ?? null;
      const performerDisplay = performerPos ? `${performer} <span class="role-tag">(${performerPos})</span>` : performer;
      const approverDisplay  = approverPos  ? `${approver} <span class="role-tag">(${approverPos})</span>`  : approver;

      tr.innerHTML = `
        <td>${dateTime}</td>
        <td><strong>${itemName}</strong></td>
        <td><span style="padding:4px 8px;border-radius:4px;font-size:11px;font-weight:bold;display:inline-block;${typeStyle}">${currentAction}</span></td>
        <td><strong>${qtyDisplay}</strong></td>
        <td><strong>${priceDisplay}</strong></td>
        <td>${utdDisplay}</td>
        <td>${catDisplay}</td>
        <td>${performerDisplay}</td>
        <td>${approverDisplay}</td>
      `;
      activityTable.appendChild(tr);
    });

  } catch (error) {
    console.error("Dashboard activity feed error:", error);
    activityTable.innerHTML = `<tr><td colspan="9" style="text-align:center;padding:15px;color:#ff4757;font-weight:bold;">⚠️ Error loading recent activity.</td></tr>`;
  }
}

// ============================================================================
// TOP STOCKED BAR CHART
// Only counts Available=true, non-expired batches
// ============================================================================
function renderTopStockedChart() {
  if (!topStockedChart) return;
  topStockedChart.innerHTML = "";

  const uniqueItemsMap = {};
  const today = new Date();
  today.setHours(0, 0, 0, 0);

  rawInventory.forEach(row => {
    if (row.available === false) return; // skip deleted

    const name = row.itemName ?? "Unknown";
    const qty  = row.quantity ?? 0;

    const expDate = utdToDate(row.UTD);
    expDate.setHours(0, 0, 0, 0);
    if (expDate < today) return; // skip expired

    if (!uniqueItemsMap[name]) uniqueItemsMap[name] = 0;
    uniqueItemsMap[name] += qty;
  });

  const topItems = Object.keys(uniqueItemsMap)
    .map(name => ({ name, qty: uniqueItemsMap[name] }))
    .filter(item => item.qty > 0)
    .sort((a, b) => b.qty - a.qty)
    .slice(0, 5);

  if (topItems.length === 0) {
    topStockedChart.innerHTML = `<p style="text-align:center;color:#888;padding:20px;">No stocked items available to visualize.</p>`;
    return;
  }

  const maxQty = Math.max(...topItems.map(i => i.qty));

  topItems.forEach(item => {
    const pct = (item.qty / maxQty) * 100;
    const chartRow = document.createElement("div");
    chartRow.style.cssText = "margin:15px 0;display:flex;align-items:center;font-size:13px;";
    chartRow.innerHTML = `
      <div style="width:120px;font-weight:bold;text-overflow:ellipsis;overflow:hidden;white-space:nowrap;padding-right:10px;">${item.name}</div>
      <div style="flex:1;background:#eee;border-radius:4px;height:20px;margin-right:10px;overflow:hidden;">
        <div style="width:${pct}%;background:#4a3828;height:100%;border-radius:4px;transition:width 0.5s ease-in-out;"></div>
      </div>
      <div style="width:40px;font-weight:600;text-align:right;color:#4a3828;">${item.qty}</div>
    `;
    topStockedChart.appendChild(chartRow);
  });
}

// Lifecycle Bootstrapper
if (document.readyState === "loading") {
  document.addEventListener("DOMContentLoaded", initDashboard);
} else {
  initDashboard();
}