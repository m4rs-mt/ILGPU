---
# Add this header to tell Jekyll to process the file
---

function redirectAfterFew() {
    setTimeout(function () {
        window.location.href = '{{ "/" | relative_url }}';
    }, 3000);
}

window.addEventListener('load', redirectAfterFew);
