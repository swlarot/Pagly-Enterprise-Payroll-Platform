interface PaglyLogoProps {
  variant?: 'full' | 'icon';
  theme?: 'dark' | 'light';
  className?: string;
  size?: 'sm' | 'md' | 'lg';
}

const sizeMap = {
  sm: { full: 'h-8', icon: 'h-8 w-8' },
  md: { full: 'h-10', icon: 'h-10 w-10' },
  lg: { full: 'h-14', icon: 'h-14 w-14' },
};

export function PaglyLogo({ variant = 'full', theme = 'dark', className = '', size = 'md' }: PaglyLogoProps) {
  const sizeClass = sizeMap[size][variant];
  const textFill = theme === 'dark' ? 'white' : '#102a43';

  if (variant === 'icon') {
    return (
      <svg
        viewBox="0 0 64 64"
        fill="none"
        xmlns="http://www.w3.org/2000/svg"
        className={`${sizeClass} ${className}`}
      >
        <rect width="64" height="64" rx="16" fill="url(#paglyIconGrad)" />
        <path d="M18 32 L27 24 L40 37 L27 42 Z" fill="white" opacity="0.9" />
        <path d="M27 24 L40 20 L40 37 L27 24 Z" fill="white" opacity="0.6" />
        <circle cx="40" cy="29" r="4.5" fill="white" opacity="0.4" />
        <defs>
          <linearGradient id="paglyIconGrad" x1="0" y1="0" x2="64" y2="64" gradientUnits="userSpaceOnUse">
            <stop offset="0%" stopColor="#10b981" />
            <stop offset="100%" stopColor="#0d9488" />
          </linearGradient>
        </defs>
      </svg>
    );
  }

  return (
    <svg
      viewBox="0 0 240 60"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      className={`${sizeClass} ${className}`}
    >
      <rect x="0" y="6" width="48" height="48" rx="12" fill="url(#paglyFullGrad)" />
      <path d="M15 30 L22 23 L32 33 L22 37 Z" fill="white" opacity="0.9" />
      <path d="M22 23 L32 20 L32 33 L22 23 Z" fill="white" opacity="0.6" />
      <circle cx="32" cy="27" r="3.5" fill="white" opacity="0.4" />
      <text
        x="58"
        y="42"
        fontFamily="'Space Grotesk', sans-serif"
        fontWeight="700"
        fontSize="36"
        fill={textFill}
        letterSpacing="-0.5"
      >
        Pagly
      </text>
      <defs>
        <linearGradient id="paglyFullGrad" x1="0" y1="6" x2="48" y2="54" gradientUnits="userSpaceOnUse">
          <stop offset="0%" stopColor="#10b981" />
          <stop offset="100%" stopColor="#0d9488" />
        </linearGradient>
      </defs>
    </svg>
  );
}
