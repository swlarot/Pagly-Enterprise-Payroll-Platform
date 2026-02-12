---
name: pagly-react
description: >
  Generate React 19 components and pages for Pagly payroll SaaS.
  Use when creating components, forms, tables, modals, dashboard widgets, or pages.
  Follows project conventions: JSX, Tailwind CSS dark theme (navy/emerald),
  custom UI components from components/ui/, Lucide Icons, react-hot-toast.
allowed-tools: Read, Write, Edit, Bash, Glob, Grep
---
# Pagly React Frontend Specialist

## Stack
- React 19 with Vite (JSX, NOT TypeScript)
- Tailwind CSS with dark theme
- Lucide Icons for all icons
- react-hot-toast for notifications
- recharts for charts/graphs
- Custom UI components in `components/ui/`

## Dark Theme (MANDATORY)
- Backgrounds: `bg-navy-950`, `bg-navy-900`, `bg-gray-800`
- Accents: `text-emerald-500`, `bg-emerald-600`, `hover:bg-emerald-700`
- Text: `text-gray-100`, `text-gray-200`, `text-gray-300`, `text-gray-400`
- Borders: `border-gray-700`, `border-gray-600`
- Cards: `bg-gray-800 border border-gray-700 rounded-lg`
- Inputs: `bg-gray-700 border-gray-600 text-gray-100`

## Page Structure (ALWAYS follow this pattern)
```jsx
import React, { useEffect, useState } from 'react';
import { Card, CardBody } from '../components/ui/Card';
import { Button } from '../components/ui/Button';
import { Loader2 } from 'lucide-react';
import toast from 'react-hot-toast';

export default function MiPaginaPage() {
  const [data, setData] = useState([]);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => { loadData(); }, []);

  const loadData = async () => {
    try {
      setIsLoading(true);
      const token = localStorage.getItem('token');
      const res = await fetch('/api/endpoint', {
        headers: { 'Authorization': `Bearer ${token}` }
      });
      if (!res.ok) throw new Error('Error al cargar datos');
      setData(await res.json());
    } catch (error) {
      toast.error(error.message || 'Error');
    } finally {
      setIsLoading(false);
    }
  };

  if (isLoading) return <div className="flex justify-center p-8"><Loader2 className="animate-spin text-emerald-500" /></div>;

  return (
    <div className="space-y-6">
      <h1 className="text-3xl font-bold text-gray-100">Mi Página</h1>
      <Card><CardBody>{/* Contenido */}</CardBody></Card>
    </div>
  );
}
```

## Available UI Components (use these FIRST, never raw HTML for inputs/buttons)
- `Button` → `components/ui/Button`
- `Card`, `CardBody` → `components/ui/Card`
- `Input` → `components/ui/Input`
- `Select` → `components/ui/Select`
- `Badge` → `components/ui/Badge`
- `Modal` → `components/ui/Modal`

## Layouts
- `AuthLayout` → tenant user pages (sidebar + header)
- `SystemAdminLayout` → system admin pages
- `ProtectedRoute`, `RoleGuard`, `SystemAdminRoute` → `components/auth/`

## Rules
1. ALWAYS `export default function NombrePage()` — never named exports for pages
2. ALWAYS register new pages in `App.tsx` (import + route)
3. ALWAYS handle loading, error, and empty states
4. ALWAYS use `toast` from react-hot-toast for user messages
5. ALWAYS use Bearer token from localStorage for API calls
6. ALWAYS format currency with `Intl.NumberFormat('es-PA', { style: 'currency', currency: 'PAB' })`
7. NEVER use TypeScript — this project uses JSX
8. Verify `npm run build` passes after changes

## Key Pages Already Implemented
- EmpleadosPage: employee CRUD with Pay Info (PayPeriodType, HoursPerWeek, HoursPerPeriod, HourlyRate)
- PlanillasPage: payroll management with hours panel, PayPeriodType selector, generate-defaults
- HorasExtraPage: overtime management with limit validation, type suggestions, holiday detection, charts
- ReportesPage: reports with modal display including overtime report
- DashboardPage: main dashboard

## Chart Components (recharts)
Located in `components/charts/`:
- OvertimeByTypeBarChart
- OvertimeTrendLineChart
- OvertimeCostDistributionPieChart
- OvertimeLimitsChart
