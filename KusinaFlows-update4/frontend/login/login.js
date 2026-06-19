const form = document.getElementById('form')
const username_input = document.getElementById('username-input') 
const password_input = document.getElementById('password-input')
const error_message = document.getElementById('error-message')

form.addEventListener('submit', async (e) => {
   e.preventDefault();

   let errors = getLoginFormErrors(username_input.value, password_input.value);
   if(errors.length > 0){
      error_message.innerText = errors.join(". ");
      return; 
   } 

   error_message.innerText = "Checking credentials...";

   try {
      const response = await fetch('http://localhost:5244/api/auth/login', { 
         method: 'POST',
         headers: {
            'Content-Type': 'application/json'
         },
         body: JSON.stringify({
            username: username_input.value,
            password: password_input.value
         })
      });

      // --- SUCCESSFUL AUTHENTICATION STREAM ---
      if (response.ok) {
         const data = await response.json();
         
         // 🔍 DIAGNOSTIC LOG: Run this to see the exact structure of your user object properties!
         console.log("Logged In User Payload:", data.user);

         // 🔒 MULTI-CASE INACTIVE ACCOUNT SECURITY CHECK
         // Captures camelCase 'active' or PascalCase 'Active' safely
         const isAccountActive = data.user.active ?? data.user.Active;

         if (isAccountActive === false) {
            error_message.innerText = "Can't Logged in";
            
            // Highlight inputs to indicate access restriction
            username_input.parentElement.classList.add('incorrect');
            password_input.parentElement.classList.add('incorrect');
            return; // 🛑 BLOCK EVERYTHING HERE
         }

         // ... otherwise, continue with logging in safely ...
         error_message.innerText = "";
         localStorage.setItem("isLoggedIn", "true");
         localStorage.setItem("currentUser", JSON.stringify(data.user));
         localStorage.setItem("authToken", data.token);

         // 3. Keep old keys as fallback markers if other components depend on them
         localStorage.setItem("userRole", data.user.position);
         localStorage.setItem("currentStaffUser", username_input.value);

         // 🚀 NEW LOG TRACKING OPERATION: Fire off the database update string pipeline
         // (uses kfFetch so the freshly-issued token rides along — this endpoint
         // is behind the auth middleware like everything else under /api/)
         try {
            await window.kfFetch('http://localhost:5244/api/staff/update-login-time', {
               method: 'POST',
               headers: {
                  'Content-Type': 'application/json'
               },
               body: JSON.stringify({
                  Username: username_input.value
               })
            });
         } catch (logError) {
            console.warn("Failed to capture automated login metrics:", logError);
         }

         // Redirect cleanly to the structural landing space dashboard
         window.location.replace("../dashboard/dashboard.html"); 
      } else {
      // If we get here, the server explicitly rejected the login (like a 401, 403, or 404)
      let serverError = "";
      
      try {
         const responseText = await response.text();
         if (responseText) {
            const parsedData = JSON.parse(responseText);
            serverError = parsedData.message || parsedData.error || responseText;
         }
      } catch (parseError) {
         serverError = "";
      }

      if (!serverError && response.status === 401) {
         serverError = "Wrong Password"; 
      }

      const lowerMessage = serverError.toLowerCase();

      // --- EVALUATE ERROR STATES ---
      // Catch the "Can't Logged in" response string or a 403 status code
      if (lowerMessage.includes("logged in") || response.status === 403) {
         error_message.innerText = "Can't Logged in";
         username_input.parentElement.classList.add('incorrect');
         password_input.parentElement.classList.add('incorrect');
      } 
      else if (lowerMessage.includes("username") || lowerMessage.includes("exist") || lowerMessage.includes("found")) {
         error_message.innerText = "Unknown Username";
         username_input.parentElement.classList.add('incorrect');
      } 
      else if (lowerMessage.includes("password") || lowerMessage.includes("incorrect") || lowerMessage.includes("wrong")) {
         error_message.innerText = "Wrong Password";
         password_input.parentElement.classList.add('incorrect');
      } 
      else {
         error_message.innerText = serverError || "Invalid Credentials";
      }
   }

   } catch (error) {
      console.error("Backend Error:", error);
      error_message.innerText = "Can't connect to server. Build backend application.";
   }
});

// Strict check function: Only ensures fields are filled out locally
function getLoginFormErrors(username, password){
    let errors = []

    if(username === '' || username == null){
        errors.push('Username is required')
        username_input.parentElement.classList.add('incorrect')
    }

    if(password === '' || password == null){
        errors.push('Password is required')
        password_input.parentElement.classList.add('incorrect')
    }

    return errors;
}

// Track input changes to clear the red error styling on the fly
const allInputs = [username_input, password_input]

allInputs.forEach(input =>{
    input.addEventListener('input', () => {
        if(input.parentElement.classList.contains('incorrect')){
            input.parentElement.classList.remove('incorrect')
            error_message.innerText = ''
        }
    })
})