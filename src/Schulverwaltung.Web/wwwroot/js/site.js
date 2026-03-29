// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

var _modalNavUrls = [];
var _modalNavIdx  = -1;

function openModal(url, navUrls, navIdx) {
    var frame    = document.getElementById('bc-modal-frame');
    var backdrop = document.getElementById('bc-modal');
    if (!frame || !backdrop) { location.href = url; return; }
    _modalNavUrls = navUrls || [];
    _modalNavIdx  = (navIdx !== undefined && navIdx !== null) ? navIdx : -1;
    frame.src = url;
    backdrop.classList.add('open');
    _updateNavArrows();
}

function _updateNavArrows() {
    var prev = document.getElementById('bc-modal-prev');
    var next = document.getElementById('bc-modal-next');
    if (!prev || !next) return;
    var hasNav = _modalNavUrls.length > 1 && _modalNavIdx >= 0;
    if (!hasNav || _modalNavIdx === 0) { prev.classList.add('bc-hidden'); } else { prev.classList.remove('bc-hidden'); }
    if (!hasNav || _modalNavIdx >= _modalNavUrls.length - 1) { next.classList.add('bc-hidden'); } else { next.classList.remove('bc-hidden'); }
}

function navigateModal(delta) {
    var newIdx = _modalNavIdx + delta;
    if (newIdx < 0 || newIdx >= _modalNavUrls.length) return;
    _modalNavIdx = newIdx;
    var frame = document.getElementById('bc-modal-frame');
    if (frame) frame.src = _modalNavUrls[_modalNavIdx];
    _updateNavArrows();
}

function closeModal(refresh) {
    var frame    = document.getElementById('bc-modal-frame');
    var backdrop = document.getElementById('bc-modal');
    if (!backdrop) return;
    backdrop.classList.remove('open');
    _modalNavUrls = [];
    _modalNavIdx  = -1;
    _updateNavArrows();
    setTimeout(function() { if (frame) frame.src = 'about:blank'; }, 250);
    if (refresh) location.reload();
}

document.addEventListener('DOMContentLoaded', function() {
    var backdrop = document.getElementById('bc-modal');
    if (backdrop) backdrop.addEventListener('click', function(e) {
        if (e.target === backdrop) closeModal(false);
    });

    // Keyboard: Escape closes, arrow keys navigate
    document.addEventListener('keydown', function(e) {
        var backdrop = document.getElementById('bc-modal');
        if (!backdrop || !backdrop.classList.contains('open')) return;
        if (e.key === 'Escape') { closeModal(false); }
        if (e.key === 'ArrowLeft')  { navigateModal(-1); }
        if (e.key === 'ArrowRight') { navigateModal(1); }
    });
});

window.addEventListener('message', function(e) {
    if (e.data && e.data.type === 'modalClose') closeModal(e.data.refresh);
});
