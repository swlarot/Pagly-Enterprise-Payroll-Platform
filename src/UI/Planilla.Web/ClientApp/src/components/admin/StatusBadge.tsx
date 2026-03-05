import { CheckCircle2, XCircle } from 'lucide-react';

interface StatusBadgeProps {
  isActive: boolean;
  showIcon?: boolean;
}

export function StatusBadge({ isActive, showIcon = true }: StatusBadgeProps) {
  return (
    <span
      className={`inline-flex items-center gap-1 px-2 py-1 rounded-full text-xs font-medium ${
        isActive ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800'
      }`}
    >
      {showIcon &&
        (isActive ? <CheckCircle2 className="w-3 h-3" /> : <XCircle className="w-3 h-3" />)}
      {isActive ? 'Activo' : 'Inactivo'}
    </span>
  );
}
