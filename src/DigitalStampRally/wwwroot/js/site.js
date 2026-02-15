// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

function formatJst(iso){
    const d = new Date(iso);
    if (isNaN(d)) return iso;

    const y = d.getFullYear();
    const m = d.getMonth() + 1;
    const day = d.getDate();
    const hh = d.getHours();     // ← padStartしない
    const mm = d.getMinutes();  // ← padStartしない

    return `${y}年${m}月${day}日 ${hh}時${mm}分`;
}

function formatAchievementCode(code){
    if (!code) return "";

    // 数字だけにする（念のため）
    const s = String(code).replace(/[^0-9]/g, "");

    // 例: 12345678 → 1234-5678
    return s.replace(/(\d{4})(?=\d)/g, "$1-");
}   

function sendEvent(name, params) {
    if (typeof gtag !== 'undefined') {
        gtag('event', name, params);
    }
}