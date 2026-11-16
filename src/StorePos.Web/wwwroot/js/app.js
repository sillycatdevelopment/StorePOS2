// StorePOS 2.0 — small helpers used on every page (no dependencies).
(function () {
  // <form data-confirm="¿Seguro?"> or <button data-confirm="..."> asks before submitting.
  document.addEventListener('submit', function (e) {
    var form = e.target;
    var msg = (e.submitter && e.submitter.getAttribute('data-confirm')) || form.getAttribute('data-confirm');
    if (msg && !window.confirm(msg)) e.preventDefault();
  });

  // <select data-autosubmit> submits its form on change (filters).
  document.addEventListener('change', function (e) {
    if (e.target.matches('[data-autosubmit]')) e.target.form.submit();
  });

  // <a data-print> prints the page.
  document.addEventListener('click', function (e) {
    if (e.target.matches('[data-print]')) { e.preventDefault(); window.print(); }
  });
})();
