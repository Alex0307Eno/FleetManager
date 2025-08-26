document.addEventListener("DOMContentLoaded", () => {
    // 點任何 <a> 連結時顯示 loading
    document.querySelectorAll("a").forEach(link => {
        link.addEventListener("click", e => {
            const href = link.getAttribute("href");
            if (href && !href.startsWith("#") && !href.startsWith("javascript:")) {
                document.getElementById("loading-overlay").style.display = "flex";
            }
        });
    });

    // 頁面完全載入後，隱藏 loading
    window.addEventListener("pageshow", () => {
        document.getElementById("loading-overlay").style.display = "none";
    });
});