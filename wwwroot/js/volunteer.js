(() => {
  const imgs = ['/img/vol/v1.svg','/img/vol/v2.svg','/img/vol/v3.svg'];
  let idx = 0;

  function rotate(id) {
    const el = document.getElementById(id);
    if (!el) return;
    idx = (idx + 1) % imgs.length;
    el.style.opacity = '0.2';
    setTimeout(() => {
      el.src = imgs[idx];
      el.style.opacity = '1';
    }, 220);
  }

  setInterval(() => {
    rotate('volPhoto');
    rotate('volPhotoPage');
  }, 3500);
})();
