const downloads = {
  windows: { label: 'Windows', href: 'https://github.com/agailloty/ExifRenamer/releases/latest/download/ExifRenamer-windows-x64-Setup.exe', icon: '⊞' },
  macos: { label: 'macOS', href: 'https://github.com/agailloty/ExifRenamer/releases/latest', icon: '●' },
  linux: { label: 'Linux', href: 'https://github.com/agailloty/ExifRenamer/releases/latest/download/ExifRenamer-linux-x64.deb', icon: '◆' }
};

function detectOS() {
  const value = `${navigator.userAgent} ${navigator.platform}`.toLowerCase();
  if (value.includes('win')) return 'windows';
  if (value.includes('mac')) return 'macos';
  if (value.includes('linux') && !value.includes('android')) return 'linux';
  return null;
}

const os = detectOS();
const smartButton = document.querySelector('.smart-download');
if (os && downloads[os]) {
  const choice = downloads[os];
  document.querySelector('.detected-os').textContent = choice.label;
  document.querySelector('.os-icon').textContent = choice.icon;
  smartButton.href = choice.href;
  document.querySelectorAll('.download-choice').forEach((item) => item.classList.toggle('recommended', item.dataset.os === os));
}

const shots = {
  rename: ['assets/screenshot-renamer.svg', "Maquette de l'espace d'organisation de TidyMemo"],
  metadata: ['assets/screenshot-metadata.svg', "Maquette de l'explorateur de métadonnées de TidyMemo"],
  video: ['assets/screenshot-video.svg', "Maquette de l'espace de compression vidéo de TidyMemo"]
};
document.querySelectorAll('[data-shot]').forEach((tab) => tab.addEventListener('click', () => {
  document.querySelectorAll('[data-shot]').forEach((item) => item.setAttribute('aria-selected', String(item === tab)));
  const image = document.querySelector('#product-shot');
  image.classList.add('switching');
  window.setTimeout(() => { [image.src, image.alt] = shots[tab.dataset.shot]; image.classList.remove('switching'); }, 160);
}));

const menu = document.querySelector('.menu-toggle');
menu.addEventListener('click', () => {
  const open = menu.getAttribute('aria-expanded') === 'true';
  menu.setAttribute('aria-expanded', String(!open));
  document.querySelector('.main-nav').classList.toggle('open', !open);
});
document.querySelectorAll('.main-nav a').forEach((link) => link.addEventListener('click', () => {
  menu.setAttribute('aria-expanded', 'false'); document.querySelector('.main-nav').classList.remove('open');
}));

const observer = new IntersectionObserver((entries) => entries.forEach((entry) => {
  if (entry.isIntersecting) { entry.target.classList.add('visible'); observer.unobserve(entry.target); }
}), { threshold: 0.12 });
document.querySelectorAll('.reveal').forEach((element) => observer.observe(element));
document.querySelector('#year').textContent = new Date().getFullYear();
