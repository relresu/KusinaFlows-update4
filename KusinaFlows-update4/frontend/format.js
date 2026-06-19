// ============================================================================
// KUSINAFLOW UNIFIED DATE & TIME FORMATTING
// Date  -> MM/DD/YYYY
// Time  -> 12-hour clock with AM/PM (e.g. 02:30 PM)
// ============================================================================
(function () {
  // Accepts: Date object, 8-digit UTD integer/string (YYYYMMDD),
  // "YYYY-MM-DD", or "YYYY-MM-DD HH:mm:ss"
  function parseAnyDate(input) {
    if (input instanceof Date) return isNaN(input.getTime()) ? null : input;
    if (input === null || input === undefined || input === "") return null;

    if (typeof input === "number") {
      const s = String(input).padStart(8, "0");
      return new Date(parseInt(s.slice(0, 4)), parseInt(s.slice(4, 6)) - 1, parseInt(s.slice(6, 8)));
    }

    if (typeof input === "string") {
      if (/^\d{8}$/.test(input)) {
        return new Date(parseInt(input.slice(0, 4)), parseInt(input.slice(4, 6)) - 1, parseInt(input.slice(6, 8)));
      }
      if (/^\d{4}-\d{2}-\d{2}$/.test(input)) {
        const [y, m, d] = input.split("-").map(Number);
        return new Date(y, m - 1, d);
      }
      const isoLike = input.includes("T") ? input : input.replace(" ", "T");
      const dt = new Date(isoLike);
      if (!isNaN(dt.getTime())) return dt;
    }

    return null;
  }

  function formatDateMMDDYYYY(input) {
    const d = parseAnyDate(input);
    if (!d) return "N/A";
    const mm = String(d.getMonth() + 1).padStart(2, "0");
    const dd = String(d.getDate()).padStart(2, "0");
    const yyyy = d.getFullYear();
    return `${mm}/${dd}/${yyyy}`;
  }

  function formatTime12Hour(input) {
    const d = parseAnyDate(input);
    if (!d) return "";
    let hours = d.getHours();
    const minutes = String(d.getMinutes()).padStart(2, "0");
    const ampm = hours >= 12 ? "PM" : "AM";
    hours = hours % 12;
    if (hours === 0) hours = 12;
    return `${String(hours).padStart(2, "0")}:${minutes} ${ampm}`;
  }

  // Combined "MM/DD/YYYY hh:mm AM/PM". Falls back gracefully for
  // sentinel values like "N/A", "-", or unparseable input.
  function formatDateTimeDisplay(input) {
    if (input === null || input === undefined || input === "") return "N/A";
    if (input === "N/A" || input === "-") return input;

    const d = parseAnyDate(input);
    if (!d) return String(input);

    const hasTimeComponent = typeof input !== "string" || /\d{2}:\d{2}/.test(input);
    return hasTimeComponent
      ? `${formatDateMMDDYYYY(d)} ${formatTime12Hour(d)}`
      : formatDateMMDDYYYY(d);
  }

  window.KFFormat = {
    parseAnyDate,
    formatDateMMDDYYYY,
    formatTime12Hour,
    formatDateTimeDisplay
  };
})();
