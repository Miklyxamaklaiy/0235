(() => {
  const slider = document.getElementById('ppSlider');
  if (!slider) return;

  const getStep = () => {
    const first = slider.querySelector('.pp-slide');
    if (!first) return 260;
    const style = window.getComputedStyle(slider);
    const gap = parseFloat(style.columnGap || style.gap || '14') || 14;
    return first.getBoundingClientRect().width + gap;
  };

  let timer = null;

  const tick = () => {
    const step = getStep();
    const max = slider.scrollWidth - slider.clientWidth - 2;
    const next = slider.scrollLeft + step;
    if (next >= max) {
      slider.scrollTo({ left: 0, behavior: 'smooth' });
    } else {
      slider.scrollBy({ left: step, behavior: 'smooth' });
    }
  };

  const start = () => {
    stop();
    timer = window.setInterval(tick, 3500);
  };

  const stop = () => {
    if (timer) window.clearInterval(timer);
    timer = null;
  };

  // pause on hover/touch for UX
  slider.addEventListener('mouseenter', stop);
  slider.addEventListener('mouseleave', start);
  slider.addEventListener('touchstart', stop, { passive: true });
  slider.addEventListener('touchend', start, { passive: true });

  start();
})();
