// ============================================================================
// CONFIGURATION & GLOBAL STATE MANAGER
// ============================================================================
const API_BASE_URL = "http://localhost:5244/api"; 
let stockHistory = [];

// DOM Elements
const staffFilterSelect = document.getElementById("staffFilterSelect");
const btnClearStaffFilter = document.getElementById("btnClearStaffFilter");
const historyItemSearchInput = document.getElementById("historyItemSearchInput");
const tableBody = document.getElementById("activityTable");

// ============================================================================
// RUNTIME INITIALIZATION & POPULATION PIPELINE
// ============================================================================
async function initStockHistoryPage() {
    await populateStaffDropdownFilter();
    await fetchStockHistoryFromServer();
    setupFilterEventListeners();
}

// ============================================================================
// DYNAMIC DROPDOWN GENERATION ROUTINE
// ============================================================================
async function populateStaffDropdownFilter() {
    if (!staffFilterSelect) return;

    try {
        const response = await window.kfFetch(`${API_BASE_URL}/staff`);
        if (!response.ok) throw new Error(`Staff API status code: ${response.status}`);

        const staffList = await response.json();
        const activeStaff = staffList.filter(s => s.active === true || s.Active === true);

        staffFilterSelect.innerHTML = `<option value="all">All Stock Controllers</option>`;

        activeStaff.forEach(staff => {
            const firstName = (staff.firstName || staff.FirstName || "").trim();
            const lastName = (staff.lastName || staff.LastName || "").trim();
            
            const filterValueObj = {
                first: firstName.toLowerCase(),
                last: lastName.toLowerCase()
            };

            if (firstName || lastName) {
                const option = document.createElement("option");
                option.value = JSON.stringify(filterValueObj);
                option.textContent = `${firstName} ${lastName}`.trim();
                staffFilterSelect.appendChild(option);
            }
        });
    } catch (error) {
        console.error("Failed to populate stock controller drop-down elements:", error);
    }
}

// ============================================================================
// CORE DATA FETCH ROUTINE (READ FROM ENDPOINT)
// ============================================================================
async function fetchStockHistoryFromServer() {
    try {
        const response = await window.kfFetch(`${API_BASE_URL}/inventory/all-history`);
        if (!response.ok) throw new Error(`HTTP network error! Status: ${response.status}`);
 
        const serverData = await response.json();
 
        stockHistory = serverData.sort((a, b) => {
            const idA = a.SH_ID ?? a.sh_ID ?? 0;
            const idB = b.SH_ID ?? b.sh_ID ?? 0;
            return idB - idA;
        }); 
        
        renderHistory();
    } catch (error) {
        console.error("Critical log synchronization failure:", error);
        if (tableBody) {
            tableBody.innerHTML = `
                <tr>
                    <td colspan="9" style="text-align:center; color:red; font-weight:bold; padding:20px;">
                        Failed to synchronize transactional history logs.
                    </td>
                </tr>`;
        }
    }
}

// ============================================================================
// FORMATTERS & RENDERING ENGINE
// ============================================================================
function formatUtdValue(rawVal) {
    if (!rawVal || rawVal === "N/A" || rawVal === 0) return "N/A";
    return window.KFFormat.formatDateMMDDYYYY(rawVal);
}
 
function renderHistory() {
    if (!tableBody) return;
    tableBody.innerHTML = "";

    const selectedValue = staffFilterSelect ? staffFilterSelect.value : "all";
    
    // 🎯 Grab input and convert to lowercase for non-sensitive matching
    const searchKeyword = historyItemSearchInput ? historyItemSearchInput.value.trim().toLowerCase() : "";

    // Comprehensive filtering system
    const filteredHistory = stockHistory.filter(act => {
        const itemName = (act.itemName || act.ItemName || "").toLowerCase();
        
        // 1. Text Search Filter Validation (Checks if string contains the pattern)
        if (searchKeyword && !itemName.includes(searchKeyword)) {
            return false; 
        }

        // 2. Dropdown Staff Controller Validation
        if (selectedValue === "all") return true;

        try {
            const targets = JSON.parse(selectedValue);
            const performer = (act.performedBy || act.PerformedBy || "").toLowerCase();
            const approver = (act.approvedBy || act.ApprovedBy || "").toLowerCase();

            const matchPerformer = performer.includes(targets.first) && performer.includes(targets.last);
            const matchApprover = approver.includes(targets.first) && approver.includes(targets.last);

            return matchPerformer || matchApprover;
        } catch (e) {
            return false;
        }
    });

    if (filteredHistory.length === 0) {
        tableBody.innerHTML = `
            <tr>
                <td colspan="9" style="text-align:center; color:#888; padding:20px;">
                    No transaction logs found matching your criteria.
                </td>
            </tr>`;
        return;
    }

    filteredHistory.forEach(act => {
        const row = document.createElement("tr");

        const currentAction = act.action || act.Action || "Stock-In";
        const curQty = act.quantity ?? act.Quantity ?? 0;
        const oldQty = act.oldQuantity ?? act.OldQuantity ?? 0;
        const curPrice = parseFloat(act.price || act.Price || 0);
        const oldPrice = parseFloat(act.oldPrice || act.OldPrice || 0);
        const curUtd = act.utd ?? act.UTD ?? 0;
        const oldUtd = act.oldUTD ?? act.OldUTD ?? 0;
        const curCat = act.category || act.Category || "General";
        const oldCat = act.oldCategory || act.OldCategory || "General";
        
        const performer = act.performedBy || act.PerformedBy || "System Auto";
        const approver = act.approvedBy || act.ApprovedBy || "N/A";
        const performerPos = act.performedByPosition || act.PerformedByPosition || null;
        const approverPos = act.approvedByPosition || act.ApprovedByPosition || null;
        const performerDisplay = performerPos ? `${performer} <span class="role-tag">(${performerPos})</span>` : performer;
        const approverDisplay = approverPos ? `${approver} <span class="role-tag">(${approverPos})</span>` : approver;
        const displayTime = window.KFFormat.formatDateTimeDisplay(act.dateTime || act.DateTime || "N/A");
        const itemName = act.itemName || act.ItemName || "Unknown Item";

        let typeClass = "action-badge text-stock-in";
        const lowerAction = currentAction.toLowerCase();
        if (lowerAction.includes("out")) typeClass = "action-badge text-stock-out";
        else if (lowerAction.includes("add")) typeClass = "action-badge text-add-item";
        else if (lowerAction.includes("edit")) typeClass = "action-badge text-near-expiry";
 
        const isAdd = lowerAction.includes("add") || lowerAction.includes("fresh");
        const qtyDisplay = (isAdd || oldQty === curQty) ? `${curQty}` : `${oldQty} → ${curQty}`;
        const priceDisplay = (isAdd || oldPrice === curPrice) ? `₱${curPrice.toFixed(0)}` : `₱${oldPrice.toFixed(0)} → ₱${curPrice.toFixed(0)}`;
        const utdDisplay = (isAdd || oldUtd === curUtd) ? formatUtdValue(curUtd) : `${formatUtdValue(oldUtd)} → ${formatUtdValue(curUtd)}`;
        const catDisplay = (isAdd || oldCat === curCat) ? curCat : `${oldCat} → ${curCat}`;
 
        row.innerHTML = `
            <td>${displayTime}</td>
            <td><strong>${itemName}</strong></td>
            <td><span class="${typeClass}" style="padding: 4px 8px; border-radius: 4px; font-weight: bold; font-size: 11px;">${currentAction}</span></td>
            <td><strong>${qtyDisplay}</strong></td>
            <td><strong>${priceDisplay}</strong></td>
            <td>${utdDisplay}</td>
            <td>${catDisplay}</td>
            <td>${performerDisplay}</td>
            <td>${approverDisplay}</td>
        `;
 
        tableBody.appendChild(row);
    });
}

// ============================================================================
// EVENT LISTENERS CONTROL HANDLERS
// ============================================================================
function setupFilterEventListeners() {
    if (!staffFilterSelect) return;

    // Trigger on dropdown updates
    staffFilterSelect.addEventListener("change", () => {
        renderHistory();
        if (staffFilterSelect.value !== "all") {
            if (btnClearStaffFilter) btnClearStaffFilter.style.display = "inline-block";
        } else {
            if (btnClearStaffFilter) btnClearStaffFilter.style.display = "none";
        }
    });

    // 🎯 Trigger real-time search filtering on input typing
    if (historyItemSearchInput) {
        historyItemSearchInput.addEventListener("input", () => {
            renderHistory();
        });
    }

    // Reset layout choices
    if (btnClearStaffFilter) {
        btnClearStaffFilter.addEventListener("click", () => {
            staffFilterSelect.value = "all";
            if (historyItemSearchInput) historyItemSearchInput.value = ""; // Clear search text too
            btnClearStaffFilter.style.display = "none";
            renderHistory();
        });
    }
}
 
window.addEventListener("focus", fetchStockHistoryFromServer);
 
if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", initStockHistoryPage);
} else {
    initStockHistoryPage();
}