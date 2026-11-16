// StorePOS 2.0 — point of sale. Vanilla JS, no dependencies.
// The browser only shows numbers; the server recomputes prices, costs and owners when the sale is saved.
(function () {
  'use strict';
  var root = document.getElementById('pos');
  if (!root) return;

  var isManager = root.dataset.manager === '1';
  var manualMode = root.dataset.mode === '0';
  var registerOpen = root.dataset.open === '1';
  var startupMode = root.dataset.startup === '1';      // sell untracked stock and record it as initial inventory
  var canRegister = root.dataset.canRegister === '1';  // may register unknown barcodes from here
  var allOwners = JSON.parse(root.dataset.owners || '[]');
  var token = document.querySelector('input[name="__RequestVerificationToken"]').value;
  var fmt = new Intl.NumberFormat('es-MX', { style: 'currency', currency: 'MXN' });
  var $ = function (id) { return document.getElementById(id); };

  var scan = $('scan'), results = $('results'), msg = $('msg'), cartBody = $('cart');
  var cart = [];          // { key, p, ownerId, qty (base units), disc (cents) }
  var method = null;      // { id, cash }
  var found = [];         // current search results
  var sel = -1;           // selected result index
  var busy = false, lastEsc = 0, searchTimer = null, keySeq = 0;

  if (!isManager) document.querySelectorAll('.manager-only').forEach(function (el) { el.style.display = 'none'; });

  function money(cents) { return fmt.format(cents / 100); }
  function scale(p) { return p.unit === 1 ? 1000 : 1; }
  function lineGross(l) { return l.p.unit === 1 ? Math.round(l.p.priceCents * l.qty / 1000) : l.p.priceCents * l.qty; }
  function fmtQty(p, q) { return p.unit === 1 ? (q / 1000).toLocaleString('es-MX', { maximumFractionDigits: 3 }) + ' kg' : q + ' pza'; }
  function stockOf(p, ownerId) {
    var s = p.owners.reduce(function (s, o) { return s + (ownerId == null || o.ownerId === ownerId ? o.qty : 0); }, 0);
    return ownerId == null ? s + (p.packUnits || 0) : s;   // pieces that can still come out of closed boxes
  }
  function say(text, kind) { msg.textContent = text || ''; msg.style.color = kind === 'error' ? 'var(--bad)' : kind === 'ok' ? 'var(--ok)' : ''; }
  function beep() { try { var a = new (window.AudioContext || window.webkitAudioContext)(); var o = a.createOscillator(); o.frequency.value = 300; o.connect(a.destination); o.start(); setTimeout(function () { o.stop(); a.close(); }, 150); } catch (e) { } }

  // ---------------------------------------------------------------- cart

  function add(p) {
    var ownerId = null;
    if (p.owners.length === 1) ownerId = p.owners[0].ownerId;
    // Scanning the same piece product again adds one to its most recent line (keeping the chosen owner).
    var existing = null;
    for (var i = cart.length - 1; i >= 0; i--) if (cart[i].p.id === p.id) { existing = cart[i]; break; }
    if (existing && p.unit === 0) {
      existing.qty += 1;
    } else {
      existing = { key: ++keySeq, p: p, ownerId: ownerId, qty: p.unit === 1 ? 1000 : 1, disc: 0 };
      cart.push(existing);
    }
    if (stockOf(p, null) <= 0) say(startupMode ? '“' + p.name + '” no tiene existencia registrada: se registrará como inventario inicial al cobrar.'
                                               : '“' + p.name + '” no tiene existencia registrada.', startupMode ? '' : 'error');
    else say('Agregado: ' + p.name, 'ok');
    render();
    if (p.unit === 1) {   // weighed product: go straight to the quantity
      var input = cartBody.querySelector('tr[data-key="' + existing.key + '"] input.qty');
      if (input) { input.focus(); input.select(); return; }
    }
    scan.focus();
  }

  function ownerOptions(l) {
    var html = '';
    var withStock = l.p.owners;
    if (withStock.length > 1 && l.p.needsOwnerChoice) html += '<option value="">— elegir dueño —</option>';
    else if (withStock.length !== 1) html += '<option value="">' + (withStock.length === 0 ? (l.p.packUnits ? 'Se abre una caja' : 'Sin existencia') : 'Automático') + '</option>';
    withStock.forEach(function (o) {
      html += '<option value="' + o.ownerId + '"' + (l.ownerId === o.ownerId ? ' selected' : '') + '>' + esc(o.name) + ' (' + fmtQty(l.p, o.qty) + ')</option>';
    });
    if (isManager) {   // allow attributing an authorized no-stock sale to any active owner
      allOwners.filter(function (a) { return !withStock.some(function (o) { return o.ownerId === a.id; }); })
        .forEach(function (a) { html += '<option value="' + a.id + '"' + (l.ownerId === a.id ? ' selected' : '') + '>' + esc(a.name) + ' (sin existencia)</option>'; });
    }
    return html;
  }

  function esc(s) { return String(s).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); }

  function render() {
    if (cart.length === 0) {
      cartBody.innerHTML = '<tr class="empty"><td colspan="7" class="muted">Sin productos. Escanea o busca para agregar.</td></tr>';
    } else {
      cartBody.innerHTML = cart.map(function (l) {
        var needOwner = l.p.needsOwnerChoice && l.ownerId === null;
        var avail = stockOf(l.p, l.ownerId) + (l.ownerId == null ? 0 : (l.p.packUnits || 0));
        var short = l.qty > avail && !startupMode;
        return '<tr data-key="' + l.key + '">' +
          '<td>' + esc(l.p.name) + (short ? ' <span class="badge bad">existencia: ' + fmtQty(l.p, avail) + '</span>' : '') + '</td>' +
          '<td><select class="owner' + (needOwner ? ' need-owner' : '') + '">' + ownerOptions(l) + '</select></td>' +
          '<td class="num"><input class="qty" type="number" min="0" step="' + (l.p.unit === 1 ? '0.001' : '1') + '" value="' + (l.qty / scale(l.p)) + '"></td>' +
          '<td class="num">' + money(l.p.priceCents) + (l.p.unit === 1 ? '/kg' : '') + '</td>' +
          '<td class="num"' + (isManager ? '' : ' style="display:none"') + '><input class="disc money" type="number" min="0" step="0.01" value="' + (l.disc / 100) + '"></td>' +
          '<td class="num">' + money(lineGross(l) - l.disc) + '</td>' +
          '<td><button type="button" class="btn small remove" title="Quitar">✕</button></td></tr>';
      }).join('');
    }
    totals();
  }

  function totals() {
    var sub = cart.reduce(function (s, l) { return s + lineGross(l); }, 0);
    var lineDisc = cart.reduce(function (s, l) { return s + l.disc; }, 0);
    var ticketDisc = isManager ? Math.round((parseFloat($('ticketDiscount').value) || 0) * 100) : 0;
    var total = Math.max(0, sub - lineDisc - ticketDisc);
    $('subtotal').textContent = money(sub);
    $('discountTotal').textContent = money(lineDisc + ticketDisc);
    $('total').textContent = money(total);
    var received = Math.round((parseFloat($('received').value) || 0) * 100);
    $('change').textContent = received > 0 ? money(Math.max(0, received - total)) : '$0.00';
    return { total: total, ticketDisc: ticketDisc, received: received };
  }

  cartBody.addEventListener('change', function (e) {
    var tr = e.target.closest('tr[data-key]'); if (!tr) return;
    var l = cart.find(function (x) { return x.key === +tr.dataset.key; }); if (!l) return;
    if (e.target.classList.contains('owner')) l.ownerId = e.target.value === '' ? null : +e.target.value;
    if (e.target.classList.contains('qty')) {
      var v = parseFloat(e.target.value);
      if (!(v > 0)) { cart.splice(cart.indexOf(l), 1); }
      else l.qty = l.p.unit === 1 ? Math.round(v * 1000) : Math.round(v);
    }
    if (e.target.classList.contains('disc')) l.disc = Math.max(0, Math.round((parseFloat(e.target.value) || 0) * 100));
    render();
  });
  cartBody.addEventListener('keydown', function (e) {
    if (e.key === 'Enter' && e.target.matches('input')) { e.preventDefault(); e.target.dispatchEvent(new Event('change', { bubbles: true })); scan.focus(); }
  });
  cartBody.addEventListener('click', function (e) {
    if (!e.target.classList.contains('remove')) return;
    var key = +e.target.closest('tr').dataset.key;
    cart = cart.filter(function (l) { return l.key !== key; });
    render(); scan.focus();
  });

  // ---------------------------------------------------------------- search & scan

  function showResults(list) {
    found = list; sel = list.length ? 0 : -1;
    if (!list.length) { results.hidden = true; return; }
    results.innerHTML = list.map(function (p, i) {
      var st = stockOf(p, null);
      return '<button type="button" data-i="' + i + '" class="' + (i === 0 ? 'sel' : '') + '"><span>' + esc(p.name) +
        (p.barcode ? ' <span class="muted small">' + esc(p.barcode) + '</span>' : '') + '</span><span>' + money(p.priceCents) +
        ' <span class="badge ' + (st > 0 ? 'ok' : 'bad') + '">' + fmtQty(p, st) + '</span></span></button>';
    }).join('');
    results.hidden = false;
  }
  function hideResults() { results.hidden = true; found = []; sel = -1; }
  function moveSel(d) {
    if (!found.length) return;
    sel = (sel + d + found.length) % found.length;
    results.querySelectorAll('button').forEach(function (b, i) { b.classList.toggle('sel', i === sel); });
  }
  results.addEventListener('click', function (e) {
    var b = e.target.closest('button[data-i]'); if (!b) return;
    add(found[+b.dataset.i]); hideResults(); scan.value = '';
  });

  // If the black StorePOS window is in "Seleccionar" mode the server is paused and requests never finish.
  var FROZEN = 'El servidor no responde. Revisa la ventana negra de StorePOS: si su título empieza con “Seleccionar”, haz clic en ella y presiona Esc.';
  function fetchT(url, options, ms) {
    var ctrl = window.AbortController ? new AbortController() : null;
    var timer = ctrl ? setTimeout(function () { ctrl.abort(); }, ms) : null;
    options = options || {};
    if (ctrl) options.signal = ctrl.signal;
    return fetch(url, options).finally(function () { if (timer) clearTimeout(timer); });
  }
  function get(url) { return fetchT(url, { headers: { 'Accept': 'application/json' } }, 8000).then(function (r) { if (!r.ok) throw new Error(r.status); return r.json(); }); }

  function search(text) {
    return get('/Pos?handler=Search&q=' + encodeURIComponent(text)).then(showResults).catch(function () { say(FROZEN, 'error'); });
  }

  function submitScan() {
    var text = scan.value.trim();
    if (!text) return;
    if (sel >= 0 && found.length && !results.hidden && !/^\d{6,}$/.test(text)) { add(found[sel]); hideResults(); scan.value = ''; return; }
    get('/Pos?handler=Lookup&code=' + encodeURIComponent(text)).then(function (r) {
      if (r.found) { add(r.product); scan.value = ''; hideResults(); return; }
      return get('/Pos?handler=Search&q=' + encodeURIComponent(text)).then(function (list) {
        if (list.length === 1) { add(list[0]); scan.value = ''; hideResults(); }
        else if (list.length === 0) {
          if (canRegister) { openQuick(text); say('Código nuevo: regístralo para venderlo.'); }
          else { beep(); say('No se encontró “' + text + '”. Pide a un encargado que lo dé de alta.', 'error'); scan.select(); }
        }
        else { showResults(list); say(list.length + ' resultados: usa ↑ ↓ y Enter.'); }
      });
    }).catch(function () { say(FROZEN, 'error'); });
  }

  scan.addEventListener('keydown', function (e) {
    if (e.key === 'Enter') { e.preventDefault(); clearTimeout(searchTimer); submitScan(); }
    else if (e.key === 'ArrowDown') { e.preventDefault(); moveSel(1); }
    else if (e.key === 'ArrowUp') { e.preventDefault(); moveSel(-1); }
  });
  scan.addEventListener('input', function () {
    clearTimeout(searchTimer);
    var text = scan.value.trim();
    // Scanners type digits very fast and end with Enter: don't search while digits are arriving.
    if (text.length < 2 || /^\d+$/.test(text)) { hideResults(); return; }
    searchTimer = setTimeout(function () { search(text); }, 250);
  });

  // ---------------------------------------------------------------- quick product registration

  var quick = $('quick');
  function openQuick(text) {
    if (!quick) return;
    hideResults();
    var t = (text || '').trim();
    var isCode = /^[0-9A-Za-z\-]{4,}$/.test(t) && /\d/.test(t) && !/\s/.test(t);
    $('qBarcode').value = isCode ? t : '';
    $('qName').value = isCode ? '' : t;
    ['qPrice', 'qCost', 'qShelf', 'qMin'].forEach(function (id) { $(id).value = ''; });
    $('qUnit').value = '0';
    quick.hidden = false;
    (isCode || !t ? $('qName') : $('qPrice')).focus();
    scan.value = '';
  }
  function closeQuick() { if (quick) { quick.hidden = true; scan.focus(); } }
  if (quick) {
    $('openQuick').addEventListener('click', function () { openQuick(scan.value); });
    $('cancelQuick').addEventListener('click', closeQuick);
    quick.addEventListener('submit', function (e) {
      e.preventDefault();
      if (busy) return;
      var unit = +$('qUnit').value, sc = unit === 1 ? 1000 : 1;
      var body = {
        name: $('qName').value.trim(),
        barcode: $('qBarcode').value.trim() || null,
        categoryId: $('qCategory').value ? +$('qCategory').value : null,
        unit: unit,
        salePriceCents: Math.round((parseFloat($('qPrice').value) || 0) * 100),
        purchasePriceCents: Math.round((parseFloat($('qCost').value) || 0) * 100),
        minStock: Math.round((parseFloat($('qMin').value) || 0) * sc),
        ownerId: $('qOwner').value ? +$('qOwner').value : null,
        shelfQty: Math.round((parseFloat($('qShelf').value) || 0) * sc)
      };
      if (unit === 0 && (body.shelfQty !== Math.round(body.shelfQty))) { say('La existencia por pieza debe ser un número entero.', 'error'); return; }
      busy = true;
      fetchT('/Pos?handler=QuickAdd', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
        body: JSON.stringify(body)
      }, 20000).then(function (r) { return r.json(); }).then(function (r) {
        if (!r.ok) { say(r.error, 'error'); return; }
        quick.hidden = true;
        add(r.product);
        say('Producto registrado y agregado: ' + r.product.name, 'ok');
      }).catch(function () { say(FROZEN, 'error'); })
        .finally(function () { busy = false; });
    });
  }

  // ---------------------------------------------------------------- payment

  $('methods').addEventListener('click', function (e) {
    var b = e.target.closest('button[data-id]'); if (!b) return;
    selectMethod(b);
  });
  function selectMethod(b) {
    document.querySelectorAll('#methods button').forEach(function (x) { x.classList.toggle('sel', x === b); });
    method = { id: +b.dataset.id, cash: b.dataset.cash === '1' };
    $('cashBox').style.display = method.cash ? '' : 'none';
    if (method.cash) $('received').focus();
  }
  var firstCash = document.querySelector('#methods button[data-cash="1"]') || document.querySelector('#methods button');
  if (firstCash) selectMethod(firstCash);
  scan.focus();

  $('received').addEventListener('input', totals);
  $('received').addEventListener('keydown', function (e) { if (e.key === 'Enter') { e.preventDefault(); pay(); } });
  if (isManager) $('ticketDiscount').addEventListener('input', totals);

  function pay() {
    if (busy) return;
    if (!registerOpen) { say('La caja está cerrada. Ábrela en “Caja y corte”.', 'error'); return; }
    if (!cart.length) { say('No hay productos en la venta.', 'error'); return; }
    if (!method) { say('Selecciona la forma de pago.', 'error'); return; }
    var missing = cart.find(function (l) { return l.p.needsOwnerChoice && l.ownerId === null; });
    if (missing) { say('Selecciona de qué dueño se vende “' + missing.p.name + '”.', 'error'); return; }
    var t = totals();
    if (method.cash && t.received > 0 && t.received < t.total) { say('El efectivo recibido es menor al total.', 'error'); $('received').focus(); return; }

    var body = {
      lines: cart.map(function (l) { return { productId: l.p.id, quantity: l.qty, ownerId: l.ownerId, discountCents: l.disc }; }),
      paymentMethodId: method.id,
      cashReceivedCents: method.cash ? t.received : 0,
      discountCents: t.ticketDisc,
      notes: $('notes').value,
      authorizeNoStock: isManager && $('authorize').checked
    };
    busy = true; $('pay').disabled = true; say('Guardando venta…');
    fetchT('/Pos?handler=Complete', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
      body: JSON.stringify(body)
    }, 30000).then(function (r) { return r.json(); }).then(function (r) {
      if (!r.ok) { beep(); say(r.error, 'error'); return; }
      $('lastSale').hidden = false;
      $('lastId').textContent = '#' + r.id;
      $('lastTotal').textContent = r.total;
      $('lastChange').textContent = r.change;
      $('lastReceipt').href = '/Sales/Receipt?id=' + r.id;
      say('Venta #' + r.id + ' registrada. Cambio: ' + r.change, 'ok');
      cart = []; $('received').value = ''; $('notes').value = '';
      if (isManager) { $('ticketDiscount').value = '0'; $('authorize').checked = false; }
      render(); scan.focus();
    }).catch(function () {
      beep(); say(FROZEN + ' Antes de volver a cobrar, revisa en el Historial de ventas si la venta ya quedó registrada.', 'error');
    }).finally(function () { busy = false; $('pay').disabled = false; });
  }
  $('pay').addEventListener('click', pay);

  $('clear').addEventListener('click', function () {
    if (cart.length && !confirm('¿Vaciar la venta actual?')) return;
    cart = []; render(); scan.focus();
  });

  document.addEventListener('keydown', function (e) {
    if (e.key === 'F2') { e.preventDefault(); scan.focus(); scan.select(); }
    else if (e.key === 'F9') { e.preventDefault(); pay(); }
    else if (e.key === 'F4' && quick) { e.preventDefault(); openQuick(scan.value); }
    else if (e.key === 'Escape') {
      if (quick && !quick.hidden) { closeQuick(); return; }
      if (!results.hidden) { hideResults(); return; }
      var now = Date.now();
      if (now - lastEsc < 600 && cart.length) { cart = []; render(); say('Venta vaciada.'); }
      lastEsc = now; scan.focus();
    }
  });
  window.addEventListener('beforeunload', function (e) { if (cart.length) { e.preventDefault(); e.returnValue = ''; } });
  render();
})();
