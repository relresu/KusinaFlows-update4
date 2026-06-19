// Navigation system for all pages inside KusinaFlow
function go(id, folderAndPage) {
  const el = document.getElementById(id);
  if (!el) return;

  el.addEventListener("click", () => {
    // 1. Get the base URL path up to the current folder location
    const currentLoc = window.location.href;
    
    // 2. Safely find the 'frontend/' position in the URL
    const frontendIndex = currentLoc.indexOf('/frontend/');
    
    if (frontendIndex !== -1) {
      // Create a clean path starting from the frontend folder base root
      const baseUrl = currentLoc.substring(0, frontendIndex + 10); // Includes '/frontend/'
      window.location.href = baseUrl + folderAndPage;
    } else {
      // Fallback: If running without a server (file:/// protocol directly)
      window.location.href = "../" + folderAndPage;
    }
  });
}

// [Keep your existing go() function definitions up here...]

// ============================================================================
// EXACT ROUTE MAPPING ARCHITECTURE
// ============================================================================
go("dashboardBtn", "dashboard/dashboard.html");
go("stocksBtn", "stocks/stocks.html");
go("reportBtn", "gen-reports/gen-reports.html");
go("stockHistoryBtn", "stock-history/stock-history.html");
go("staffManagementBtn", "staff-management/staff-management.html"); // <--- ADDED: Maps sidebar clicks to your staff folder
// Log Out is intentionally NOT wired through go() — it needs a confirmation
// step first. See the logout confirmation modal logic further below.


// Dynamic active state styling & User Profile card injection
document.addEventListener("DOMContentLoaded", () => {
    // 1. Retrieve and Parse Active Session Data
    const sessionData = localStorage.getItem("currentUser");
    const sessionToken = localStorage.getItem("authToken");
    if (!sessionData || !sessionToken) {
        // No (or stale) session — the auth middleware would reject every API
        // call anyway, so send the user back to login before anything fires.
        localStorage.clear();
        window.KFApi.redirectToLogin();
        return;
    }
    const user = JSON.parse(sessionData);

    // 2. Inject Dynamic User Profile Plate into the Sidebar Bottom Arena
    const sidebarNav = document.querySelector(".sidebar nav");
    if (sidebarNav) {
        // Create user display plate element
        const userProfilePlate = document.createElement("div");
        userProfilePlate.style.cssText = `
            margin-top: auto;
            padding: 15px;
            border-top: 1px solid rgba(255,255,255,0.15);
            display: flex;
            flex-direction: column;
            gap: 5px;
            color: #fff;
            font-family: sans-serif;
        `;
        
        userProfilePlate.innerHTML = `
            <div style="font-size: 11px; text-transform: uppercase; letter-spacing: 1px; color: #bda38a;">Logged In As</div>
            <div style="font-weight: bold; font-size: 14px;">${user.firstName} ${user.lastName}</div>
            <div style="font-size: 12px; font-style: italic; color: #ccc;">${user.position}</div>
        `;
        sidebarNav.appendChild(userProfilePlate);
    }

    // 3. Logout confirmation modal — asks before actually logging the user out
    const logoutBtn = document.getElementById("logoutBtn");
    if (logoutBtn) {
        const logoutModal = buildLogoutConfirmModal();
        document.body.appendChild(logoutModal.element);

        logoutBtn.addEventListener("click", () => {
            logoutModal.element.classList.remove("hidden");
        });

        logoutModal.yesBtn.addEventListener("click", async () => {
            // Best-effort token invalidation server-side; logout proceeds either way
            try {
                await window.kfFetch("http://localhost:5244/api/auth/logout", { method: "POST" });
            } catch (err) {
                console.warn("Logout endpoint unreachable, clearing session locally anyway:", err);
            }
            localStorage.clear(); // Flushes all session tracking data keys simultaneously
            window.KFApi.redirectToLogin();
        });

        logoutModal.noBtn.addEventListener("click", () => {
            logoutModal.element.classList.add("hidden");
        });
    }

    // 4. Enforce Structural Privilege Limitations Rules
    const staffManagementMenuItem = document.getElementById("staffManagementBtn");
    
    if (user.position === "Staff") {
        // Rule: Staff cannot view Staff Management at all
        if (staffManagementMenuItem) {
            staffManagementMenuItem.style.display = "none";
        }
        
        // If a Staff member tries to manually type the URL path into the address bar, boot them out
        if (window.location.href.includes("staff-management.html")) {
            alert("Access Denied: You do not possess structural clearance to view this directory node.");
            window.location.href = "../dashboard/dashboard.html";
        }
    }
});

// ============================================================================
// LOGOUT CONFIRMATION MODAL
// Built and injected dynamically so it works on every page without needing
// the markup to be hand-added to each individual HTML file.
// ============================================================================
function buildLogoutConfirmModal() {
    const overlay = document.createElement("div");
    overlay.className = "modal hidden";
    overlay.id = "logoutConfirmModal";

    overlay.innerHTML = `
        <div class="modal-content" style="text-align:center; width: 360px;">
            <h2 style="margin-bottom: 20px;">Are you Logging Out?</h2>
            <div class="modal-buttons" style="justify-content: center;">
                <button type="button" id="logoutConfirmYes" class="btn-save">Yes</button>
                <button type="button" id="logoutConfirmNo" class="btn-cancel">No</button>
            </div>
        </div>
    `;

    return {
        element: overlay,
        yesBtn: overlay.querySelector("#logoutConfirmYes"),
        noBtn: overlay.querySelector("#logoutConfirmNo")
    };
}