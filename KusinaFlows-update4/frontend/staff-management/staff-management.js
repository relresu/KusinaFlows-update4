// ============================================================================
// CONFIGURATION & GLOBAL STATE MANAGER
// ============================================================================
const API_BASE_URL = "http://localhost:5244/api"; 

let staffRegistry = [];
let selectedStaffIds = new Set();

// DOM Elements - Core Layout Structures
const staffTableBody = document.getElementById("staffTableBody");
const selectedCount = document.getElementById("selectedCount");
const bulkDeactivateBtn = document.getElementById("bulkDeactivateBtn");
const staffSearchInput = document.getElementById("staffSearchInput");
const roleFilter = document.getElementById("roleFilter");
const statusFilter = document.getElementById("statusFilter");
const exportCsvBtn = document.getElementById("exportCsvBtn");
const printListBtn = document.getElementById("printListBtn");

// Modals & Forms Mappings
const staffModal = document.getElementById("staffModal");
const staffForm = document.getElementById("staffForm");
const staffModalTitle = document.getElementById("staffModalTitle");
const closeStaffModal = document.getElementById("closeStaffModal");

// Form Inputs - Interactive Elements
const staffProfilePicInput = document.getElementById("staffProfilePicInput");
const staffAvatarPreview = document.getElementById("staffAvatarPreview");
const staffAvatarEmoji = document.getElementById("staffAvatarEmoji");
const staffFirstName = document.getElementById("staffFirstName");
const staffMI = document.getElementById("staffMI");
const staffLastName = document.getElementById("staffLastName");
const staffUsername = document.getElementById("staffUsername");
const staffPassword = document.getElementById("staffPassword");
const staffRole = document.getElementById("staffRole");
const staffContactInfo = document.getElementById("staffContactInfo");
const staffStatusToggle = document.getElementById("staffStatusToggle");
const staffPasswordWarning = document.getElementById("staffPasswordWarning");
const staffContactWarning = document.getElementById("staffContactWarning");

// Topbar Trigger Action
const addStaffBtn = document.getElementById("addStaffBtn");

// Holds the raw base64 string of the uploaded image file
let structuralBase64Image = "";

// Toggles between the emoji placeholder and an actual uploaded/stored avatar image
function setAvatarPreview(base64Image) {
    if (base64Image) {
        if (staffAvatarPreview) {
            staffAvatarPreview.src = base64Image;
            staffAvatarPreview.style.display = "block";
        }
        if (staffAvatarEmoji) staffAvatarEmoji.style.display = "none";
    } else {
        if (staffAvatarPreview) {
            staffAvatarPreview.src = "";
            staffAvatarPreview.style.display = "none";
        }
        if (staffAvatarEmoji) staffAvatarEmoji.style.display = "block";
    }
}

// ============================================================================
// INITIALIZATION & RESOURCE SYNCHRONIZATION (READ)
// ============================================================================
async function initializeStaffDashboard() {
    try {
        const response = await window.kfFetch(`${API_BASE_URL}/staff`);
        if (!response.ok) throw new Error(`Server status: ${response.status}`);
        
        staffRegistry = await response.json();
        console.log("Raw Staff Data from Server:", staffRegistry[0]);
        renderStaffTable();
    } catch (error) {
        console.error("Staff management execution failure:", error);
        if (staffTableBody) {
            staffTableBody.innerHTML = `
                <tr>
                    <td colspan="10" style="color: #ff4757; text-align: center; font-weight: bold; padding: 20px;">
                        ⚠️ System Offline: Could not communicate with Staff Routing Server.<br>
                        <span style="font-size: 12px; font-weight: normal; color: #aaa;">Error: ${error.message}</span>
                    </td>
                </tr>`;
        }
    }
}

// ============================================================================
// DYNAMIC TABLES COMPONENT RENDERING ENGINE
// ============================================================================
function renderStaffTable(filteredData = null) {
    if (!staffTableBody) return;
    
    const records = filteredData !== null ? filteredData : staffRegistry;
    staffTableBody.innerHTML = "";
    
    records.forEach(staff => {
        // 🎯 EXACT CASING MATCH FOR YOUR BACKEND PAYLOAD
        const staffId = staff.sC_ID; 

        const row = document.createElement("tr");
        
        const profileImageSrc = staff.profilePicture && staff.profilePicture.trim() !== "" 
            ? staff.profilePicture 
            : "data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' width='40' height='40' viewBox='0 0 24 24' fill='%23ccc'><circle cx='12' cy='8' r='4'/><path d='M12 14c-6.1 0-10 4-10 4v2h20v-2s-3.9-4-10-4z'/></svg>";

        const statusStyle = staff.active 
            ? "background: #d4edda; color: #28a745;" 
            : "background: #fdadb2; color: #721c24;";

        const isChecked = selectedStaffIds.has(staffId) ? "checked" : "";

        row.innerHTML = `
            <td style="text-align: center;">
                <input type="checkbox" class="row-select-checkbox" data-id="${staffId}" ${isChecked} onchange="handleRowSelect(this)">
            </td>
            <td><strong>#${staffId}</strong></td> 
            <td>
                <img src="${profileImageSrc}" alt="Avatar" style="width: 35px; height: 35px; border-radius: 50%; object-fit: cover; border: 1px solid #ddd; background: #fafafa;">
            </td>
            <td>${staff.lastName}, ${staff.firstName} ${staff.mi || ""}</td>
            <td>${staff.username}</td>
            <td><span style="font-weight: 500;">${staff.position}</span></td>
            <td>
                <span style="padding: 3px 8px; border-radius: 4px; font-size: 11px; font-weight: bold; ${statusStyle}">${staff.active ? 'Active' : 'Inactive'}</span>
            </td>
            <td>${staff.dateHired ? window.KFFormat.formatDateMMDDYYYY(staff.dateHired) : "N/A"}</td>
            <td>${(staff.lastLogin && staff.lastLogin !== "-") ? window.KFFormat.formatDateTimeDisplay(staff.lastLogin) : '<span style="color:#aaa; font-style:italic;">Unknown</span>'}</td>
            <td>
                <button style="background: #2ed573; color: white; border: none; padding: 4px 8px; border-radius: 4px; cursor: pointer; font-size: 11px; font-weight: bold;" 
                        onclick="loadStaffForEditing(${staffId})">
                    Edit
                </button>
            </td>
        `;
        staffTableBody.appendChild(row);
    });
    
    updateBulkActionPanelState();
}

// ============================================================================
// BASE64 PLAIN TEXT IMAGE STREAM PROCESSING
// ============================================================================
if (staffProfilePicInput) {
    staffProfilePicInput.addEventListener("change", function (e) {
        const file = e.target.files[0];
        if (!file) return;

        const reader = new FileReader();
        reader.onload = function (event) {
            structuralBase64Image = event.target.result;
            setAvatarPreview(structuralBase64Image);
        };
        reader.readAsDataURL(file);
    });
}

// ============================================================================
// LIVE FIELD VALIDATION (numbers-only contact info, password length warning)
// ============================================================================
if (staffContactInfo) {
    staffContactInfo.addEventListener("input", () => {
        staffContactInfo.value = staffContactInfo.value.replace(/\D/g, "").slice(0, 11);
        if (staffContactWarning) staffContactWarning.style.display = "none";
    });
}

if (staffPassword) {
    staffPassword.addEventListener("input", () => {
        if (staffPasswordWarning) staffPasswordWarning.style.display = "none";
    });
}

// ============================================================================
// MUTATION SUBMIT ACTIONS (CREATE / UPDATE)
// ============================================================================
staffForm.addEventListener("submit", async (e) => {
    e.preventDefault();

    const mode = staffForm.dataset.mode;

    // Password must be at least 4 characters when one is provided (always required on ADD)
    const passwordValue = staffPassword ? staffPassword.value.trim() : "";
    const passwordProvided = mode === "ADD" || passwordValue !== "";
    const passwordInvalid = passwordProvided && passwordValue.length < 4;
    if (staffPasswordWarning) staffPasswordWarning.style.display = passwordInvalid ? "block" : "none";
    if (passwordInvalid) { staffPassword.focus(); return; }

    // Contact info must be exactly 11 digits, numbers only (when provided)
    const contactValue = staffContactInfo ? staffContactInfo.value.trim() : "";
    const contactInvalid = contactValue !== "" && !/^\d{11}$/.test(contactValue);
    if (staffContactWarning) staffContactWarning.style.display = contactInvalid ? "block" : "none";
    if (contactInvalid) { staffContactInfo.focus(); return; }

    const today = new Date();

    const localDateHired = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`;

    // Structure matches your C# StaffDto exactly
    const payload = {
        FirstName: staffFirstName.value.trim(),
        MI: staffMI.value.trim() || null,
        LastName: staffLastName.value.trim(),
        Position: staffRole.value,               
        Username: staffUsername.value.trim(),
        ContactInfo: staffContactInfo ? staffContactInfo.value.trim() : null,
        ProfilePicture: structuralBase64Image || null,
        Active: staffStatusToggle.checked        
    };

    if (mode === "EDIT") {
        const staffId = parseInt(staffForm.dataset.editId);
        payload.SC_ID = staffId;
        
        if (staffPassword && staffPassword.value.trim() !== "") {
            payload.Password = staffPassword.value.trim();
        } else {
            payload.Password = null;
        }

        try {
            const response = await window.kfFetch(`${API_BASE_URL}/staff/${staffId}`, {
                method: "PUT",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload)
            });

            if (!response.ok) {
                const errText = await response.text();
                throw new Error(errText || "Backend system rejected structural data modification updates.");
            }
            
            closeStaffModalWindow();
            await initializeStaffDashboard();
        } catch (error) {
            console.error("Staff modification update fault:", error);
            alert("Error updating record: " + error.message);
        }
    } else {
        payload.DateHired = localDateHired;
        payload.Password = staffPassword.value.trim() || "password123"; 
        payload.LastLogin = "-";

        try {
            const response = await window.kfFetch(`${API_BASE_URL}/staff`, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload)
            });

            if (!response.ok) {
                const errText = await response.text();
                throw new Error(errText || "Failed to write fresh entry row to database storage.");
            }
            
            closeStaffModalWindow();
            await initializeStaffDashboard();
        } catch (error) {
            console.error("Staff addition execution fault:", error);
            alert("Error writing entity payload: " + error.message);
        }
    }
});

// ============================================================================
// MODAL WINDOW INTERACTIVE CONTROLS
// ============================================================================
addStaffBtn.addEventListener("click", () => {
    staffModalTitle.textContent = "Add New Staff";
    staffForm.reset();
    
    staffForm.dataset.mode = "ADD";
    structuralBase64Image = "";

    setAvatarPreview("");
    if (staffPassword) staffPassword.setAttribute("required", "true");
    if (staffPasswordWarning) staffPasswordWarning.style.display = "none";
    if (staffContactWarning) staffContactWarning.style.display = "none";
    
    staffModal.classList.remove("hidden");
    staffModal.style.display = "flex";
});

window.loadStaffForEditing = function(staffId) {
    // 🎯 FIX 1: Checked against correct sC_ID backend casing constraint
    const target = staffRegistry.find(s => s.sC_ID === staffId);
    if (!target) return;

    staffModalTitle.textContent = "Edit Staff Member Settings";
    staffForm.reset();

    staffForm.dataset.mode = "EDIT";
    staffForm.dataset.editId = staffId;
    staffForm.dataset.dateHired = target.dateHired;

    // 🎯 FIX 2: Properties populated explicitly from working camelCase keys
    staffFirstName.value = target.firstName || "";
    staffMI.value = target.mi || "";
    staffLastName.value = target.lastName || "";
    staffUsername.value = target.username || "";
    staffRole.value = target.position || "Staff"; // Maps correctly to target.position
    
    if (staffContactInfo) {
        staffContactInfo.value = target.contactInfo || "";
    }
    
    // 🎯 FIX 3: Bound checkbox explicitly to active state boolean property
    staffStatusToggle.checked = !!target.active;

    structuralBase64Image = target.profilePicture || "";
    setAvatarPreview(structuralBase64Image);


    if (staffPassword) {
        staffPassword.removeAttribute("required");
        staffPassword.placeholder = "Leave blank to keep password unchanged";
    }
    if (staffPasswordWarning) staffPasswordWarning.style.display = "none";
    if (staffContactWarning) staffContactWarning.style.display = "none";

    staffModal.classList.remove("hidden");
    staffModal.style.display = "flex";
};

function closeStaffModalWindow() {
    staffModal.classList.add("hidden");
    staffModal.style.display = "none";
}
closeStaffModal.addEventListener("click", closeStaffModalWindow);

// ============================================================================
// LIVE DYNAMIC INTERPOLATION DATA MATCH FILTERS
// ============================================================================
staffSearchInput.addEventListener("keyup", performPipelineSearchFilter);
roleFilter.addEventListener("change", performPipelineSearchFilter);
statusFilter.addEventListener("change", performPipelineSearchFilter);

function performPipelineSearchFilter() {
    const term = staffSearchInput.value.toLowerCase();
    const selectedRole = roleFilter.value;
    const selectedStatus = statusFilter.value;

    const matches = staffRegistry.filter(staff => {
        const matchTerm = (staff.firstName + " " + staff.lastName + " " + staff.username).toLowerCase().includes(term);
        const matchRole = selectedRole === "all" || staff.position === selectedRole; 
        const matchStatus = selectedStatus === "all" || (selectedStatus === "Active" ? staff.active : !staff.active); 
        return matchTerm && matchRole && matchStatus;
    });

    renderStaffTable(matches);
}

// ============================================================================
// SELECTION MANAGEMENT HOOKS
// ============================================================================
window.handleRowSelect = function(checkbox) {
    const id = parseInt(checkbox.dataset.id); 
    if (checkbox.checked) {
        selectedStaffIds.add(id);
    } else {
        selectedStaffIds.delete(id);
    }
    updateBulkActionPanelState();
};

document.getElementById("selectAllCheckbox").addEventListener("change", function(e) {
    const visibleCheckboxes = staffTableBody.querySelectorAll(".row-select-checkbox");
    visibleCheckboxes.forEach(cb => {
        cb.checked = e.target.checked;
        const id = parseInt(cb.dataset.id);
        if (e.target.checked) {
            selectedStaffIds.add(id);
        } else {
            selectedStaffIds.delete(id);
        }
    });
    updateBulkActionPanelState();
});

function updateBulkActionPanelState() {
    if (selectedCount) selectedCount.textContent = `${selectedStaffIds.size} staff selected`;
    if (bulkDeactivateBtn) {
        bulkDeactivateBtn.style.display = selectedStaffIds.size > 0 ? "inline-block" : "none";
    }
}

// ============================================================================
// QUICK ACTIONS: BULK DEACTIVATE / EXPORT CSV / PRINT LIST
// ============================================================================
if (bulkDeactivateBtn) {
    bulkDeactivateBtn.addEventListener("click", async () => {
        if (selectedStaffIds.size === 0) return;
        if (!confirm(`Deactivate ${selectedStaffIds.size} selected staff member(s)? They will no longer be able to log in.`)) return;

        const targets = staffRegistry.filter(s => selectedStaffIds.has(s.sC_ID));

        try {
            for (const staff of targets) {
                // UpdateStaff replaces the full row, so the complete existing
                // record is resent with only Active flipped — a partial payload
                // here would null out the staff member's other fields server-side.
                const payload = {
                    FirstName: staff.firstName,
                    MI: staff.mi,
                    LastName: staff.lastName,
                    Position: staff.position,
                    Username: staff.username,
                    ContactInfo: staff.contactInfo,
                    ProfilePicture: staff.profilePicture,
                    Active: false
                };
                const response = await window.kfFetch(`${API_BASE_URL}/staff/${staff.sC_ID}`, {
                    method: "PUT",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify(payload)
                });
                if (!response.ok) throw new Error(`Failed to deactivate ${staff.firstName} ${staff.lastName}.`);
            }
            selectedStaffIds.clear();
            await initializeStaffDashboard();
        } catch (error) {
            console.error("Bulk deactivate error:", error);
            alert(error.message);
        }
    });
}

if (exportCsvBtn) {
    exportCsvBtn.addEventListener("click", () => {
        const rows = staffRegistry.length ? staffRegistry : [];
        const header = ["Staff ID", "Last Name", "First Name", "M.I.", "Username", "Position", "Status", "Contact Info", "Date Hired", "Last Login"];

        const csvEscape = (value) => {
            const str = (value ?? "").toString();
            return /[",\n]/.test(str) ? `"${str.replace(/"/g, '""')}"` : str;
        };

        const lines = [header.join(",")];
        rows.forEach(s => {
            lines.push([
                s.sC_ID, s.lastName, s.firstName, s.mi || "", s.username,
                s.position, s.active ? "Active" : "Inactive", s.contactInfo || "",
                s.dateHired || "", s.lastLogin || ""
            ].map(csvEscape).join(","));
        });

        const blob = new Blob([lines.join("\n")], { type: "text/csv;charset=utf-8;" });
        const url = URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = url;
        a.download = `kusinaflow-staff-${new Date().toISOString().slice(0, 10)}.csv`;
        document.body.appendChild(a);
        a.click();
        a.remove();
        URL.revokeObjectURL(url);
    });
}

if (printListBtn) {
    printListBtn.addEventListener("click", () => window.print());
}

// ============================================================================
// DOM LIFECYCLE RUNTIME INITIALIZATION
// ============================================================================
document.addEventListener("DOMContentLoaded", () => {
    const user = JSON.parse(localStorage.getItem("currentUser"));
    if (!user) return;

    const roleDropdown = document.getElementById("staffRole");

    if (user.position === "Manager") {
        if (roleDropdown) {
            roleDropdown.innerHTML = `<option value="Staff">Staff</option>`;
        }

        const originalLoadStaffForEditing = window.loadStaffForEditing;
        window.loadStaffForEditing = function(staffId) {
            // 🎯 FIX 4: Aligned interception logic check to match sC_ID casing
            const targetWorker = staffRegistry.find(s => s.sC_ID === staffId);
            
            if (targetWorker && (targetWorker.position === "Manager" || targetWorker.position === "Owner")) {
                alert("Privilege Limitation Error: Managers cannot alter Managerial or Ownership security structures.");
                return;
            }
            originalLoadStaffForEditing(staffId);
        };
    }
    
    initializeStaffDashboard();
});