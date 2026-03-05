import React, { useState } from 'react';
import { ChevronDown, ChevronUp } from 'lucide-react';

interface CollapsibleSectionProps {
  title: string;
  icon?: React.ReactNode;
  children: React.ReactNode;
  defaultOpen?: boolean;
  badge?: string | number;
}

const CollapsibleSection: React.FC<CollapsibleSectionProps> = ({
  title,
  icon,
  children,
  defaultOpen = false,
  badge,
}) => {
  const [isOpen, setIsOpen] = useState(defaultOpen);

  return (
    <div className="border border-navy-600 rounded-lg overflow-hidden mb-4">
      <button
        type="button"
        onClick={() => setIsOpen(!isOpen)}
        className="w-full flex items-center justify-between px-4 py-3 bg-navy-800 hover:bg-navy-700 transition-colors text-left"
      >
        <div className="flex items-center gap-2">
          {icon && <span className="text-gray-400">{icon}</span>}
          <span className="text-sm font-medium text-gray-200">{title}</span>
          {badge !== undefined && (
            <span className="text-xs bg-primary-500/20 text-primary-400 px-2 py-0.5 rounded-full">
              {badge}
            </span>
          )}
        </div>
        {isOpen
          ? <ChevronUp className="w-4 h-4 text-gray-400" />
          : <ChevronDown className="w-4 h-4 text-gray-400" />
        }
      </button>
      {isOpen && (
        <div className="px-4 py-4 bg-navy-850 border-t border-navy-700">
          {children}
        </div>
      )}
    </div>
  );
};

export default CollapsibleSection;
