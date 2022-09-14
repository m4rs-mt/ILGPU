function easteregg() {
    const span = document.getElementById('easter-egg');
    if (span.textContent === 'ö') {
        span.textContent = 'œ';
    } else if (span.textContent === 'œ') {
        span.textContent = 'ö';
    }
}
