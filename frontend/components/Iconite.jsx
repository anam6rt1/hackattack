// components/Iconite.jsx — iconițele pentru cele 3 activități ale unui nivel

export function IconCamera({ className = "" }) {
  return (
    <svg viewBox="0 0 24 24" fill="currentColor" className={className} aria-hidden="true">
      <rect x="2" y="6" width="13" height="12" rx="2.5" />
      <path d="M16 10.5 22 7v10l-6-3.5z" />
    </svg>
  );
}

export function IconCarte({ className = "" }) {
  return (
    <svg viewBox="0 0 24 24" fill="currentColor" className={className} aria-hidden="true">
      <path d="M3 5.5C3 4.7 3.7 4 4.5 4H10c1.1 0 2 .9 2 2v14c0-.8-.7-1.5-1.5-1.5h-6C3.7 18.5 3 17.8 3 17z" />
      <path d="M21 5.5c0-.8-.7-1.5-1.5-1.5H14c-1.1 0-2 .9-2 2v14c0-.8.7-1.5 1.5-1.5h6c.8 0 1.5-.7 1.5-1.5z" />
    </svg>
  );
}

export function IconPlay({ className = "" }) {
  return (
    <svg viewBox="0 0 24 24" fill="currentColor" className={className} aria-hidden="true">
      <path d="M7 4.5v15l13-7.5z" />
    </svg>
  );
}

export const ACTIVITATI = [
  { tip: "video", Icon: IconCamera, eticheta: "Videoclip" },
  { tip: "materiale", Icon: IconCarte, eticheta: "Materiale" },
  { tip: "play", Icon: IconPlay, eticheta: "Întrebări" },
];
