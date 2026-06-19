// ============================================================================
// KUSINAFLOW UNIFIED DATA LAYER (API GATEWAY)
// ============================================================================


  window.KusinaDB = {
  async getInventory() {
    return JSON.parse(localStorage.getItem("inventory")) || [];
  },
  async saveInventory(inventoryData) {
    localStorage.setItem("inventory", JSON.stringify(inventoryData));
  },
  async getHistory() {
    return JSON.parse(localStorage.getItem("stockHistory")) || [];
  },
  async saveHistory(historyData) {
    localStorage.setItem("stockHistory", JSON.stringify(historyData));
  },
  async syncAll(inventory, history) {
    await this.saveInventory(inventory);
    await this.saveHistory(history);
  }
};
