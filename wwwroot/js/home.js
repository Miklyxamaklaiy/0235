(() => {
  const mapEl = document.getElementById('mapBox');
  if (!mapEl) return;

  function ensureCss(href) {
    if ([...document.querySelectorAll('link[rel="stylesheet"]')].some(l => (l.href || '').includes(href))) return;
    const link = document.createElement('link');
    link.rel = 'stylesheet';
    link.href = href;
    document.head.appendChild(link);
  }

  function loadScript(src) {
    return new Promise((resolve, reject) => {
      const s = document.createElement('script');
      s.src = src;
      s.async = true;
      s.onload = resolve;
      s.onerror = reject;
      document.head.appendChild(s);
    });
  }

  async function ensureLeaflet() {
    if (window.L) return true;
    try {
      ensureCss('https://unpkg.com/leaflet@1.9.4/dist/leaflet.css');
      await loadScript('https://unpkg.com/leaflet@1.9.4/dist/leaflet.js');
    } catch (_) {}

    try {
      ensureCss('https://unpkg.com/leaflet-control-geocoder@2.4.0/dist/Control.Geocoder.css');
      await loadScript('https://unpkg.com/leaflet-control-geocoder@2.4.0/dist/Control.Geocoder.js');
    } catch (_) {}

    return !!window.L;
  }

  function debounce(fn, delay = 350) {
    let timer = null;
    return (...args) => {
      clearTimeout(timer);
      timer = setTimeout(() => fn(...args), delay);
    };
  }

  function escapeHtml(s) {
    return String(s ?? '').replace(/[&<>"']/g, (ch) => ({
      '&': '&amp;',
      '<': '&lt;',
      '>': '&gt;',
      '"': '&quot;',
      "'": '&#39;'
    }[ch]));
  }

  function truncate(text, max = 220) {
    const value = String(text ?? '').trim();
    if (!value) return '';
    return value.length <= max ? value : `${value.slice(0, max).trim()}…`;
  }

  (async () => {
    const ok = await ensureLeaflet();
    if (!ok) {
      mapEl.innerHTML = '<div class="p-4 text-muted">Карта не загрузилась. Проверь интернет и перезагрузи страницу.</div>';
      return;
    }

    const isAuth = document.body.dataset.auth === '1';
    const csrf = document.querySelector('meta[name="csrf-token"]')?.getAttribute('content') || '';

    const modalEl = document.getElementById('orgModal');
    const modal = modalEl && window.bootstrap ? new bootstrap.Modal(modalEl) : null;
    const modalTitle = document.getElementById('orgModalTitle');
    const modalMeta = document.getElementById('orgModalMeta');
    const modalDesc = document.getElementById('orgModalDesc');
    const modalContacts = document.getElementById('orgModalContacts');
    const modalLinks = document.getElementById('orgModalLinks');

    const map = L.map('mapBox', { zoomControl: true });
    map.setView([55.751244, 37.618423], 4);

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '&copy; OpenStreetMap contributors'
    }).addTo(map);

    if (window.L?.Control?.Geocoder) {
      L.Control.geocoder({ defaultMarkGeocode: true }).addTo(map);
    }

    let markers = [];
    let favSet = new Set();

    const orgList = document.getElementById('orgList');
    const orgCount = document.getElementById('orgCount');

    const filterQ = document.getElementById('filterQ');
    const filterCity = document.getElementById('filterCity');
    const filterCategory = document.getElementById('filterCategory');
    const filterFav = document.getElementById('filterFav');
    const btnApply = document.getElementById('btnApply');
    const btnReset = document.getElementById('btnReset');

    const api = {
      orgs: '/api/organizations',
      cities: '/api/organizations/cities',
      categories: '/api/organizations/categories',
      favList: '/api/favorites',
      favToggle: '/api/favorites/toggle'
    };

    function showOrgModal(o) {
      if (!modal) return;

      modalTitle.textContent = o.name || 'Организация';
      modalMeta.textContent = [o.category, o.city, o.address].filter(Boolean).join(' · ');
      modalDesc.textContent = o.shortDescription || 'Краткое описание пока не добавлено.';

      modalContacts.innerHTML = '';
      const contacts = [];
      if (o.address) contacts.push(`<div><strong>Адрес</strong><br>${escapeHtml(o.address)}</div>`);
      if (o.phone) contacts.push(`<div><strong>Телефон</strong><br>${escapeHtml(o.phone)}</div>`);
      if (o.email) contacts.push(`<div><strong>Email</strong><br><a href="mailto:${encodeURIComponent(o.email)}">${escapeHtml(o.email)}</a></div>`);
      modalContacts.innerHTML = contacts.length ? contacts.join('<div class="pp-orgmodal-gap"></div>') : '<div class="text-muted">Контакты не указаны.</div>';

      modalLinks.innerHTML = '';
      const links = [];
      if (o.website) {
        links.push(`<a class="pp-outline-btn text-center text-decoration-none" href="${o.website}" target="_blank" rel="noopener">Перейти на сайт</a>`);
      }
      if (o.sourceUrl) {
        links.push(`<a class="pp-outline-btn text-center text-decoration-none" href="${o.sourceUrl}" target="_blank" rel="noopener">Открыть источник</a>`);
      }
      modalLinks.innerHTML = links.length ? links.join('') : '<div class="text-muted">Внешние ссылки отсутствуют.</div>';

      modal.show();
    }

    async function loadFilters() {
      try {
        const [citiesResp, categoriesResp] = await Promise.all([
          fetch(api.cities),
          fetch(api.categories)
        ]);

        const cities = citiesResp.ok ? await citiesResp.json() : [];
        const categories = categoriesResp.ok ? await categoriesResp.json() : [];

        cities.forEach(c => {
          const opt = document.createElement('option');
          opt.value = c;
          opt.textContent = c;
          filterCity.appendChild(opt);
        });

        categories.forEach(c => {
          const opt = document.createElement('option');
          opt.value = c;
          opt.textContent = c;
          filterCategory.appendChild(opt);
        });
      } catch (e) {
        console.warn('Не удалось загрузить значения фильтров', e);
      }
    }

    async function loadFavorites() {
      if (!isAuth) return;
      try {
        const response = await fetch(api.favList);
        const ids = response.ok ? await response.json() : [];
        favSet = new Set(ids);
      } catch (e) {
        console.warn('Не удалось загрузить избранное', e);
      }
    }

    function clearMarkers() {
      markers.forEach(m => m.remove());
      markers = [];
    }

    function renderList(items) {
      orgCount.textContent = `${items.length}`;
      orgList.innerHTML = '';

      if (items.length === 0) {
        orgList.innerHTML = '<div class="p-3 text-muted">Организации пока не найдены. Запусти синхронизацию в админке или добавь запись вручную.</div>';
        return;
      }

      items.forEach(o => {
        const row = document.createElement('div');
        row.className = 'pp-orgitem';
        row.setAttribute('role', 'button');
        row.setAttribute('tabindex', '0');

        const left = document.createElement('div');
        left.className = 'pp-orgleft';

        const name = document.createElement('div');
        name.className = 'pp-orgname';
        name.textContent = o.name;

        const meta = document.createElement('div');
        meta.className = 'pp-orgmeta';
        meta.textContent = [o.category, o.city, o.address].filter(Boolean).join(' · ');

        const desc = document.createElement('div');
        desc.className = 'pp-orgdesc';
        desc.textContent = truncate(o.shortDescription, 155) || 'Краткое описание пока не добавлено.';

        left.appendChild(name);
        left.appendChild(meta);
        left.appendChild(desc);

        const actions = document.createElement('div');
        actions.className = 'pp-orgactions';

        const details = document.createElement('button');
        details.className = 'pp-orgbtn pp-orgbtn-details';
        details.type = 'button';
        details.textContent = 'i';
        details.title = 'Подробнее';
        details.addEventListener('click', (e) => {
          e.stopPropagation();
          showOrgModal(o);
        });

        const btn = document.createElement('button');
        btn.className = 'pp-orgbtn';
        btn.type = 'button';
        btn.title = 'В избранное';

        const setHeart = (on) => {
          btn.textContent = on ? '♥' : '♡';
          btn.classList.toggle('hearted', !!on);
        };
        setHeart(favSet.has(o.id));

        btn.addEventListener('click', async (e) => {
          e.stopPropagation();
          if (!isAuth) {
            alert('Чтобы использовать избранное, войдите в аккаунт.');
            return;
          }

          const form = new URLSearchParams();
          form.set('organizationId', String(o.id));

          const resp = await fetch(api.favToggle, {
            method: 'POST',
            headers: {
              'Content-Type': 'application/x-www-form-urlencoded',
              'X-CSRF-TOKEN': csrf
            },
            body: form.toString()
          });

          if (!resp.ok) return;

          const data = await resp.json();
          if (data.favorited) favSet.add(o.id);
          else favSet.delete(o.id);

          setHeart(data.favorited);

          if (filterFav.checked) {
            await loadOrganizations();
          }
        });

        actions.appendChild(details);
        actions.appendChild(btn);

        const focusOnOrg = () => {
          if (o.lat != null && o.lng != null) {
            map.setView([o.lat, o.lng], 11, { animate: true });
          }
          showOrgModal(o);
        };

        row.appendChild(left);
        row.appendChild(actions);

        row.addEventListener('click', focusOnOrg);
        row.addEventListener('keydown', (e) => {
          if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            focusOnOrg();
          }
        });

        orgList.appendChild(row);
      });
    }

    function renderMarkers(items) {
      clearMarkers();
      items.forEach(o => {
        if (o.lat == null || o.lng == null) return;

        const m = L.marker([o.lat, o.lng]).addTo(map);
        const links = [];
        if (o.website) links.push(`<a href="${o.website}" target="_blank" rel="noopener">сайт</a>`);
        if (o.sourceUrl) links.push(`<a href="${o.sourceUrl}" target="_blank" rel="noopener">источник</a>`);
        m.bindPopup(`<div style="min-width:260px">
          <div style="font-weight:700;margin-bottom:4px">${escapeHtml(o.name)}</div>
          <div style="color:#6c757d;font-size:12px;margin-bottom:6px">${escapeHtml([o.category, o.city, o.address].filter(Boolean).join(' · '))}</div>
          <div style="font-size:13px;line-height:1.45;margin-bottom:8px">${escapeHtml(truncate(o.shortDescription, 180) || 'Краткое описание пока не добавлено.')}</div>
          <div style="display:flex;gap:10px;flex-wrap:wrap">${links.join('')}</div>
        </div>`);
        m.on('click', () => showOrgModal(o));
        markers.push(m);
      });
    }

    async function loadOrganizations() {
      try {
        orgList.innerHTML = '<div class="p-3 text-muted">Загрузка...</div>';

        const params = new URLSearchParams();
        if (filterQ?.value?.trim()) params.set('q', filterQ.value.trim());
        if (filterCity?.value) params.set('city', filterCity.value);
        if (filterCategory?.value) params.set('category', filterCategory.value);
        if (filterFav?.checked) params.set('favoritesOnly', 'true');

        const response = await fetch(`${api.orgs}?${params.toString()}`);
        const items = response.ok ? await response.json() : [];

        renderList(items);
        renderMarkers(items);

        const pts = items
          .filter(x => x.lat != null && x.lng != null)
          .map(x => [x.lat, x.lng]);

        if (pts.length >= 2) {
          map.fitBounds(L.latLngBounds(pts), { padding: [20, 20] });
        } else if (pts.length === 1) {
          map.setView(pts[0], 11);
        } else {
          map.setView([55.751244, 37.618423], 4);
        }
      } catch (e) {
        console.error(e);
        orgList.innerHTML = '<div class="p-3 text-danger">Ошибка загрузки организаций.</div>';
      }
    }

    const debouncedLoad = debounce(loadOrganizations, 350);

    btnApply?.addEventListener('click', loadOrganizations);
    btnReset?.addEventListener('click', async () => {
      if (filterQ) filterQ.value = '';
      if (filterCity) filterCity.value = '';
      if (filterCategory) filterCategory.value = '';
      if (filterFav) filterFav.checked = false;
      await loadOrganizations();
    });

    filterQ?.addEventListener('input', debouncedLoad);
    filterQ?.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') {
        e.preventDefault();
        loadOrganizations();
      }
    });
    filterCity?.addEventListener('change', loadOrganizations);
    filterCategory?.addEventListener('change', loadOrganizations);
    filterFav?.addEventListener('change', loadOrganizations);

    await loadFilters();
    await loadFavorites();
    await loadOrganizations();
  })();
})();
