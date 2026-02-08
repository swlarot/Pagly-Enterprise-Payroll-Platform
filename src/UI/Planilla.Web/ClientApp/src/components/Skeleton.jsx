import React from 'react';

// Base skeleton with pulse animation
const SkeletonBase = ({ className = '' }) => (
    <div className={`animate-pulse bg-navy-700 rounded ${className}`}></div>
);

// Skeleton for cards
export const SkeletonCard = () => (
    <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
        <div className="flex items-center justify-between">
            <div className="flex-1">
                <SkeletonBase className="h-4 w-24 mb-3" />
                <SkeletonBase className="h-8 w-16 mb-4" />
                <SkeletonBase className="h-3 w-32" />
            </div>
            <SkeletonBase className="w-12 h-12 rounded-lg" />
        </div>
    </div>
);

// Skeleton for table rows
export const SkeletonTable = ({ rows = 5, columns = 6 }) => (
    <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 overflow-hidden">
        <div className="px-6 py-4 border-b border-navy-700">
            <SkeletonBase className="h-6 w-48" />
        </div>
        <div className="overflow-x-auto">
            <table className="w-full">
                <thead className="bg-navy-950 border-b border-navy-700">
                    <tr>
                        {Array.from({ length: columns }).map((_, i) => (
                            <th key={i} className="py-3 px-6">
                                <SkeletonBase className="h-4 w-24" />
                            </th>
                        ))}
                    </tr>
                </thead>
                <tbody className="bg-navy-900 divide-y divide-navy-700">
                    {Array.from({ length: rows }).map((_, rowIndex) => (
                        <tr key={rowIndex}>
                            {Array.from({ length: columns }).map((_, colIndex) => (
                                <td key={colIndex} className="py-4 px-6">
                                    <SkeletonBase className="h-4 w-full" />
                                </td>
                            ))}
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    </div>
);

// Skeleton for text lines
export const SkeletonText = ({ lines = 3, className = '' }) => (
    <div className={`space-y-3 ${className}`}>
        {Array.from({ length: lines }).map((_, i) => (
            <SkeletonBase key={i} className={`h-4 ${i === lines - 1 ? 'w-3/4' : 'w-full'}`} />
        ))}
    </div>
);

export default SkeletonBase;
