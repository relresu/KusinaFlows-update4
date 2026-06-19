// ============================================================================
// CONFIGURATION & GLOBAL STATE MANAGER
// ============================================================================
const API_BASE_URL = "http://localhost:5244/api"; 
 
let rawInventory = []; 
let inventoryGroups = []; 
let currentTransactionType = ""; 
let currentFilterMode = "DEFAULT"; 
 
// DOM Elements - Core Layout
const inventoryTableBody = document.getElementById("inventoryTableBody");
const totalItems = document.getElementById("totalItems");
const inventoryValue = document.getElementById("inventoryValue");
const searchInput = document.getElementById("searchInput");
const showUnavailableCheckbox = document.getElementById("showUnavailableCheckbox");
 
// Modals & Forms - Item Mutations
const itemModal = document.getElementById("itemModal");
const itemForm = document.getElementById("itemForm");
const modalTitle = document.getElementById("modalTitle");
const closeModal = document.getElementById("closeModal");
 
// Modals & Forms - Stock Adjustments
const stockModal = document.getElementById("stockModal");
const stockForm = document.getElementById("stockForm");
const stockModalTitle = document.getElementById("stockModalTitle");
const stockItemSelect = document.getElementById("stockItemSelect");
const stockBatchSelect = document.getElementById("stockBatchSelect");
const stockUTD = document.getElementById("stockUTD");
const stockUTDLabel = document.getElementById("stockUTDLabel");
const closeStockModal = document.getElementById("closeStockModal");
 
// Delete Approval Modal refs
const deleteModal            = document.getElementById("deleteModal");
const deleteModalTitle       = document.getElementById("deleteModalTitle");
const deleteModalSubtitle    = document.getElementById("deleteModalSubtitle");
const deleteApprovalDropdown = document.getElementById("deleteApprovalDropdown");
const deleteConfirmBtn       = document.getElementById("deleteConfirmBtn");
const closeDeleteModalBtn    = document.getElementById("closeDeleteModal");

// Edit Price Modal refs
const editPriceModal            = document.getElementById("editPriceModal");
const editPriceSubtitle         = document.getElementById("editPriceSubtitle");
const editPriceForm             = document.getElementById("editPriceForm");
const editPriceInput            = document.getElementById("editPriceInput");
const editPriceApprovalDropdown = document.getElementById("editPriceApprovalDropdown");
const closeEditPriceModalBtn    = document.getElementById("closeEditPriceModal");

// Topbar Action Trigger Interceptors
const addItemBtn = document.getElementById("addItemBtn");
const stockInBtn = document.getElementById("stockInBtn");
const stockOutBtn = document.getElementById("stockOutBtn");
const lowStockBtn = document.getElementById("lowStockBtn");
 
 
// ============================================================================
// DATE & UTD HELPER UTILITIES
// ============================================================================
// Parses 8-digit integer UTD (e.g. 20260614) into { year, month, day }
function parseUtdInt(utdInt) {
    const s = String(utdInt ?? 0).padStart(8, '0');
    return {
        year:  parseInt(s.substring(0, 4)),
        month: parseInt(s.substring(4, 6)),
        day:   parseInt(s.substring(6, 8))
    };
}
 
// Converts 8-digit integer UTD → Date object
function utdToDate(utdInt) {
    const { year, month, day } = parseUtdInt(utdInt);
    return new Date(year, month - 1, day);
}
 
// Converts 8-digit integer UTD → "MM/DD/YYYY"
function formatUtdDisplay(utdInt) {
    if (!utdInt || utdInt === 0) return "N/A";
    return window.KFFormat.formatDateMMDDYYYY(utdInt);
}

// Converts "DateAdded" string (e.g. "2026-06-14 10:30:00") → "MM/DD/YYYY"
function formatDateAddedDisplay(dateStr) {
    if (!dateStr || dateStr === "N/A") return "N/A";
    return window.KFFormat.formatDateMMDDYYYY(dateStr);
}
 
// Converts 8-digit UTD integer → "YYYY-MM-DD" for <input type="date"> value
function utdToInputDate(utdInt) {
    const { year, month, day } = parseUtdInt(utdInt);
    if (!year) return "";
    return `${year}-${String(month).padStart(2,'0')}-${String(day).padStart(2,'0')}`;
}
 
// Converts "YYYY-MM-DD" string → 8-digit integer (e.g. 20260614)
function inputDateToUtdInt(dateStr) {
    if (!dateStr) return 0;
    return parseInt(dateStr.replace(/-/g, ''));
}

// Shared expiry-status label/color lookup (used for both per-batch and
// item-level "majority status" rendering)
const UTD_STATUS_COLORS = {
    "Expired":        "background: #721c24; color: #ffffff;",
    "Critical":       "background: #ffe0e3; color: #ff4757;",
    "Expiring Soon":  "background: #fff3cd; color: #856404;",
    "Fresh Stock":    "background: #d4edda; color: #28a745;"
};

function batchStatusLabel(diffDays) {
    if (diffDays < 0) return "Expired";
    if (diffDays <= 7) return "Critical";
    if (diffDays <= 14) return "Expiring Soon";
    return "Fresh Stock";
}
 
// ============================================================================
// TRANSACTION AUDIT UTILITIES
// ============================================================================
function formatUserIdentity(user) {
    if (!user) return "System Auto";
    const lastName = user.lastName || user.LastName || "";
    const firstName = user.firstName || user.FirstName || "";
    const mi = user.mi || user.MI || "";
    const position = user.position || user.Position || "";
    
    const miString = mi.trim() !== "" ? ` ${mi.trim()}.` : "";
    return `${lastName}, ${firstName}${miString} (${position})`;
}
 
async function populateApproverDropdown(dropdownId) {
    const dropdown = document.getElementById(dropdownId);
    if (!dropdown) return;
 
    try {
        const response = await window.kfFetch(`${API_BASE_URL}/staff`); 
        if (!response.ok) throw new Error("Failed to pull personnel logs.");
        
        const staffRegistry = await response.json();
        dropdown.innerHTML = '<option value="" disabled selected>-- Select an Approver --</option>';
        
        const authorizedApprovers = staffRegistry.filter(worker => {
            const pos = (worker.position || worker.Position || "").toLowerCase();
            const isActive = worker.active ?? true;
            return isActive && (pos === "manager" || pos === "owner");
        });
        
        authorizedApprovers.forEach(approver => {
            const option = document.createElement("option");
            const scId = approver.SC_ID ?? approver.sc_ID ?? approver.scId ?? approver.sC_ID;
            option.value = scId;                          // SC_ID integer as value
            option.textContent = formatUserIdentity(approver); // display name
            dropdown.appendChild(option);
        });
    } catch (error) {
        console.error("Approver Dropdown Error:", error);
    }
}
 
function getActiveSessionUser() {
    const activeSession = localStorage.getItem("currentUser");
    if (!activeSession) {
        alert("Session expired. Please log back in.");
        window.KFApi.redirectToLogin();
        return null;
    }
    return JSON.parse(activeSession);
}
 
// ============================================================================
// DATA PIPELINES (READ & GROUP)
// ============================================================================
function groupInventoryData() {
    const showAll = showUnavailableCheckbox ? showUnavailableCheckbox.checked : false;
    const groups = {};
    const today = new Date();
    today.setHours(0, 0, 0, 0);
 
    rawInventory.forEach(row => {
        const expiryDate = utdToDate(row.UTD ?? row.utd ?? 0);
        expiryDate.setHours(0, 0, 0, 0);
        
        const diffDays = Math.ceil((expiryDate - today) / (1000 * 60 * 60 * 24));
 
        // 🎯 FIX: Expired items are still forced to false, but 0 quantities are NOT forced to false anymore.
        // The server-side 'Available' database state is now trusted for 0 quantities.
        if (diffDays < 0) {
            row.available = false;
        }
 
        // Skip individual rows completely if they are unavailable and we shouldn't show them
        if (!showAll && !row.available) return;
 
        if (!groups[row.itemName]) {
            groups[row.itemName] = {
                id: row.itemID,
                name: row.itemName,
                price: row.price,
                category: row.category,
                expanded: false,
                anyAvailable: false, 
                batches: []
            };
        }
        
        if (row.available) groups[row.itemName].anyAvailable = true;
        groups[row.itemName].batches.push(row);
    });
 
    let groupedArray = Object.values(groups);
 
    // Clean out parent groups that have no active batches left
    if (!showAll) {
        groupedArray = groupedArray.filter(group => group.anyAvailable);
    }
 
    // Sort order: Active registries bubble to top, alphabetically ordered
    groupedArray.sort((a, b) => {
        if (a.anyAvailable === b.anyAvailable) return a.name.localeCompare(b.name); 
        return a.anyAvailable ? -1 : 1; 
    });
 
    inventoryGroups = groupedArray;
}
 
// ============================================================================
// RENDER ENGINE
// ============================================================================
function renderInventory(filteredGroups = null) {
    if (!inventoryTableBody) return;
    
    if (filteredGroups === null) {
        filteredGroups = (currentFilterMode === "LOW_STOCK") ? getLowStockGroups() : inventoryGroups;
    }
    
    inventoryTableBody.innerHTML = "";
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    
    filteredGroups.forEach(group => {
        const totalQty = group.batches.reduce((sum, b) => sum + (b.available ? b.quantity : 0), 0);
        const isGroupUnavailable = !group.anyAvailable;
 
        let qtyLabel = "Moderate Quantity"; let qtyColor = "background: #fff3cd; color: #856404;";
        if (totalQty === 0) { qtyLabel = "No Stock"; qtyColor = "background: #fdadb2; color: #721c24;"; }
        else if (totalQty <= 5) { qtyLabel = "Low Quantity"; qtyColor = "background: #ffe0e3; color: #ff4757;"; }
        else if (totalQty > 15) { qtyLabel = "High Quantity"; qtyColor = "background: #d4edda; color: #28a745;"; }
 
        const activeBatches = group.batches.filter(b => b.available);
        let utdLabel = "N/A"; let utdColor = "background: #eee; color: #666;";

        if (activeBatches.length > 0) {
            // Status shown at the item level reflects the MAJORITY status among its
            // active batches (not just the single worst/best batch).
            const statusTally = {};
            activeBatches.forEach(b => {
                const exp = utdToDate(b.UTD ?? b.utd ?? 0);
                const diffDays = Math.ceil((exp - today) / (1000 * 60 * 60 * 24));
                const label = batchStatusLabel(diffDays);
                statusTally[label] = (statusTally[label] || 0) + 1;
            });

            // Tie-break order: when counts are equal, the more severe status wins.
            const severityOrder = ["Expired", "Critical", "Expiring Soon", "Fresh Stock"];
            let bestLabel = severityOrder[severityOrder.length - 1];
            let bestCount = -1;
            severityOrder.forEach(label => {
                const count = statusTally[label] || 0;
                if (count > bestCount) { bestCount = count; bestLabel = label; }
            });

            utdLabel = bestLabel;
            utdColor = UTD_STATUS_COLORS[bestLabel];
        } else if (isGroupUnavailable) {
            utdLabel = "Archived"; utdColor = "background: #fdadb2; color: #721c24;";
        }
 
        const row = document.createElement("tr");
        if (isGroupUnavailable) row.style.backgroundColor = "#fff5f5";
 
        row.innerHTML = `
            <td><span style="cursor: pointer; font-size: 14px;" onclick="toggleExpand('${group.name.replace(/'/g, "\\'")}')">${group.expanded ? "▲" : "▼"}</span></td>
            <td><strong>${group.name} ${isGroupUnavailable ? '(Archived)' : ''}</strong></td>
            <td>₱${group.price.toLocaleString("en-PH", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</td>
            <td>${totalQty}</td>
            <td>${group.category}</td>
            <td>
                <div style="display: flex; gap: 6px; align-items: center;">
                    <span style="padding: 4px 8px; border-radius: 4px; font-size: 11px; font-weight: bold; min-width: 85px; text-align: center; ${utdColor}">${utdLabel}</span>
                    <span style="padding: 4px 8px; border-radius: 4px; font-size: 11px; font-weight: bold; min-width: 90px; text-align: center; ${qtyColor}">${qtyLabel}</span>
                </div>
            </td>
            <td>
                ${!isGroupUnavailable ? `
                    <button class="edit-price-btn" onclick="openEditPriceModal(${group.id}, '${group.name.replace(/'/g, "\\'")}', ${group.price})">Edit Price</button>
                    <button style="background: #ff4757; color: white; border: none; padding: 5px 10px; border-radius: 4px; cursor: pointer; font-weight: bold;" onclick="deleteEntireItem('${group.name.replace(/'/g, "\\'")}')">Delete Item</button>
                ` : `<span style="color: #aaa; font-style: italic; font-size: 13px;">No Actions</span>`}
            </td>
        `;
        inventoryTableBody.appendChild(row);
 
        if (group.expanded) {
            group.batches.sort((a, b) => (a.available === b.available) ? 0 : a.available ? -1 : 1);
            const batchRow = document.createElement("tr");
            batchRow.innerHTML = `
                <td colspan="7">
                    <div style="padding: 10px 40px; background: #fafafa; border-radius: 4px;">
                        <table style="width: 100%; border-collapse: collapse;">
                            <thead>
                                <tr style="border-bottom: 2px solid #ddd; text-align: left; font-size: 12px; color: #666;">
                                    <th>Date Added</th><th>Batch Code</th><th>Quantity</th><th>Use-Thru-Date</th><th>Status Indicators</th><th style="text-align: right;">Actions</th>
                                </tr>
                            </thead>
                            <tbody>
                                ${group.batches.map(b => {
                                    const daDate = formatDateAddedDisplay(b.DateAdded ?? b.dateAdded);
                                    const utdDate = formatUtdDisplay(b.UTD ?? b.utd);
                                    const bExp = utdToDate(b.UTD ?? b.utd ?? 0);
                                    const bDiffDays = Math.ceil((bExp - today) / (1000 * 60 * 60 * 24));
 
                                    let bUtdLabel = "Fresh Stock"; let bUtdColor = "background: #d4edda; color: #28a745;";
                                    if (bDiffDays < 0) { bUtdLabel = "Expired"; bUtdColor = "background: #721c24; color: #ffffff;"; }
                                    else if (bDiffDays <= 7) { bUtdLabel = "Critical"; bUtdColor = "background: #ffe0e3; color: #ff4757;"; }
                                    else if (bDiffDays <= 14) { bUtdLabel = "Expiring Soon"; bUtdColor = "background: #fff3cd; color: #856404;"; }
 
                                    let bQtyLabel = "Moderate Quantity"; let bQtyColor = "background: #fff3cd; color: #856404;";
                                    if (b.quantity === 0) { bQtyLabel = "No Stock"; bQtyColor = "background: #fdadb2; color: #721c24;"; }
                                    else if (b.quantity <= 5) { bQtyLabel = "Low Quantity"; bQtyColor = "background: #ffe0e3; color: #ff4757;"; }
                                    else if (b.quantity > 15) { bQtyLabel = "High Quantity"; bQtyColor = "background: #d4edda; color: #28a745;"; }
 
                                    // Check if the individual item batch is active but empty
                                    const isUnavailableButActive = (b.quantity === 0 && b.available === true);
 
                                    return `
                                        <tr style="border-bottom: 1px solid #eee; font-size: 13px; ${!b.available ? 'background-color: #ffd6d6;' : ''}">
                                            <td>${daDate}</td><td>#BTC-${b.batchID}</td><td>${b.quantity}</td><td>${utdDate}</td>
                                            <td>
                                                <span style="padding: 2px 6px; border-radius: 4px; font-size: 10px; font-weight: bold; display: inline-block; min-width: 75px; text-align: center; ${bUtdColor}">${bUtdLabel}</span>
                                                <span style="padding: 2px 6px; border-radius: 4px; font-size: 10px; font-weight: bold; display: inline-block; min-width: 80px; text-align: center; ${bQtyColor}">${bQtyLabel}</span>
                                            </td>
                                            <td style="text-align: right;">
                                                <button style="background: #2ed573; color: white; border: none; padding: 3px 8px; border-radius: 4px; cursor: pointer; font-size: 11px;" onclick="editItemGroup(${b.batchID})">Edit</button>
                                                
                                                ${isUnavailableButActive ? `
                                                    <button style="background: #ff4757; color: white; border: none; padding: 3px 8px; border-radius: 4px; cursor: pointer; font-size: 11px;" onclick="executeLogicalDelete(${b.batchID})">Delete Item</button>
                                                ` : `
                                                    <button style="background: #ff4757; color: white; border: none; padding: 3px 8px; border-radius: 4px; cursor: pointer; font-size: 11px; ${!b.available ? 'opacity: 0.3;' : ''}" onclick="deleteBatchRow(${b.batchID})" ${!b.available ? 'disabled' : ''}>Delete</button>
                                                `}
                                            </td>
                                        </tr>
                                    `;
                                }).join("")}
                            </tbody>
                        </table>
                    </div>
                </td>
            `;
            inventoryTableBody.appendChild(batchRow);
        }
    });
 
    updateMetrics();
}
 
function updateMetrics() {
    if (totalItems) totalItems.textContent = inventoryGroups.filter(g => g.anyAvailable).length;
    const valueSum = rawInventory.reduce((sum, row) => row.available ? sum + (row.quantity * row.price) : sum, 0);
    if (inventoryValue) inventoryValue.textContent = `₱${valueSum.toLocaleString("en-PH", { minimumFractionDigits: 2 })}`;
}
 
function populateStockDropdown() {
    if (!stockItemSelect) return;
    stockItemSelect.innerHTML = '<option value="" disabled selected>Select Item</option>';
    const itemTracker = new Set();
    rawInventory.forEach(row => {
        if (row.available && !itemTracker.has(row.itemName)) {
            itemTracker.add(row.itemName);
            const opt = document.createElement("option");
            opt.value = row.itemID; opt.dataset.name = row.itemName; opt.dataset.category = row.category; opt.dataset.price = row.price;
            opt.textContent = row.itemName;
            stockItemSelect.appendChild(opt);
        }
    });
}
 
// ============================================================================
// WINDOW ACTIONS & MUTATIONS
// ============================================================================
window.toggleExpand = function(groupName) {
    const group = inventoryGroups.find(g => g.name === groupName);
    if (group) { group.expanded = !group.expanded; renderInventory(); }
};
 
window.editItemGroup = function(batchId) {
    const targetBatch = rawInventory.find(b => b.batchID === batchId);
    if (!targetBatch) return;
 
    modalTitle.textContent = "Edit Inventory Item";
    document.getElementById("editBatchId").value = batchId;
    document.getElementById("editItemId").value = targetBatch.itemID;
    document.getElementById("itemName").value = targetBatch.itemName;
    document.getElementById("itemQuantity").value = targetBatch.quantity;
    document.getElementById("itemCategory").value = targetBatch.category;
    document.getElementById("itemPrice").value = targetBatch.price;
    document.getElementById("itemUTD").value = utdToInputDate(targetBatch.UTD ?? targetBatch.utd ?? 0);
 
    populateApproverDropdown("itemManagerApprovalDropdown");
    itemForm.dataset.mode = "EDIT";
    itemModal.classList.remove("hidden");
};
 
// ============================================================================
// DELETE APPROVAL MODAL
// ============================================================================
let _pendingDeleteBatchIds = [];

function openDeleteModal(batchIds, label) {
    _pendingDeleteBatchIds = Array.isArray(batchIds) ? batchIds : [batchIds];
    if (deleteModalTitle)    deleteModalTitle.textContent    = "Confirm Deletion";
    if (deleteModalSubtitle) deleteModalSubtitle.textContent = `You are about to delete: ${label}. This cannot be undone.`;
    populateApproverDropdown("deleteApprovalDropdown");
    if (deleteModal) deleteModal.classList.remove("hidden");
}

if (closeDeleteModalBtn) {
    closeDeleteModalBtn.addEventListener("click", () => deleteModal.classList.add("hidden"));
}

if (deleteConfirmBtn) {
    deleteConfirmBtn.addEventListener("click", async () => {
        const approverScId = deleteApprovalDropdown ? parseInt(deleteApprovalDropdown.value) : 0;
        if (!approverScId) { alert("Please select an approver before confirming."); return; }

        const currentUser = getActiveSessionUser();
        if (!currentUser) return;

        const performedByScId = currentUser.userId ?? currentUser.SC_ID ?? currentUser.sc_ID;
        deleteModal.classList.add("hidden");

        try {
            for (const batchId of _pendingDeleteBatchIds) {
                const res = await window.kfFetch(`${API_BASE_URL}/inventory/delete/${batchId}`, {
                    method: "DELETE",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ performedByScId, approvedByScId: approverScId })
                });
                if (!res.ok) throw new Error(`Failed to delete Batch #${batchId}.`);
            }
            await syncInventoryFromServer();
        } catch (error) {
            console.error("Delete error:", error);
            alert(error.message);
        }
    });
}

// These are called from inline onclick — open the modal instead of confirm()
window.deleteBatchRow = function(batchId) {
    openDeleteModal([batchId], `Batch #${batchId}`);
};

window.deleteEntireItem = function(itemName) {
    const group = inventoryGroups.find(g => g.name === itemName);
    if (!group) return;
    openDeleteModal(group.batches.map(b => b.batchID), `all batches of "${itemName}"`);
};

// ============================================================================
// EDIT PRICE MODAL
// ============================================================================
let _pendingEditPriceItemId = null;

window.openEditPriceModal = function(itemId, itemName, currentPrice) {
    _pendingEditPriceItemId = itemId;
    if (editPriceSubtitle) editPriceSubtitle.textContent = `Set a new price for "${itemName}".`;
    if (editPriceInput) editPriceInput.value = currentPrice;
    populateApproverDropdown("editPriceApprovalDropdown");
    if (editPriceModal) editPriceModal.classList.remove("hidden");
};

if (closeEditPriceModalBtn) {
    closeEditPriceModalBtn.addEventListener("click", () => editPriceModal.classList.add("hidden"));
}

if (editPriceForm) {
    editPriceForm.addEventListener("submit", async (e) => {
        e.preventDefault();
        if (!_pendingEditPriceItemId) return;

        const currentUser = getActiveSessionUser();
        if (!currentUser) return;

        const approverScId = editPriceApprovalDropdown ? parseInt(editPriceApprovalDropdown.value) : 0;
        if (!approverScId) { alert("Please select an approver before confirming."); return; }

        const newPrice = parseFloat(editPriceInput.value);
        if (isNaN(newPrice) || newPrice < 0) { alert("Please enter a valid price."); return; }

        const payload = {
            price: newPrice,
            performedByScId: currentUser.userId ?? currentUser.SC_ID ?? currentUser.sc_ID,
            approvedByScId: approverScId
        };

        try {
            const res = await window.kfFetch(`${API_BASE_URL}/inventory/update-price/${_pendingEditPriceItemId}`, {
                method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload)
            });
            if (!res.ok) throw new Error("Failed to update price.");
            editPriceModal.classList.add("hidden");
            await syncInventoryFromServer();
        } catch (err) {
            alert(err.message);
        }
    });
}
 
// ============================================================================
// FORM RECEPTORS
// ============================================================================
itemForm.addEventListener("submit", async (e) => {
    e.preventDefault();
    const currentUser = getActiveSessionUser();
    if (!currentUser) return;
 
    const mode = itemForm.dataset.mode;
    const managerApproval = document.getElementById("itemManagerApprovalDropdown").value;
 
    // 🔄 Map directly to the elements that actually exist in stock.html
    const payload = {
        itemName: document.getElementById("itemName").value,
        category: document.getElementById("itemCategory").value,
        price: parseFloat(document.getElementById("itemPrice").value),
        quantity: parseInt(document.getElementById("itemQuantity").value),
        
        // UTD as 8-digit integer (e.g. 20260614) matching the DB schema
        utD: inputDateToUtdInt(document.getElementById("itemUTD").value),
        
        // Pass audit signatures down to backend queries
        performedByScId: currentUser.userId ?? currentUser.SC_ID ?? currentUser.sc_ID,
        approvedByScId:  parseInt(managerApproval)
    };
 
    if (mode === "EDIT") {
        payload.batchID = parseInt(document.getElementById("editBatchId").value);
        payload.itemID = parseInt(document.getElementById("editItemId").value);
        try {
            const res = await window.kfFetch(`${API_BASE_URL}/inventory/update-full-batch`, {
                method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload)
            });
            if (!res.ok) throw new Error("Failed update.");
            closeItemModalWindow(); await syncInventoryFromServer();
        } catch (e) { alert(e.message); }
    } else {
        // Backend handles creating incremental ItemIDs automatically if itemID <= 0,
        // so we can set it to 0 here for fresh entries safely.
        payload.itemID = 0; 
        try {
            const res = await window.kfFetch(`${API_BASE_URL}/inventory/add`, {
                method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload)
            });
            if (!res.ok) throw new Error("Failed creation.");
            closeItemModalWindow(); await syncInventoryFromServer();
        } catch (e) { alert(e.message); }
    }
});
 
stockForm.addEventListener("submit", async (e) => {
    e.preventDefault();
    const currentUser = getActiveSessionUser();
    if (!currentUser) return;
 
    const selectedOption = stockItemSelect.options[stockItemSelect.selectedIndex];
    const qty = parseInt(document.getElementById("stockQuantity").value);
    const approverValue = document.getElementById("stockManagerApprovalDropdown").value;
 
    if (currentTransactionType === "IN") {
        const payload = {
            itemID: parseInt(selectedOption.value), itemName: selectedOption.dataset.name,
            category: selectedOption.dataset.category, price: parseFloat(selectedOption.dataset.price),
            quantity: qty, status: "Fresh Stock", performedByScId: currentUser.userId ?? currentUser.SC_ID ?? currentUser.sc_ID, approvedByScId: parseInt(approverValue),
            utD: inputDateToUtdInt(stockUTD.value)
        };
        try {
            const res = await window.kfFetch(`${API_BASE_URL}/inventory/add`, {
                method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload)
            });
            if (!res.ok) throw new Error("Stock-In execution error.");
            closeStockModalWindow(); await syncInventoryFromServer();
        } catch (err) { alert(err.message); }
    } else {
        const selectedBatch = stockBatchSelect.options[stockBatchSelect.selectedIndex];
        if (qty > parseInt(selectedBatch.dataset.maxQty)) { alert("Overdeduction bounds tripped."); return; }
 
        const payload = { batchID: parseInt(selectedBatch.value), quantity: qty, performedByScId: currentUser.userId ?? currentUser.SC_ID ?? currentUser.sc_ID, approvedByScId: parseInt(approverValue) };
        try {
            const res = await window.kfFetch(`${API_BASE_URL}/inventory/stock-out-specific`, {
                method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload)
            });
            if (!res.ok) throw new Error("Stock-Out error.");
            closeStockModalWindow(); await syncInventoryFromServer();
        } catch (err) { alert(err.message); }
    }
});
 
stockItemSelect.addEventListener("change", () => {
    const selectedOption = stockItemSelect.options[stockItemSelect.selectedIndex];
    if (!stockBatchSelect) return;
    stockBatchSelect.innerHTML = '<option value="" disabled selected>Select an active batch...</option>';
 
    if (currentTransactionType === "OUT") {
        rawInventory.filter(row => row.itemName === selectedOption.dataset.name && row.available && row.quantity > 0)
            .forEach(batch => {
                const opt = document.createElement("option"); opt.value = batch.batchID; opt.dataset.maxQty = batch.quantity;
                opt.textContent = `Batch #${batch.batchID} (Qty: ${batch.quantity} - Exp: ${formatUtdDisplay(batch.UTD ?? batch.utd)})`;
                stockBatchSelect.appendChild(opt);
            });
    }
});
 
// ============================================================================
// UI WINDOW TOGGLES
// ============================================================================
addItemBtn.addEventListener("click", () => {
    modalTitle.textContent = "Add Item"; itemForm.reset();
    populateApproverDropdown("itemManagerApprovalDropdown");
    itemForm.dataset.mode = "ADD"; itemModal.classList.remove("hidden");
});
 
stockInBtn.addEventListener("click", () => {
    currentTransactionType = "IN"; stockModalTitle.textContent = "Stock-In Transaction"; stockForm.reset();
    document.getElementById("batchSelectContainer").style.display = "none"; stockBatchSelect.removeAttribute("required");
    stockUTD.style.display = "block"; stockUTD.setAttribute("required", "true"); stockUTDLabel.style.display = "block";
    populateApproverDropdown("stockManagerApprovalDropdown"); stockModal.classList.remove("hidden");
});
 
stockOutBtn.addEventListener("click", () => {
    currentTransactionType = "OUT"; stockModalTitle.textContent = "Stock-Out Transaction"; stockForm.reset();
    document.getElementById("batchSelectContainer").style.display = "block"; stockBatchSelect.setAttribute("required", "true");
    stockUTD.style.display = "none"; stockUTD.removeAttribute("required"); stockUTDLabel.style.display = "none";
    populateApproverDropdown("stockManagerApprovalDropdown"); stockModal.classList.remove("hidden");
});
 
function closeItemModalWindow() { itemModal.classList.add("hidden"); }
function closeStockModalWindow() { stockModal.classList.add("hidden"); }
closeModal.addEventListener("click", closeItemModalWindow);
closeStockModal.addEventListener("click", closeStockModalWindow);
 
// ============================================================================
// FILTERS & LIFECYCLE MANAGEMENT
// ============================================================================
function getLowStockGroups() {
    return inventoryGroups.map(g => ({ ...g, batches: g.batches.filter(b => b.quantity <= 5) })).filter(g => g.batches.length > 0);
}
 
lowStockBtn.addEventListener("click", () => {
    if (currentFilterMode === "DEFAULT") {
        currentFilterMode = "LOW_STOCK"; inventoryGroups.forEach(g => { if (g.batches.some(b => b.quantity <= 5)) g.expanded = true; });
        renderInventory(); lowStockBtn.textContent = "Back to Default"; lowStockBtn.style.background = "#ffa502";
    } else {
        currentFilterMode = "DEFAULT"; groupInventoryData(); renderInventory();
        lowStockBtn.textContent = "Low Stock Items"; lowStockBtn.style.background = "";
    }
});
 
if (showUnavailableCheckbox) {
    showUnavailableCheckbox.addEventListener("change", () => {
        groupInventoryData(); 
        renderInventory(); 
        populateStockDropdown();
    });
}
 
searchInput.addEventListener("keyup", () => {
    const term = searchInput.value.toLowerCase();
    renderInventory(inventoryGroups.filter(g => g.name.toLowerCase().includes(term) || g.category.toLowerCase().includes(term)));
});
 
async function syncInventoryFromServer() {
    try {
        const response = await window.kfFetch(`${API_BASE_URL}/inventory?includeUnavailable=true`);
        
        if (!response.ok) throw new Error("HTTP degradation error!");
        
        // API returns Pascal case (BatchID, ItemName, etc.) — normalize to camelCase
        // so all existing code below continues to work without changes.
        const raw = await response.json();
        rawInventory = raw.map(r => ({
            batchID:     r.BatchID    ?? r.batchID,
            itemID:      r.ItemID     ?? r.itemID,
            itemName:    r.ItemName   ?? r.itemName   ?? "Unknown",
            category:    r.Category   ?? r.category   ?? "General",
            price:       r.Price      ?? r.price       ?? 0,
            quantity:    r.Quantity   ?? r.quantity    ?? 0,
            available:   r.Available  ?? r.available   ?? false,
            UTD:         r.UTD        ?? r.utd         ?? 0,
            action:      r.Action     ?? r.action      ?? "Add Item",
            DateAdded:   r.DateAdded  ?? r.dateAdded   ?? "",
            performedBy: r.PerformedBy ?? r.performedBy ?? "System Auto",
            approvedBy:  r.ApprovedBy  ?? r.approvedBy  ?? "N/A"
        }));
        
        groupInventoryData(); 
        renderInventory(); 
        populateStockDropdown();
        
        console.log("Inventory synced successfully.");
    } catch (error) {
        console.error("Critical core synchronization failure:", error);
    }
}
 
async function executeLogicalDelete(batchId) {
    if (!confirm("Are you sure you want to permanently delete and archive this item row?")) {
        return;
    }
 
    try {
        const response = await window.kfFetch(`${API_BASE_URL}/inventory/delete/${batchId}`, {
            method: 'DELETE',
            headers: {
                'Content-Type': 'application/json'
            }
        });
 
        if (!response.ok) {
            throw new Error("Failed to process deletion routine on server.");
        }
 
        const data = await response.json();
        alert(data.message);
 
        await syncInventoryFromServer(); 
        
    } catch (error) {
        console.error("Critical delete processing error:", error);
        alert("Could not process deletion. Check console for details.");
    }
}
 
// Logout is handled centrally by nav.js (with a confirmation modal).

// LIFECYCLE BOOTSTRAP RESILIENCE LOOP
if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", syncInventoryFromServer);
} else {
    syncInventoryFromServer();
}