// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Display stored UTC dates using the visitor's device time zone.
document.addEventListener("DOMContentLoaded", () => {
    document.querySelectorAll("time.device-local-time[datetime]").forEach(element => {
        const date = new Date(element.getAttribute("datetime"));
        if (!Number.isNaN(date.getTime())) {
            element.textContent = new Intl.DateTimeFormat(undefined, {
                year: "numeric", month: "short", day: "2-digit", hour: "2-digit", minute: "2-digit"
            }).format(date);
            element.title = `Stored as ${date.toISOString()} UTC`;
        }
    });

    const offset = document.getElementById("client-utc-offset");
    if (offset) offset.value = new Date().getTimezoneOffset().toString();

    // Edit pages receive UTC database values. Convert them once to datetime-local device values.
    document.querySelectorAll("input[data-device-local-input]").forEach(input => {
        if (!input.value || input.dataset.localised === "true") return;
        const utc = new Date(`${input.value}:00Z`);
        if (Number.isNaN(utc.getTime())) return;
        const pad = value => String(value).padStart(2, "0");
        input.value = `${utc.getFullYear()}-${pad(utc.getMonth() + 1)}-${pad(utc.getDate())}T${pad(utc.getHours())}:${pad(utc.getMinutes())}`;
        input.dataset.localised = "true";
    });
});
